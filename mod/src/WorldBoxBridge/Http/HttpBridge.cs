using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WorldBoxBridge.Commands;
using WorldBoxBridge.Reflection;
using WorldBoxBridge.Session;
using WorldBoxBridge.Threading;
using SessionState = WorldBoxBridge.Session.Session;

namespace WorldBoxBridge.Http;

/// <summary>
/// Hosts the local HTTP API on a raw <see cref="TcpListener"/>.
/// </summary>
/// <remarks>
/// <para><b>Why not <see cref="System.Net.HttpListener"/>?</b> Under Unity's Mono runtime
/// (verified on Unity 2022.3.60f1), <c>HttpListener.Start()</c> returns successfully and
/// <c>IsListening</c> reports <c>true</c>, but no TCP socket is actually bound. This is a
/// long-standing bug in Mono's managed HTTP implementation, see
/// <see href="https://discussions.unity.com/t/httplistener-ignores-port-on-some-windows-platform-s/755558"/>
/// for the discussion. <see cref="TcpListener"/> bypasses the broken managed HTTP layer and
/// goes straight to the platform socket APIs, which work reliably.</para>
///
/// <para>The HTTP/1.1 subset we implement is intentionally minimal: connection-per-request,
/// no keep-alive, no chunked transfer encoding, no compression. Everything we need for a
/// loopback control plane and nothing more.</para>
/// </remarks>
internal sealed class HttpBridge : IDisposable
{
    /// <summary>
    /// Anti-GC anchor. Even if the BepInEx plugin MonoBehaviour gets destroyed (which happens
    /// on this game shortly after Awake), this static keeps the bridge instance alive for the
    /// full process lifetime so the accept thread + socket survive.
    /// </summary>
    private static HttpBridge? _alive;

    private readonly ManualLogSource _log;
    private readonly BridgeConfig _config;
    private readonly CommandRegistry _registry;
    private readonly VersionInfo _version;
    private readonly SessionState _session;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrencyGate _inFlight;
    private Task? _loop;

    /// <summary>Per-connection read timeout. Local agents respond fast, any longer = wedged.</summary>
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(35);

    /// <summary>
    /// How long a request waits for a free slot before it is told the bridge is busy.
    /// </summary>
    /// <remarks>
    /// The number comes from arithmetic, not taste. The Python client allows 35s per call and
    /// the dispatcher's queueing deadline is 30s, so the wait here is what is left of the
    /// client's headroom: at 5s a request that then times out on the main thread answered at
    /// 35s and the client reported the bridge unreachable instead of showing the
    /// <c>MAIN_THREAD_TIMEOUT</c> it had just been sent. 2s keeps 3s of margin, and buys the
    /// same thing 5s did, since a burst of short commands drains in milliseconds and anything
    /// stuck behind a 25s pulse run was never going to be admitted either way.
    /// </remarks>
    private static readonly TimeSpan AdmissionTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Deadline the handler holds over a command once it has started, whichever thread it
    /// started on. Long enough to be a backstop and not a policy: every command that means to
    /// take time bounds itself well inside it.
    /// </summary>
    private static readonly TimeSpan CommandBackstop = TimeSpan.FromSeconds(60);
    private const int MaxHeaderBytes = 16 * 1024;
    private const int MaxBodyBytes = 4 * 1024 * 1024;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
    };

    public HttpBridge(
        ManualLogSource log,
        BridgeConfig config,
        CommandRegistry registry,
        VersionInfo version,
        SessionState session
    )
    {
        _log = log;
        _config = config;
        _registry = registry;
        _version = version;
        _session = session;
        _inFlight = new ConcurrencyGate(_config.MaxConcurrentRequests.Value);
        // Mono Unity quirk: IPAddress.Parse("127.0.0.1") does not always behave the same as
        // the IPAddress.Loopback constant. Several Unity dev threads document the listener
        // silently failing to bind with Parse'd addresses where the constant works fine.
        // We treat the common loopback strings as aliases for the constant; other addresses
        // go through Parse normally.
        var host = _config.Host.Value;
        IPAddress bindAddress = host switch
        {
            "127.0.0.1" or "localhost" => IPAddress.Loopback,
            "::1" => IPAddress.IPv6Loopback,
            _ => IPAddress.Parse(host),
        };
        _log.LogInfo(
            $"[diag] resolved host '{host}' to IPAddress={bindAddress} (family={bindAddress.AddressFamily})"
        );
        _listener = new TcpListener(bindAddress, _config.Port.Value);
    }

    public void Start()
    {
        _config.AssertLoopbackOnly();
        _log.LogInfo("[diag] about to call _listener.Start()...");
        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            _log.LogError($"[diag] _listener.Start() THREW: {ex.GetType().FullName}: {ex.Message}");
            throw;
        }
        var sock = _listener.Server;
        _log.LogInfo(
            $"[diag] after Start(): IsBound={sock.IsBound} "
                + $"LocalEndPoint={sock.LocalEndPoint} "
                + $"Handle={sock.Handle} "
                + $"AddressFamily={sock.AddressFamily} "
                + $"SocketType={sock.SocketType} "
                + $"ProtocolType={sock.ProtocolType}"
        );
        _log.LogInfo(
            $"listening on http://{_config.Host.Value}:{_config.Port.Value} "
                + $"(commands={_registry.Count}, agents={_session.Agents.Count}, "
                + $"scenario={_session.ScenarioPreset}, legacy_mode={_session.Agents.IsLegacyMode})"
        );
        // Use a dedicated NON-background thread.
        //   - Mono's thread pool inside Unity has shown odd behavior with long-lived tasks.
        //   - A plain Thread bypasses the pool entirely.
        //   - IsBackground=false would prevent process shutdown until the thread exits, so we
        //     keep IsBackground=true but anchor the listener via _alive (anti-GC) instead.
        _alive = this;
        var t = new Thread(AcceptLoopBlocking)
        {
            IsBackground = true,
            Name = "WorldBoxBridge.Accept",
        };
        t.Start();
        _loop = Task.CompletedTask;
        _log.LogInfo(
            $"[diag] accept thread started (Id={t.ManagedThreadId}, IsBackground={t.IsBackground})"
        );
    }

    private void AcceptLoopBlocking()
    {
        _log.LogInfo(
            $"[accept-thread] entered. listener.IsBound={_listener.Server.IsBound} "
                + $"LocalEndPoint={_listener.Server.LocalEndPoint}"
        );
        // (self-connect probe removed, it triggered OnDestroy in some Unity configurations)
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = _listener.AcceptTcpClient();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException sex) when (_cts.IsCancellationRequested)
            {
                _log.LogInfo($"[accept-thread] socket closed during shutdown: {sex.Message}");
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(
                    $"[accept-thread] AcceptTcpClient threw: {ex.GetType().Name}: {ex.Message}"
                );
                Thread.Sleep(200);
                continue;
            }

            _ = Task.Run(() => HandleClientAsync(client, _cts.Token));
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            client.NoDelay = true;
            client.ReceiveTimeout = (int)ReadTimeout.TotalMilliseconds;
            client.SendTimeout = (int)ReadTimeout.TotalMilliseconds;

            try
            {
                using var stream = client.GetStream();
                var request = await ReadRequestAsync(stream, cancellationToken)
                    .ConfigureAwait(false);
                if (request == null)
                {
                    return; // empty / malformed; drop silently
                }

                var response = await RouteAsync(request, cancellationToken).ConfigureAwait(false);
                await WriteResponseAsync(stream, response, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The bridge is shutting down under a request. Expected, and the client is
                // about to lose the socket anyway, so there is nothing to report.
            }
            catch (Exception ex)
            {
                _log.LogError($"HandleClientAsync error: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Request parsing
    // ──────────────────────────────────────────────────────────────────────

    private sealed class HttpRequest
    {
        public string Method { get; set; } = "GET";
        public string Path { get; set; } = "/";
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public byte[] Body { get; set; } = Array.Empty<byte>();

        public string? GetHeader(string name)
        {
            return Headers.TryGetValue(name, out var v) ? v : null;
        }
    }

    private async Task<HttpRequest?> ReadRequestAsync(
        NetworkStream stream,
        CancellationToken cancellationToken
    )
    {
        var headers = await ReadHeadersAsync(stream, cancellationToken).ConfigureAwait(false);
        if (headers.TotalRead == 0)
        {
            return null; // peer closed before sending anything
        }
        var buffer = headers.Buffer;
        var totalRead = headers.TotalRead;
        var headerEnd = headers.HeaderEnd;
        var headerText = Encoding.ASCII.GetString(buffer, 0, headerEnd);
        var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
        if (lines.Length == 0)
        {
            return null;
        }

        var requestLine = lines[0].Split(' ');
        if (requestLine.Length < 2)
        {
            return null;
        }
        var req = new HttpRequest
        {
            Method = requestLine[0].ToUpperInvariant(),
            Path = requestLine[1],
        };

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }
            req.Headers[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
        }

        // Body, only if Content-Length is present and positive.
        if (int.TryParse(req.GetHeader("Content-Length"), out var len) && len > 0)
        {
            if (len > MaxBodyBytes)
            {
                throw new InvalidOperationException(
                    $"Body of {len} bytes exceeds {MaxBodyBytes} byte limit."
                );
            }
            req.Body = new byte[len];

            // For small requests, the body often arrives in the same TCP read as the headers.
            // Copy whatever leftover bytes the header read already consumed into req.Body before
            // pulling more from the stream, otherwise we'd block forever (or, with timeouts,
            // throw EndOfStreamException on a connection that's perfectly fine).
            var leftover = totalRead - headerEnd;
            if (leftover > 0)
            {
                var take = System.Math.Min(leftover, len);
                Array.Copy(buffer, headerEnd, req.Body, 0, take);
            }
            var read = System.Math.Min(leftover, len);
            while (read < len)
            {
                var chunk = await stream
                    .ReadAsync(req.Body, read, len - read, cancellationToken)
                    .ConfigureAwait(false);
                if (chunk <= 0)
                {
                    throw new EndOfStreamException("Client closed connection mid-body.");
                }
                read += chunk;
            }
        }

        return req;
    }

    /// <summary>
    /// Triple returned by <see cref="ReadHeadersAsync"/>. Plain struct rather than a
    /// <c>ValueTuple</c>, <c>System.ValueTuple</c> isn't always loadable under Unity's
    /// Mono runtime (out-of-band package on net462).
    /// </summary>
    private readonly struct HeaderReadResult
    {
        public HeaderReadResult(byte[] buffer, int totalRead, int headerEnd)
        {
            Buffer = buffer;
            TotalRead = totalRead;
            HeaderEnd = headerEnd;
        }

        public byte[] Buffer { get; }
        public int TotalRead { get; }
        public int HeaderEnd { get; }
    }

    /// <summary>
    /// Reads from the stream until the empty-line CRLF CRLF terminator. Returns the buffer,
    /// total bytes read, and the offset where headers end, so the caller can recover any body
    /// bytes that arrived in the same TCP read as the headers.
    /// </summary>
    private static async Task<HeaderReadResult> ReadHeadersAsync(
        NetworkStream stream,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[MaxHeaderBytes];
        var pos = 0;
        while (pos < MaxHeaderBytes)
        {
            var n = await stream
                .ReadAsync(buffer, pos, MaxHeaderBytes - pos, cancellationToken)
                .ConfigureAwait(false);
            if (n <= 0)
            {
                if (pos == 0)
                {
                    return new HeaderReadResult(buffer, 0, 0);
                }
                break;
            }
            pos += n;

            for (var i = 3; i < pos; i++)
            {
                if (
                    buffer[i - 3] == (byte)'\r'
                    && buffer[i - 2] == (byte)'\n'
                    && buffer[i - 1] == (byte)'\r'
                    && buffer[i] == (byte)'\n'
                )
                {
                    return new HeaderReadResult(buffer, pos, i + 1);
                }
            }
        }
        throw new InvalidOperationException(
            $"Request header exceeds {MaxHeaderBytes} bytes, refusing."
        );
    }

    // ──────────────────────────────────────────────────────────────────────
    // Routing, same logic as before, just using HttpRequest instead of HttpListenerRequest
    // ──────────────────────────────────────────────────────────────────────

    private sealed class HttpResponse
    {
        public int Status { get; set; } = 200;
        public string StatusText { get; set; } = "OK";
        public string ContentType { get; set; } = "application/json; charset=utf-8";
        public byte[] Body { get; set; } = Array.Empty<byte>();
    }

    private async Task<HttpResponse> RouteAsync(
        HttpRequest req,
        CancellationToken cancellationToken
    )
    {
        if (!_config.Enabled.Value)
        {
            return ErrorResponse(
                503,
                "Service Unavailable",
                ErrorCode.Disabled,
                "WorldBoxBridge is disabled. Set enabled = true in WorldBoxBridge.cfg."
            );
        }
        var ctx = Authenticate(req);
        if (ctx == null)
        {
            return ErrorResponse(
                401,
                "Unauthorized",
                ErrorCode.Unauthorized,
                "Missing or invalid credential. Send either 'Authorization: Bearer <token>' "
                    + "or the legacy 'X-WB-Token: <token>' header."
            );
        }

        var path = req.Path.Split('?')[0];
        if (path == "/health" && req.Method == "GET")
        {
            return await ExecuteCommandAsync("health", new JObject(), ctx.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        if (path == "/cmd" && req.Method == "POST")
        {
            return await HandleCommandAsync(req, ctx.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        if (path == "/capabilities" && req.Method == "GET")
        {
            return CapabilitiesResponse();
        }

        return ErrorResponse(
            404,
            "Not Found",
            ErrorCode.UnknownCommand,
            $"No route for {req.Method} {path}."
        );
    }

    private async Task<HttpResponse> HandleCommandAsync(
        HttpRequest req,
        RequestContext ctx,
        CancellationToken cancellationToken
    )
    {
        JObject body;
        try
        {
            var raw = req.Body.Length == 0 ? "{}" : Encoding.UTF8.GetString(req.Body);
            body = JObject.Parse(raw);
        }
        catch (JsonException ex)
        {
            return ErrorResponse(
                400,
                "Bad Request",
                ErrorCode.BadArgs,
                $"Request body is not valid JSON: {ex.Message}"
            );
        }

        var name = (string?)body["name"];
        if (string.IsNullOrWhiteSpace(name))
        {
            return ErrorResponse(
                400,
                "Bad Request",
                ErrorCode.BadArgs,
                "Body must contain a non-empty 'name' field."
            );
        }
        var args = body["args"] as JObject ?? new JObject();
        return await ExecuteCommandAsync(name!, args, ctx, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponse> ExecuteCommandAsync(
        string name,
        JObject args,
        RequestContext ctx,
        CancellationToken cancellationToken
    )
    {
        if (!_registry.TryGet(name, out var command))
        {
            return ErrorResponse(
                404,
                "Not Found",
                ErrorCode.UnknownCommand,
                $"No command named '{name}'.",
                commandName: name,
                args: args
            );
        }

        // Admission control. Past this point a single request can read a whole save into memory
        // or park a per-frame job for 25 seconds, so how many run at once is a number the bridge
        // chooses rather than one the client does. It sits here rather than around the whole
        // connection for two reasons: unauthenticated traffic must not be able to spend the
        // slots, and a client that dribbles its request in or reads its response slowly must not
        // hold one it is not using. What that second reason does NOT do is bound the socket. The
        // ReceiveTimeout set in HandleClientAsync only applies to synchronous socket calls, and
        // the bridge reads with ReadAsync, so a client that sends half a request line and stops
        // parks in ReadHeadersAsync with no deadline. It costs a socket and a pool thread, which
        // the accept loop still does not cap, it just no longer costs a slot. TODOS carries that
        // one. Awaited outside the try below so a cancellation on shutdown reaches
        // HandleClientAsync, which knows it is not an error.
        var admitted = await _inFlight
            .TryEnterAsync(AdmissionTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (!admitted)
        {
            _log.LogWarning(
                $"refused '{name}': {_inFlight.Capacity} requests already in flight and none "
                    + $"freed up within {AdmissionTimeout.TotalSeconds:F0}s."
            );
            // No `args` on this one. Every other error echoes them back to help the caller see
            // what it sent, but this is the load-shedding path: a refused `load_world` carrying
            // 4 MB of base64 would be serialized into the response, so refusing a request to
            // save memory would cost several more copies of the largest payload accepted.
            return ErrorResponse(
                503,
                "Service Unavailable",
                ErrorCode.Busy,
                $"The bridge is already running {_inFlight.Capacity} commands and none finished "
                    + $"within {AdmissionTimeout.TotalSeconds:F0}s. Retry. Raising "
                    + "max_concurrent_requests in WorldBoxBridge.cfg lifts this particular "
                    + "limit, though a command that registers a per-frame job has a second one "
                    + "that config does not reach.",
                commandName: name
            );
        }

        try
        {
            // Turn-based gate (Phase 4): in turn_based sessions, action + control commands
            // are reserved for the current-turn agent, minus the shared unblocks TurnGate
            // exempts. God-role agents (ActionGlobal) bypass the gate so a hierarchical "DM"
            // can always intervene. Meta / Discovery / Read / Bus commands are not gated,
            // they can be called any time.
            if (
                _session.TurnBased
                && _session.TurnOrder is not null
                && TurnGate.IsTurnGated(command.Name, command.Category)
                && !ctx.Has(Permission.ActionGlobal)
            )
            {
                var current = _session.TurnOrder.Current;
                if (current != ctx.AgentId)
                {
                    throw new WorldBoxBridge.Commands.Action.BridgeRejectionException(
                        ErrorCode.TurnNotYours,
                        $"Not your turn (current='{current}', you='{ctx.AgentId}'). "
                            + "Wait for them to call turn_advance, or, if you are the current "
                            + "agent in another session, check that the agents.json turn_order "
                            + "includes you."
                    );
                }
            }

            // Capture ctx in locals so the closure passed to MainThreadDispatcher captures the
            // struct by value (instance is small; struct copies dodge a closure-allocation surprise).
            var capturedCtx = ctx;
            Task<object?> commandTask;
            if (command.RequiresMainThread)
            {
                // Two awaits, deliberately: the dispatcher call starts ExecuteAsync on the main
                // thread; the command's own task is then awaited BELOW, off the main thread.
                // Blocking on it inside the dispatched callback (GetResult) would deadlock any
                // command whose task completes on a later frame, invoke_power's multi-pulse
                // path returns exactly such a task, completed by subsequent dispatcher ticks.
                commandTask = await MainThreadDispatcher
                    .RunOnMainThreadAsync(
                        () => command.ExecuteAsync(args, capturedCtx, cancellationToken),
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            else
            {
                // Task.Run, and not the plain call this used to be, because the body of a
                // command that reports false runs synchronously up to its first await, and
                // `load_world` reads the whole save in that stretch. Called inline, a read that
                // never returns parks THIS thread before the backstop below exists, so the
                // admission slot is never given back and eight of them take the bridge out
                // entirely. Handing the body to another pool thread is what lets a deadline
                // sit over it. This is not the ConfigureAwait/Task.Run trap CLAUDE.md warns
                // about: that one is about main-thread commands escaping the dispatcher, and
                // this branch is the one that never wanted a frame in the first place.
                commandTask = Task.Run(
                    () => command.ExecuteAsync(args, capturedCtx, cancellationToken),
                    cancellationToken
                );
            }

            // One backstop over both branches. A multi-frame command self-limits via its
            // dispatcher job deadline and a pool-thread command has no deadline at all, so
            // neither invariant lives here: this keeps a command that parks forever from
            // holding its admission slot for the life of the process. It does not unblock the
            // thread underneath, nothing in net462 can, it only stops one wedged call from
            // costing every later caller. The linked CTS is cancelled on the normal path so
            // the timer, and its registration on the long-lived shutdown token, is released
            // per request rather than lingering.
            using var backstopCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            var backstop = Task.Delay(CommandBackstop, backstopCts.Token);
            var finished = await Task.WhenAny(commandTask, backstop).ConfigureAwait(false);
            if (finished != commandTask)
            {
                // Observe the abandoned task's eventual fault so it can never surface as
                // an UnobservedTaskException.
                _ = commandTask.ContinueWith(
                    static t => _ = t.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted
                        | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default
                );
                throw new TimeoutException(
                    $"Command task did not complete within {CommandBackstop.TotalSeconds:F0}s "
                        + "of starting."
                );
            }
            backstopCts.Cancel();
            return SuccessResponse(await commandTask.ConfigureAwait(false));
        }
        catch (TimeoutException tex)
        {
            return ErrorResponse(
                504,
                "Gateway Timeout",
                ErrorCode.MainThreadTimeout,
                tex.Message,
                commandName: name,
                args: args,
                exception: ExceptionInfo.From(tex)
            );
        }
        catch (DispatcherSaturatedException dsex)
        {
            // The per-frame job registry is full. Same answer as a full admission gate, and for
            // the same reason: nothing is broken, the caller just has to come back.
            return ErrorResponse(
                503,
                "Service Unavailable",
                ErrorCode.Busy,
                dsex.Message,
                commandName: name,
                args: args
            );
        }
        catch (WorldBoxBridge.Commands.Action.BridgeRejectionException brex)
        {
            // Structured rejection from a command, map directly to its error code.
            var status = brex.Code switch
            {
                ErrorCode.UnknownAsset => 400,
                ErrorCode.OutOfBounds => 400,
                ErrorCode.BadArgs => 400,
                ErrorCode.GameRejected => 422,
                ErrorCode.PermissionDenied => 403,
                ErrorCode.FactionScopeViolation => 403,
                ErrorCode.TurnNotYours => 409,
                // Present so the one code with two producers cannot disagree with itself: the
                // dispatcher raises BUSY through its own exception type below, and a command
                // that ever raises it through this path would otherwise arrive as a 500 that
                // docs/protocol.md documents as a 503.
                ErrorCode.Busy => 503,
                _ => 500,
            };
            return ErrorResponse(
                status,
                "Rejected",
                brex.Code,
                brex.Message,
                commandName: name,
                args: args,
                didYouMean: brex.DidYouMean
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown reached the command, usually through the dispatcher cancelling its TCS.
            // Rethrown rather than answered: HandleClientAsync knows this is not an error, and
            // without this filter the catch-all below claims it and tells the caller the game
            // crashed. The finally still runs, so the slot goes back either way.
            throw;
        }
        catch (Exception ex)
        {
            return ErrorResponse(
                500,
                "Internal Server Error",
                ErrorCode.GameCrash,
                ex.Message,
                commandName: name,
                args: args,
                exception: ExceptionInfo.From(ex)
            );
        }
        finally
        {
            // Every path out of the try has to reach this, including a future early return added
            // above it. A slot that is taken and never given back is invisible until the eighth
            // one, at which point the bridge answers BUSY to everything and looks saturated by
            // load rather than by a leak.
            _inFlight.Exit();
        }
    }

    private HttpResponse CapabilitiesResponse()
    {
        var commands = new JArray();
        foreach (var cmd in _registry.All)
        {
            commands.Add(
                new JObject
                {
                    ["name"] = cmd.Name,
                    ["category"] = cmd.Category.ToString().ToLowerInvariant(),
                    ["description"] = cmd.Description,
                    ["requires_main_thread"] = cmd.RequiresMainThread,
                    ["schema"] = cmd.ArgsSchema,
                }
            );
        }
        var payload = new JObject
        {
            ["mod_version"] = _version.ModVersion,
            ["worldbox_version"] = _version.WorldBoxVersion,
            ["unity_version"] = _version.UnityVersion,
            ["assembly_csharp_sha256"] = _version.AssemblyCSharpSha256,
            ["commands"] = commands,
        };
        return new HttpResponse
        {
            Status = 200,
            StatusText = "OK",
            Body = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None)),
        };
    }

    private HttpResponse SuccessResponse(object? result)
    {
        var envelope = new SuccessEnvelope
        {
            Result = result,
            Tick = MainThreadDispatcher.LastTick,
        };
        return new HttpResponse
        {
            Status = 200,
            StatusText = "OK",
            Body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope, JsonSettings)),
        };
    }

    private static HttpResponse ErrorResponse(
        int status,
        string statusText,
        string code,
        string message,
        string? commandName = null,
        JObject? args = null,
        IReadOnlyList<string>? didYouMean = null,
        ExceptionInfo? exception = null
    )
    {
        var envelope = new ErrorEnvelope
        {
            Error = new ErrorDetail
            {
                Code = code,
                Message = message,
                Command = commandName,
                Args = args,
                DidYouMean = didYouMean,
                Exception = exception,
            },
        };
        return new HttpResponse
        {
            Status = status,
            StatusText = statusText,
            Body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope, JsonSettings)),
        };
    }

    // ──────────────────────────────────────────────────────────────────────
    // Wire output
    // ──────────────────────────────────────────────────────────────────────

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        HttpResponse response,
        CancellationToken cancellationToken
    )
    {
        var header =
            $"HTTP/1.1 {response.Status} {response.StatusText}\r\n"
            + $"Content-Type: {response.ContentType}\r\n"
            + $"Content-Length: {response.Body.Length}\r\n"
            + "Connection: close\r\n"
            + "Cache-Control: no-store\r\n"
            + "\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream
            .WriteAsync(headerBytes, 0, headerBytes.Length, cancellationToken)
            .ConfigureAwait(false);
        if (response.Body.Length > 0)
        {
            await stream
                .WriteAsync(response.Body, 0, response.Body.Length, cancellationToken)
                .ConfigureAwait(false);
        }
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Auth, extracts a bearer credential from either the new
    // 'Authorization: Bearer <token>' header (multi-agent) or the legacy
    // 'X-WB-Token: <token>' header (v0.1, v0.2 single-tenant clients).
    // Looks the token up in the AgentRegistry; returns a RequestContext on
    // success, null otherwise. Constant-time lookup happens inside the
    // registry, see AgentRegistry.FixedTimeEquals.
    // ──────────────────────────────────────────────────────────────────────

    private RequestContext? Authenticate(HttpRequest req)
    {
        var presented = ExtractToken(req);
        if (string.IsNullOrEmpty(presented))
        {
            return null;
        }
        var agent = _session.Agents.TryAuthenticate(presented);
        if (agent == null)
        {
            return null;
        }
        return _session.ContextFor(agent);
    }

    private static string? ExtractToken(HttpRequest req)
    {
        var auth = req.GetHeader("Authorization");
        if (!string.IsNullOrEmpty(auth))
        {
            const string prefix = "Bearer ";
            if (auth!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return auth.Substring(prefix.Length).Trim();
            }
        }
        var legacy = req.GetHeader("X-WB-Token");
        return string.IsNullOrEmpty(legacy) ? null : legacy;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // ignore
        }
        try
        {
            _listener.Stop();
        }
        catch
        {
            // ignore
        }
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignore
        }
        _cts.Dispose();
    }
}
