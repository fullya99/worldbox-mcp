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
└── scripts/        installers and dev bootstrap
```

| Entry point | File | Triggered by |
|---|---|---|
| Plugin load | `mod/src/WorldBoxBridge/Plugin.cs`, `Awake` | BepInEx chainloader at game start |
| HTTP request | `mod/src/WorldBoxBridge/Http/HttpBridge.cs` | any call to `127.0.0.1:8723` |
| Introspection | same file, `GET /capabilities` | served directly, not through a command |
| Server CLI | `server/src/worldbox_mcp/__main__.py`, `main` | `uvx worldbox-mcp` |

### Counting the tool surface

<!-- gen-docs:begin total -->29<!-- gen-docs:end total --> MCP tools, which is <!-- gen-docs:begin bridge-commands -->28<!-- gen-docs:end bridge-commands --> registered `ICommand` implementations plus the `/capabilities`
endpoint. File count and command count differ because `PauseCommand.cs` declares two commands,
`pause` and `resume`. [command-reference.md](command-reference.md) is the reference list, and
every stated count elsewhere should agree with it.

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
4. The bridge runs the per-command permission gate (`ctx.Require(Permission.X)`) and (in turn-based sessions) checks that the caller holds the current turn. Both fail fast before any game-state work.
5. Bridge enqueues an `Action` on the main-thread dispatcher with a `TaskCompletionSource`.
6. Next Unity frame: dispatcher pops the action, the command runs with `RequestContext` in scope (so it can fog-of-war-filter reads or scope writes to the caller's kingdom), sets the TCS result.
7. HTTP handler awaits the TCS, serializes the result, returns `200 OK`.
8. Python server returns the result to the MCP client.

For long-running commands the dispatcher enforces a 30-second timeout to keep the game from freezing if a reflection call goes pathological, see [protocol.md](protocol.md).

## Threading model summary

| Thread | Owns |
|---|---|
| .NET thread pool | HTTP socket I/O, JSON parsing, command queueing |
| Unity main thread | All game state reads/writes, all `MapBox`/`World`/`Actor` access |
| Logger | Thread-safe via `BepInEx.Logging.ManualLogSource` |
