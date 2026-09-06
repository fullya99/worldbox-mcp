using System;
using System.Threading;
using System.Threading.Tasks;

namespace WorldBoxBridge.Threading;

/// <summary>
/// A counted admission gate: hands out at most <see cref="Capacity"/> slots at a time and
/// refuses the rest instead of letting them pile up without bound.
/// </summary>
/// <remarks>
/// <para><b>Two callers, two answers to a full gate.</b> The HTTP bridge waits a little, because
/// a burst of short commands drains in milliseconds and a caller that waited 200 ms would rather
/// have its result than an error. The dispatcher's per-frame job registry does not wait at all,
/// because it is asked from inside a main-thread action and blocking there would cost the very
/// frame the bound exists to protect.</para>
///
/// <para><b>Not <see cref="IDisposable"/>, deliberately.</b> A <see cref="SemaphoreSlim"/> only
/// owns a disposable handle once <c>AvailableWaitHandle</c> has been read, which this type never
/// does. Leaving it undisposed costs nothing and avoids racing shutdown against a waiter, which
/// would surface as <see cref="ObjectDisposedException"/> in a request that was merely late.</para>
///
/// <para>Nothing here knows about Unity or BepInEx, which is what lets the test project link the
/// file directly.</para>
/// </remarks>
internal sealed class ConcurrencyGate
{
    private readonly SemaphoreSlim _slots;

    public ConcurrencyGate(int capacity)
    {
        // A gate of zero admits nobody, which is a configuration mistake rather than a policy,
        // so it clamps instead of deadlocking the bridge. The config layer clamps too; this one
        // is what makes the type safe on its own.
        Capacity = Math.Max(1, capacity);
        _slots = new SemaphoreSlim(Capacity, Capacity);
    }

    /// <summary>Slots this gate hands out. Always at least one.</summary>
    public int Capacity { get; }

    /// <summary>
    /// Takes a slot, waiting up to <paramref name="timeout"/> for one to free up. Returns false
    /// when the gate stayed full for that long, and the caller must not call <see cref="Exit"/>
    /// in that case.
    /// </summary>
    public Task<bool> TryEnterAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return _slots.WaitAsync(timeout, cancellationToken);
    }

    /// <summary>Takes a slot if one is free this instant, never waits.</summary>
    public bool TryEnter()
    {
        return _slots.Wait(0);
    }

    /// <summary>Returns a slot taken by a successful <see cref="TryEnter"/> or entry.</summary>
    /// <remarks>
    /// Throws <see cref="SemaphoreFullException"/> when called without a matching entry, and
    /// that is on purpose: an unmatched Exit means the accounting is already wrong, and a gate
    /// that quietly grew past its capacity is a bound that has stopped bounding. Callers on the
    /// main thread catch it rather than let it reach Unity, see <c>MainThreadDispatcher.Tick</c>.
    /// A test pins the behaviour so a future second removal path fails there instead.
    /// </remarks>
    public void Exit()
    {
        _slots.Release();
    }
}
