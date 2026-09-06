# Architecture

`worldbox-mcp` is split across three address spaces that communicate through two well-defined boundaries.

```
┌─────────────┐  MCP        ┌──────────────────┐  HTTP loopback   ┌──────────────────────┐
│  AI client  ├────────────►│ worldbox-mcp     ├─────────────────►│ WorldBoxBridge       │
│   (any)     │  stdio/HTTP │ (Python, PyPI)   │  127.0.0.1:8723  │ BepInEx C# plugin    │
└─────────────┘             └──────────────────┘                  │ inside worldbox.exe  │
                                                                  │                      │
                                                                  │  ┌────────────────┐  │
                                                                  │  │ TCP listener   │  │
                                                                  │  │ + hand-rolled  │  │
                                                                  │  │ HTTP/1.1       │  │
                                                                  │  │ Auth + routing │  │
                                                                  │  └────────┬───────┘  │
                                                                  │           │          │
                                                                  │     Session layer    │
                                                                  │  (agents, perms,     │
                                                                  │   message bus,       │
                                                                  │   turn order)        │
                                                                  │           │          │
                                                                  │  ┌────────▼───────┐  │
                                                                  │  │ Main thread    │  │
                                                                  │  │ dispatcher     │  │
                                                                  │  │ (PlayerLoop)   │  │
                                                                  │  └────────┬───────┘  │
                                                                  │           │          │
                                                                  │  ┌────────▼───────┐  │
                                                                  │  │ Command via    │  │
                                                                  │  │ reflection on  │  │
                                                                  │  │ Assembly-CSharp│  │
                                                                  │  └────────────────┘  │
                                                                  └──────────────────────┘
```

## Where things live

```
worldbox-mcp/
├── mod/            BepInEx 5 plugin, net462, injected into the game process
│   ├── src/WorldBoxBridge/
│   │   ├── Plugin.cs          entry point, wires everything together
│   │   ├── Http/              TcpListener, HTTP/1.1 parser, auth, error envelope, turn gating
│   │   ├── Commands/          one directory per category, one file per command
│   │   ├── Reflection/        cached, fail-soft access to game internals
│   │   ├── Session/           agents, permissions, fog of war, turn order, message bus
│   │   └── Threading/         main-thread dispatcher injected into Unity's PlayerLoop
│   └── tests/                 xUnit, net8, linked sources so it builds without Unity
├── server/         Python MCP server, published to PyPI
│   └── src/worldbox_mcp/
│       ├── server.py          server factory
│       ├── client.py          httpx wrapper around the bridge
│       └── tools/             one module per command category
├── docs/           this site
├── examples/       client configs, demo prompts, runnable scenarios
└── scripts/        installers, dev bootstrap, and the docs generator
```

| Entry point | File | Triggered by |
|---|---|---|
| Plugin load | `mod/src/WorldBoxBridge/Plugin.cs`, `Awake` | BepInEx chainloader at game start |
| HTTP request | `mod/src/WorldBoxBridge/Http/HttpBridge.cs` | any call to `127.0.0.1:8723` |
| Introspection | same file, `GET /capabilities` | served directly, not through a command |
| Server CLI | `server/src/worldbox_mcp/__main__.py`, `main` | `uvx worldbox-mcp` |

### Counting the tool surface

<!-- gen-docs:begin total -->29<!-- gen-docs:end total --> MCP tools, which is
<!-- gen-docs:begin bridge-commands -->28<!-- gen-docs:end bridge-commands --> registered
`ICommand` implementations plus the `/capabilities` endpoint. File count and command count
differ because `PauseCommand.cs` declares two commands, `pause` and `resume`.

Both numbers above are generated. `scripts/gen-docs.py` reads them from the registered tools
and from the C# sources, rewrites them wherever they are stated, and fails CI when a count or
an inventory table drifts. Never edit one by hand, see [development.md](development.md).

### Versions that are pinned on purpose

| Package | Pinned in | Reason |
|---|---|---|
| `UnityEngine.Modules` | `mod/Directory.Packages.props` | Must match the engine the game ships. Comes from the BepInEx feed, mapped by exact id in `mod/NuGet.config` because nuget.org's copy stops at 2021.3 |
| `Newtonsoft.Json` | same | The game's bundled copy wins at runtime, so a newer nuget throws `MissingMethodException` |
| `Microsoft.NETFramework.ReferenceAssemblies` | same | Referenced explicitly so the restore graph is identical on Windows, Linux and macOS |
| `BepInEx.Core` | same | Manual bumps only. Pulls HarmonyX transitively at the version the game expects |
| csharpier | `.github/workflows/ci.yml` | 1.x changed the CLI and the XML formatting defaults |

`packages.lock.json` is committed for both mod projects, so `dotnet restore --locked-mode` really
does pin content hashes. See [development.md](development.md) for how to regenerate it.

## Why this layout

| Boundary | Why a separate process |
|---|---|
| AI client ↔ MCP server | The MCP spec dictates this; lets any client reuse the same server. |
| MCP server ↔ Mod | The mod must live inside `worldbox.exe` to access game internals. The MCP server stays a normal Python process, easy to ship via PyPI, runs on any OS, no Unity baggage. |

## Component responsibilities

### `worldbox-mcp` (Python server)

- Speaks the MCP wire protocol (stdio + Streamable HTTP).
- Exposes a curated tool surface to AI clients (see [command-reference.md](command-reference.md)).
- Owns the contract: input validation via Pydantic, error mapping, retries on transient HTTP failures.
- Auto-discovers the mod's auth token by reading `<worldbox>/BepInEx/config/WorldBoxBridge.cfg`.
- **Does not** know anything about WorldBox internals. It is a thin, typed façade over the HTTP bridge.

### `WorldBoxBridge` (BepInEx C# plugin)

- Hosts an HTTP/1.1 server built on `System.Net.Sockets.TcpListener` + a hand-rolled
  request parser, bound to `127.0.0.1`. Authenticated with a bearer token (one shared
  secret in legacy single-tenant mode; one per agent in multi-agent mode). We use
  `TcpListener` rather than `System.Net.HttpListener` because the latter silently fails
  to bind under Unity 2022.3 Mono, see gotcha 1 in [game-api-notes.md](game-api-notes.md).
- Holds a **session layer** (v0.3+) on top of HTTP routing: agent registry (token → role
  / faction / permissions), in-memory message bus with per-agent inboxes, optional
  turn-order. Loaded from `BepInEx/config/WorldBoxBridge.agents.json` at startup; falls
  back to legacy single-token mode if the file is absent. See [multi-agent.md](multi-agent.md).
- Dispatches incoming JSON commands onto Unity's main thread via a `ConcurrentQueue<Action>` drained from a delegate injected into Unity's `PlayerLoop` (not a `MonoBehaviour`). On WorldBox 0.51.2, BepInEx-created `MonoBehaviour` GameObjects get destroyed shortly after Awake, the PlayerLoop hook is part of the engine's tick table and survives that.
- Resolves all WorldBox types via cached reflection (never `using WorldBox.*` directly) so the mod survives game updates as long as core types keep their names.
- Maps every command to game APIs that live inside `Assembly-CSharp.dll`.

## Critical invariants

1. **Unity API calls happen on the main thread.** Period. The dispatcher is the only legal way for HTTP handlers to touch the game.
2. **Auth is checked before any work.** The HTTP middleware short-circuits on a bad token before queueing anything onto the main thread.
3. **Loopback only.** `HttpListener` bound to `127.0.0.1`. Refused at startup if config tries `0.0.0.0`.
4. **No static binding to game types.** A reflection lookup that fails logs a warning and disables only the affected command, the rest of the bridge keeps working.

## Data flow for a tool call

1. AI client emits `tools/call` over MCP.
2. Python server validates args with Pydantic, builds a JSON command envelope, sends `POST /cmd` with `Authorization: Bearer <token>` (the legacy `X-WB-Token` header is still accepted).
3. Mod's HTTP handler verifies the bearer against the `AgentRegistry`, resolves it into a `RequestContext` (agent id, role, kingdom claim, permissions, scenario flags), then parses the JSON body.
4. The bridge takes an admission slot, waiting up to two seconds for one and answering `503 BUSY` if none frees up. The cap is `max_concurrent_requests` in `WorldBoxBridge.cfg`, default 8. It sits after authentication so unauthenticated traffic cannot spend the slots, and before everything below because past this point a request can hold a whole save in memory.
5. The bridge runs the per-command permission gate (`ctx.Require(Permission.X)`) and (in turn-based sessions) checks that the caller holds the current turn. Both fail before any game-state work, but not before the slot: the turn gate runs just inside the admission gate, and `ctx.Require` is the first statement of the command itself, so it runs a frame later still. A refused caller therefore occupies a slot while being refused. That bounds the damage an authenticated agent with no permissions can do to a queue, and it does not remove it, see the entry in [TODOS.md](../TODOS.md).
6. Bridge enqueues an `Action` on the main-thread dispatcher with a `TaskCompletionSource`. A command that reports `RequiresMainThread == false` skips this step and gets handed to a pool thread with `Task.Run`, marshalling only the calls that need a frame. `load_world` is the case to copy: it resolves the path and reads the file on the pool thread and queues nothing but the `loadMapFromBytes` call.
7. Next Unity frame: dispatcher pops the action, the command runs with `RequestContext` in scope, so it knows who is calling and can fog-of-war-filter what it returns, and sets the TCS result. It cannot scope a write to the caller's kingdom: nothing in the bridge does, see [multi-agent.md](multi-agent.md#a-kingdom-claim-scopes-reads-not-writes).
8. HTTP handler awaits the command's *task* off the main thread, under a 60-second backstop, so a command that only finishes on a later frame (`invoke_power` with `pulses`) does not deadlock the frame it started on. The backstop covers both branches of step 6, which is why step 6 uses `Task.Run` rather than calling the command inline: a command that reports `false` runs synchronously up to its first `await`, and `load_world` reads the whole save in that stretch, so an inline call would park the handler before the backstop existed and never give the slot back. It serializes the result and returns `200 OK`, releasing the admission slot on the way out.
9. Python server returns the result to the MCP client.

The dispatcher's 30-second deadline is a **queueing** deadline, not a watchdog. `MainThreadDispatcher.Tick` compares it against the clock just before it calls an action, so an action that waited too long for a frame is dropped with `MAIN_THREAD_TIMEOUT`, and an action that has started runs to completion whatever it does. Nothing on the main thread can be interrupted. That is why blocking I/O must never be queued: see the `RequiresMainThread` remarks on `LoadWorldCommand`, and gotcha 11 in [game-api-notes.md](game-api-notes.md).

Two counted bounds keep a burst of callers from turning into unbounded work, and they are deliberately separate. The admission gate in `HttpBridge.ExecuteCommandAsync` caps how many commands execute at once, which is what stops N parallel `load_world` calls from allocating N saves. The dispatcher caps registered per-frame jobs at 32, the same number of queued actions it drains per frame, which is what stops N `invoke_power` runs with `pulses` from costing N delegate invocations every frame for 25 seconds each. The second is not merely a consequence of the first: the handler's 60-second backstop can abandon a job that outlives it, and a bound that holds only while two constants in different files keep their current relationship is not a bound. Both refusals surface as `503 BUSY`. `ConcurrencyGate` is the shared primitive, and it is free of Unity and BepInEx types so the test project links it directly.

That two-await shape has a consequence worth stating before someone hits it: `RequiresMainThread == true` puts the *first* thread of the command on the main thread, not the whole method. An `await` inside such a command does not land where you might guess. Unity installs `UnityEngine.UnitySynchronizationContext` on the main thread, it is in the UnityEngine.Modules reference assembly the mod compiles against, and the engine pumps it from the player loop, so the continuation returns to the main thread rather than to a pool thread. What it escapes is the dispatcher: no queueing deadline, no `maxPerFrame` bound, and no defined order against actions still queued. The trap is that `ConfigureAwait(false)` or `Task.Run`, reached for to get back onto the main thread, is what leaves it. Write a main-thread command as one synchronous body returning a task, and push anything that has to wait through `MainThreadDispatcher.RunPerFrameOnMainThreadAsync`. No command that reports `true` awaits today; `load_world` awaits and reports `false`, which is safe because it awaits from the pool thread. The remarks on `ICommand.RequiresMainThread` carry the same warning where an author will actually be reading.

## Threading model summary

| Thread | Owns |
|---|---|
| .NET thread pool | HTTP socket I/O, JSON parsing, command queueing, and everything a command does that does not touch a Unity API, which for `load_world` includes resolving the path and reading the save |
| Unity main thread | All game state reads/writes, all `MapBox`/`World`/`Actor` access |
| Logger | Thread-safe via `BepInEx.Logging.ManualLogSource` |
