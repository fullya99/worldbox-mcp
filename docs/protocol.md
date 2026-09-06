# Protocol specification

The mod exposes a minimal HTTP/JSON API on `http://127.0.0.1:8723` (default port; configurable). Stable across the `0.x` series.

## Endpoints

| Method + path | Purpose |
|---|---|
| `GET /health` | Liveness + version metadata. Auth required. |
| `POST /cmd` | Execute a named command. Auth required. |
| `GET /capabilities` | Introspect registered commands. Auth required. |

## Authentication

Every request must present a bearer credential, via **either** of two headers:

```
Authorization: Bearer <token>       (preferred, v0.3+)
X-WB-Token: <token>                 (legacy single-tenant, v0.1 / v0.2)
```

The bridge tries `Authorization: Bearer` first and falls back to `X-WB-Token`. Mixing the
two in different requests is fine, the C# server treats them as equivalent.

### Legacy single-token mode (default)

If no `agents.json` is present at `<worldbox>/BepInEx/config/WorldBoxBridge.agents.json`,
the bridge boots with one synthetic agent named `"legacy"` (role `God`, full permissions).
Its credential is the random token generated on first launch and stored at
`<worldbox>/BepInEx/config/WorldBoxBridge.cfg`.

### Multi-agent mode (v0.3+)

When `agents.json` exists, every entry registers a distinct bearer. Each authenticated
request inherits the agent's `role`, optional `kingdom_claim`, permission bitmask, and
inbox. See [multi-agent.md](multi-agent.md) for the full schema and four scenario presets
(PvP / coop / hierarchical / sandbox).

Requests without a valid token return `401 Unauthorized` immediately, before any work is
queued.

## `GET /health`

Returns a small JSON object useful for connection checks.

```http
GET /health HTTP/1.1
Authorization: Bearer <token>
```

```json
{
  "ok": true,
  "mod_version": "0.3.0",
  "worldbox_version": "0.x.x",
  "unity_version": "2022.3.60f1",
  "assembly_csharp_sha256": "…",
  "tick": 12345,
  "enabled": true,
  "multi_agent": false,
  "scenario": "sandbox",
  "agent_count": 1
}
```

`multi_agent`, `scenario`, `agent_count` reflect the active session. With no `agents.json` deployed the bridge runs in legacy single-tenant mode (`multi_agent: false`, scenario `sandbox`, one synthetic `"legacy"` god agent).

## `POST /cmd`

```http
POST /cmd HTTP/1.1
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "<command-name>",
  "args": { "<arg>": <value>, ... }
}
```

### Success response

```json
{
  "ok": true,
  "result": { /* command-specific */ },
  "tick": 12345
}
```

### Error response

```json
{
  "ok": false,
  "error": {
    "code": "GAME_REJECTED",
    "message": "spawn 'dragon_red' at (12, 8) failed: tile is water and actor.water_walking = false",
    "command": "spawn",
    "args": { "entity_id": "dragon_red", "x": 12, "y": 8 },
    "exception": {
      "type": "System.InvalidOperationException",
      "message": "Cannot place water-incompatible actor on water tile",
      "stack_top": "WorldBoxBridge.Commands.Action.SpawnCommand.Execute (line 47)"
    }
  }
}
```

### Error codes

| Code | HTTP | Meaning |
|---|---|---|
| `UNKNOWN_COMMAND` | 404 | The `name` field doesn't match a registered command. |
| `BAD_ARGS` | 400 | Args fail JSON-schema validation, or a command rejected a value: a missing coordinate, a count out of range, a save path that does not resolve. Commands raise it explicitly rather than letting an exception escape. |
| `UNKNOWN_ASSET` | 400 | An asset id (`tile_id` / `entity_id` / `power_id`) doesn't exist in this WorldBox build. Response includes `did_you_mean: [...]` (Levenshtein top-5). |
| `OUT_OF_BOUNDS` | 400 | Coordinates are outside the current map. |
| `GAME_REJECTED` | 422 | The game accepted the dispatch but the action was logically refused. |
| `GAME_CRASH` | 500 | An exception bubbled up from `Assembly-CSharp` or from the bridge itself. Full type + message + stack top in response. Argument validation never lands here, so a 500 means something actually broke. |
| `MAIN_THREAD_TIMEOUT` | 504 | Command waited more than 30s for a Unity frame and was dropped before it ran. The deadline is checked before the action starts, so it never abandons work already in progress. |
| `UNAUTHORIZED` | 401 | Missing or wrong bearer credential. |
| `DISABLED` | 503 | `enabled = false` in `WorldBoxBridge.cfg`. The kill-switch is active. |
| `BUSY` | 503 | The bridge already runs as many commands at once as it admits, or the dispatcher's per-frame job registry is full. Nothing broke, and the call is safe to repeat. Refused at admission it changed nothing at all; refused by the job registry, an `invoke_power` carrying a `radius` has already cloned its brush asset, which is idempotent by name. See [Concurrency limit](#concurrency-limit). |
| `PERMISSION_DENIED` _(v0.3+)_ | 403 | The agent's role lacks the permission this command needs. |
| `FACTION_SCOPE_VIOLATION` _(v0.3+)_ | 403 | Reserved, currently never raised. A kingdom claim scopes reads, not writes, see [multi-agent.md](multi-agent.md#a-kingdom-claim-scopes-reads-not-writes). |
| `TURN_NOT_YOURS` _(v0.3+)_ | 409 | Turn-based mode is active and another agent holds the current slot. |

## `GET /capabilities`

Returns the list of registered commands with their JSON Schemas. The Python server consumes this on startup to construct its MCP `tools/list` response.

```json
{
  "mod_version": "0.3.0",
  "worldbox_version": "0.x.x",
  "unity_version": "2022.3.60f1",
  "assembly_csharp_sha256": "…",
  "commands": [
    {
      "name": "spawn",
      "category": "action",
      "description": "Spawn one or more actors at (x, y).",
      "requires_main_thread": true,
      "schema": { "$schema": "https://json-schema.org/draft/2020-12/schema", ... }
    },
    …
  ]
}
```

## Kill-switch

Edit `<worldbox>/BepInEx/config/WorldBoxBridge.cfg` and set `enabled = false`. The mod hot-reloads the file every 2 seconds and stops accepting commands. New requests get `503 DISABLED`. Set back to `true` to resume, no restart required.

## Concurrency limit

The bridge runs a bounded number of commands at once. A request that arrives with every slot
taken waits two seconds for one to free up, then gets `503 BUSY` rather than joining a queue with
no end: a command in flight can hold an entire save file in memory, and `invoke_power` with
`pulses` keeps its slot for up to 25 seconds.

```ini
[Bridge]
max_concurrent_requests = 8
```

Accepted values run from 1 to 32. Unlike `enabled`, this one is read once at startup, so a change
needs a game restart. The two-second wait is arithmetic: the Python client allows 35s per call and
the dispatcher's queueing deadline is 30s, so a longer wait would push a genuine
`MAIN_THREAD_TIMEOUT` past the client's own deadline and the caller would be told the bridge was
unreachable instead of being shown the error it was sent.

A second bound sits behind it, on the dispatcher rather than on the socket: at most 32 per-frame
jobs may be registered at once, the same number of queued actions the dispatcher drains per
frame. Only `invoke_power` with `pulses` registers one today. Filling that registry also answers
`503 BUSY`, with a message that names the registry rather than the request cap. That is also why
`max_concurrent_requests` stops at 32, so raising it never carries a caller past a second limit
no configuration reaches.

Authentication runs before admission, so a flood of bad tokens cannot spend the slots, and
`GET /capabilities` is never gated because it reads no game state. `GET /health` is gated like
any other command, because it is one: a saturated bridge answering `BUSY` to a connection check
is reporting something true about itself.

## Startup window

WorldBox shows a `welcome` window after every launch, and any open window freezes the
simulation (`get_world_state.paused` reports `true` even though nobody paused it). The bridge
sets the game's own `Config.disable_startup_window` flag at plugin start so that window never
appears; control it with the `[Game]` section of `WorldBoxBridge.cfg`:

```ini
[Game]
suppress_startup_window = true
```

Set it to `false` for the vanilla startup screen. `get_ui_state` reports whether a window is
open and which, and `dismiss_window` closes it either way.

## Stability promise

- Endpoint paths, error codes, and the JSON envelope shape are part of the `0.x` SemVer contract.
- Command **inputs and outputs** are versioned per command via `capabilities()`. Adding a new optional input field is non-breaking; removing or renaming a field is a major bump.
- New commands may be added at any minor release.
- `did_you_mean` suggestions and exception detail formatting are best-effort and may evolve.
