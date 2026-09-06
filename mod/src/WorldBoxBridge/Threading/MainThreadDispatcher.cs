using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace WorldBoxBridge.Threading;

/// <summary>
/// Marshals work from arbitrary threads onto Unity's main thread by injecting a callback
/// directly into the engine's <see cref="PlayerLoop"/>.
/// </summary>
/// <remarks>
/// <para><b>Why not a <see cref="MonoBehaviour"/>?</b> On WorldBox 0.51.2 (and likely other
/// Unity games), <see cref="MonoBehaviour"/> instances created from a BepInEx plugin's Awake
/// can be destroyed by Unity shortly after, even when placed on a <c>DontDestroyOnLoad</c>
/// GameObject. The plugin code never sees the destruction (no exception, just a silent stop
/// of <c>Update()</c>), which is the worst kind of failure.</para>
///
/// <para><b>PlayerLoop instead:</b> we hook a delegate into Unity's internal frame loop, /// the same mechanism the engine uses for built-in subsystems like the physics tick or the
/// animation update. This delegate lives in the engine's player-loop table, not as a managed
/// Component, so GameObject lifecycle quirks can't reach it. The same pattern is used by
/// <a href="https://github.com/BepInEx/RuntimeUnityEditor">RuntimeUnityEditor</a> and most
/// long-running BepInEx plugins for the same reason.</para>
///
/// <para><b>Per-action deadline, and what it is not:</b> the deadline is a queueing deadline.
/// <c>Tick</c> compares it against the clock just before it calls an action, so an action that
/// waited too long for a frame comes back as <c>MAIN_THREAD_TIMEOUT</c>, and an action that has
/// started runs to completion whatever it does. Nothing here can interrupt the main thread, so
/// blocking I/O must never be queued: gotcha 11 in <c>docs/game-api-notes.md</c> has the case
/// that taught us, and <c>LoadWorldCommand</c> the shape that avoids it.</para>
///
/// <para><b>Both queues are bounded, and to the same number.</b> <c>Tick</c> drains at most 32
/// queued actions per frame, and at most 32 per-frame jobs may be registered at once. A queued
/// action that misses its turn simply runs next frame; a job refused at the gate comes back as
/// <c>BUSY</c>, because there is no later frame at which it would cost less.</para>
/// </remarks>
public static class MainThreadDispatcher
{
    private static readonly ConcurrentQueue<PendingAction> Queue = new();
    private static readonly ConcurrentQueue<PerFrameJob> IncomingJobs = new();
    private static readonly List<PerFrameJob> ActiveJobs = new(); // main-thread only
    private static ManualLogSource? _log;
    private static bool _registered;

    /// <summary>Latest <c>Time.frameCount</c> sampled on the main thread.</summary>
    public static int LastTick { get; private set; }

    /// <summary>Number of times our PlayerLoop callback has fired since startup.</summary>
    public static long UpdateCount { get; private set; }

    /// <summary>Unity version string sampled on the main thread.</summary>
    public static string? UnityVersion { get; private set; }

    public static TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How many per-frame jobs may be registered at once. The same number as <c>maxPerFrame</c>
    /// in <see cref="Tick"/> and for the same reason: 32 delegate invocations is the most frame
    /// time this dispatcher is willing to spend on bridge work. The action queue has always been
    /// bounded that way, and a job that steps every frame until its deadline needed the same
    /// bound, only for longer: an <c>invoke_power</c> run with pulses holds its slot for up to
    /// its whole 25 s budget.
    /// </summary>
    private const int MaxActiveJobs = 32;

    /// <summary>
    /// Admission for <see cref="RunPerFrameOnMainThreadAsync{T}"/>. Taken on the registering
    /// thread and returned in <see cref="Tick"/>, on the main thread, where the job leaves
    /// <see cref="ActiveJobs"/>.
    /// </summary>
    /// <remarks>
    /// That removal is a job's only exit, so the count does not drift while the tick runs. It
    /// does drift if the tick stops, which happens if another plugin resets the PlayerLoop and
    /// drops our subsystem: registration keeps taking slots that nothing gives back, and after
    /// 32 jobs every further one is refused. Worth knowing rather than guarding, because a
    /// dispatcher that no longer ticks has already broken every main-thread command, and this
    /// gate is not where that would be noticed.
    /// </remarks>
    private static readonly ConcurrencyGate JobSlots = new(MaxActiveJobs);

    /// <summary>
    /// Unique marker type identifying our subsystem inside the PlayerLoop tree. Using a
    /// distinct private type means we never collide with built-in or third-party subsystems,
    /// and we can spot our entry on subsequent reads of the loop.
    /// </summary>
    private struct WorldBoxBridgeTick { }

    /// <summary>Injects the dispatcher into Unity's PlayerLoop. Idempotent.</summary>
    public static void Bootstrap(ManualLogSource log)
    {
        if (_registered)
        {
            return;
        }
        _log = log;

        var loop = PlayerLoop.GetCurrentPlayerLoop();
        if (!TryInjectInto(ref loop, typeof(Update)))
        {
            log.LogError(
                "Could not find the Update phase in Unity's PlayerLoop, dispatcher disabled. "
                    + "Commands that require main-thread access will fail."
            );
            return;
        }
        PlayerLoop.SetPlayerLoop(loop);
        _registered = true;
        log.LogInfo(
            "[dispatcher] injected into Unity PlayerLoop Update phase, survives MonoBehaviour lifecycle."
        );
    }

    /// <summary>Schedules <paramref name="work"/> on the next Unity frame.</summary>
    public static Task<T> RunOnMainThreadAsync<T>(
        Func<T> work,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        if (work == null)
        {
            throw new ArgumentNullException(nameof(work));
        }
        if (!_registered)
        {
            throw new InvalidOperationException(
                "MainThreadDispatcher not registered. Call Bootstrap() during plugin Awake()."
            );
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var effectiveTimeout = timeout ?? DefaultTimeout;

        Queue.Enqueue(
            new PendingAction(
                run: () =>
                {
                    try
                    {
                        tcs.TrySetResult(work());
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                },
                isAlreadyDone: () => tcs.Task.IsCompleted,
                onTimeout: () =>
                    tcs.TrySetException(
                        new TimeoutException(
                            $"Action exceeded its deadline of {effectiveTimeout.TotalSeconds:F1}s before reaching the main thread."
                        )
                    ),
                deadline: DateTime.UtcNow + effectiveTimeout
            )
        );

        RegisterCancellation(tcs, cancellationToken);
        return tcs.Task;
    }

    /// <inheritdoc cref="RunOnMainThreadAsync{T}(Func{T}, TimeSpan?, CancellationToken)"/>
    public static Task RunOnMainThreadAsync(
        Action work,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        return RunOnMainThreadAsync<object?>(
            () =>
            {
                work();
                return null;
            },
            timeout,
            cancellationToken
        );
    }

    /// <summary>
    /// Runs <paramref name="step"/> once per Unity frame on the main thread until it returns
    /// false, then completes the returned task with <paramref name="onCompleted"/>'s value.
    /// The first step runs on the frame AFTER registration, so a job registered from inside a
    /// dispatched action is spaced one full frame from that action, the synthetic equivalent
    /// of holding the mouse button down.
    /// </summary>
    public static Task<T> RunPerFrameOnMainThreadAsync<T>(
        Func<bool> step,
        Func<T> onCompleted,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        if (step == null)
        {
            throw new ArgumentNullException(nameof(step));
        }
        if (!_registered)
        {
            throw new InvalidOperationException(
                "MainThreadDispatcher not registered. Call Bootstrap() during plugin Awake()."
            );
        }

        if (!JobSlots.TryEnter())
        {
            // Refused rather than queued. A job that cannot step is worth less than one that
            // never started, and the caller gets a 503 it can retry, where a silent queue would
            // hand it a task that only starts moving once someone else's 25 s run ends.
            return Task.FromException<T>(
                new DispatcherSaturatedException(
                    $"The dispatcher already has {MaxActiveJobs} per-frame jobs running and "
                        + "cannot take another. Retry once one of them finishes."
                )
            );
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var effectiveTimeout = timeout ?? DefaultTimeout;
        IncomingJobs.Enqueue(
            new PerFrameJob(
                step: step,
                complete: () =>
                {
                    try
                    {
                        tcs.TrySetResult(onCompleted());
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                },
                fail: ex => tcs.TrySetException(ex),
                isAlreadyDone: () => tcs.Task.IsCompleted,
                onTimeout: () =>
                    tcs.TrySetException(
                        new TimeoutException(
                            $"Per-frame job exceeded its deadline of {effectiveTimeout.TotalSeconds:F1}s."
                        )
                    ),
                deadline: DateTime.UtcNow + effectiveTimeout
            )
        );
        RegisterCancellation(tcs, cancellationToken);
        return tcs.Task;
    }

    /// <summary>
    /// Wires cancellation into <paramref name="tcs"/> WITHOUT leaking the registration: the
    /// tokens passed here are typically process-lifetime (the bridge's shutdown token), so an
    /// undisposed registration would pin the TCS and its captured closures forever, a slow
    /// unbounded leak over a long game session. The registration is disposed as soon as the
    /// task settles by any route.
    /// </summary>
    private static void RegisterCancellation<T>(
        TaskCompletionSource<T> tcs,
        CancellationToken cancellationToken
    )
    {
        if (!cancellationToken.CanBeCanceled)
        {
            return;
        }
        var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        tcs.Task.ContinueWith(
            static (_, state) => ((CancellationTokenRegistration)state!).Dispose(),
            registration,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }

    private static void Tick()
    {
        UpdateCount++;
        LastTick = Time.frameCount;
        UnityVersion ??= Application.unityVersion;

        // Per-frame jobs first, and ingest before the action-queue drain below: a job
        // registered by an action running this frame must not step until the NEXT frame.
        while (IncomingJobs.TryDequeue(out var newJob))
        {
            ActiveJobs.Add(newJob);
        }
        // Guarded like the action drain below, which this loop was not before it started
        // returning slots. Two things here can throw and they want different answers, so they
        // are caught separately: a job that cannot even report is dropped and still gives its
        // slot back, while a slot that refuses to come back is logged and goes no further. An
        // exception escaping this loop would skip the remaining jobs AND the whole action drain
        // for the frame, which stalls the bridge rather than degrading one command.
        for (var i = ActiveJobs.Count - 1; i >= 0; i--)
        {
            bool finished;
            try
            {
                finished = !ActiveJobs[i].RunStep();
            }
            catch (Exception ex)
            {
                _log?.LogError($"Dispatcher caught an exception stepping a per-frame job: {ex}");
                finished = true;
            }
            if (!finished)
            {
                continue;
            }
            ActiveJobs.RemoveAt(i);
            try
            {
                JobSlots.Exit();
            }
            catch (Exception ex)
            {
                _log?.LogError($"Per-frame job slot accounting is off: {ex}");
            }
        }

        // Bound the per-frame work to avoid frame stutter from request bursts.
        const int maxPerFrame = 32;
        for (var i = 0; i < maxPerFrame; i++)
        {
            if (!Queue.TryDequeue(out var pending))
            {
                return;
            }
            if (pending.IsAlreadyDone())
            {
                continue;
            }
            if (DateTime.UtcNow > pending.Deadline)
            {
                pending.OnTimeout();
                continue;
            }
            try
            {
                pending.Run();
            }
            catch (Exception ex)
            {
                _log?.LogError($"Dispatcher caught unhandled exception: {ex}");
            }
        }
    }

    /// <summary>
    /// Recursively walks the PlayerLoop tree looking for the given phase type and appends our
    /// tick subsystem as its last child. Returns true on success.
    /// </summary>
    private static bool TryInjectInto(ref PlayerLoopSystem root, Type phase)
    {
        if (root.subSystemList == null)
        {
            return false;
        }
        var subs = new List<PlayerLoopSystem>(root.subSystemList);
        for (var i = 0; i < subs.Count; i++)
        {
            if (subs[i].type == phase)
            {
                var target = subs[i];
                var children = new List<PlayerLoopSystem>(
                    target.subSystemList ?? Array.Empty<PlayerLoopSystem>()
                );
                children.Add(
                    new PlayerLoopSystem
                    {
                        type = typeof(WorldBoxBridgeTick),
                        updateDelegate = Tick,
                    }
                );
                target.subSystemList = children.ToArray();
                subs[i] = target;
                root.subSystemList = subs.ToArray();
                return true;
            }
            // Recurse so we still work if Unity reorganises the loop someday.
            var child = subs[i];
            if (TryInjectInto(ref child, phase))
            {
                subs[i] = child;
                root.subSystemList = subs.ToArray();
                return true;
            }
        }
        return false;
    }

    private sealed class PerFrameJob
    {
        private readonly Func<bool> _step;
        private readonly Action _complete;
        private readonly Action<Exception> _fail;
        private readonly Func<bool> _isAlreadyDone;
        private readonly Action _onTimeout;
        private readonly DateTime _deadline;

        public PerFrameJob(
            Func<bool> step,
            Action complete,
            Action<Exception> fail,
            Func<bool> isAlreadyDone,
            Action onTimeout,
            DateTime deadline
        )
        {
            _step = step;
            _complete = complete;
            _fail = fail;
            _isAlreadyDone = isAlreadyDone;
            _onTimeout = onTimeout;
            _deadline = deadline;
        }

        /// <summary>One frame's worth of work. Returns false when the job is finished.</summary>
        public bool RunStep()
        {
            if (_isAlreadyDone())
            {
                return false; // cancelled from another thread
            }
            if (DateTime.UtcNow > _deadline)
            {
                _onTimeout();
                return false;
            }
            try
            {
                if (_step())
                {
                    return true;
                }
                _complete();
                return false;
            }
            catch (Exception ex)
            {
                _fail(ex);
                return false;
            }
        }
    }

    private readonly struct PendingAction
    {
        public PendingAction(
            Action run,
            Func<bool> isAlreadyDone,
            Action onTimeout,
            DateTime deadline
        )
        {
            Run = run;
            IsAlreadyDone = isAlreadyDone;
            OnTimeout = onTimeout;
            Deadline = deadline;
        }

        public Action Run { get; }
        public Func<bool> IsAlreadyDone { get; }
        public Action OnTimeout { get; }
        public DateTime Deadline { get; }
    }
}
