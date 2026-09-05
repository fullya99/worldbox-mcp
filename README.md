# worldbox-mcp

> **Give your AI agent god-mode in [WorldBox](https://www.superworldbox.com/).**
> Spawn dragons. Burn continents. Run civilization speedruns. From a conversation.

[![CI](https://github.com/fullya99/worldbox-mcp/actions/workflows/ci.yml/badge.svg)](https://github.com/fullya99/worldbox-mcp/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Code style: ruff](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/astral-sh/ruff/main/assets/badge/v2.json)](https://github.com/astral-sh/ruff)
[![MCP](https://img.shields.io/badge/MCP-compatible-blue)](https://modelcontextprotocol.io)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4.23-orange)](https://bepinex.org/)

`worldbox-mcp` is a two-piece bridge that lets any [MCP](https://modelcontextprotocol.io)-compatible AI client (Claude Code, OpenCode, Codex, Cursor, Continue, ...) directly control the live game [WorldBox](https://www.superworldbox.com/).

**<!-- gen-docs:begin total -->29<!-- gen-docs:end total --> MCP tools** spanning discovery, action, observation, simulation control, multi-agent identity, and an inter-agent message bus. The agent observes the world, chooses what to do, acts on it, then observes the outcome, a full agentic loop running against an actual running game. Since v0.3, multiple AI agents can share the same world simultaneously with role-based permissions, fog-of-war, turn-taking, and direct messaging. See [`docs/multi-agent.md`](docs/multi-agent.md).

---

## What it does

```
> You: "Build me a Roman empire surrounded by hostile dragons, fast-forward
        50 years of evolution, then tell me who's still alive."

> Agent:
    pause()
    list_actors() → finds 'human', 'dragon'
    list_tiles()  → finds 'soil_high', 'sand'
    paint_tile(soil_high, x=80, y=80, radius=10)       # founder territory
    spawn(human, x=80, y=80, count=15, adult=True)     # the empire
    spawn(dragon, x=200, y=80, count=4, adult=True)    # the threat
    set_speed("x5")
    resume()
    # ... waits ~10 seconds for 50 years of simulation ...
    screenshot()
    query_actors(race="human", alive=True)
    list_kingdoms()
    → "After 50 years, the Romans built 2 cities. The dragons
       razed the western one but the eastern capital Aletalda
       still stands with 47 citizens."
```

The whole exchange is real, `examples/scenarios/ecology_smoke.py` runs a stripped-down version of it as a smoke test.

## How it works

```
┌─────────────┐  MCP stdio  ┌──────────────────┐  HTTP 127.0.0.1   ┌─────────────────────┐
│  AI client  ├────────────►│ worldbox-mcp     ├──────────────────►│ WorldBoxBridge      │
│ (any MCP)   │             │ (Python on PyPI) │                   │ BepInEx C# plugin   │
└─────────────┘             └──────────────────┘                   │ inside worldbox.exe │
                                                                   └──────────────────────┘
```

- **`WorldBoxBridge`**: a [BepInEx 5](https://bepinex.org/) plugin (C#) injected into WorldBox that exposes a local, auth-protected HTTP API to the game's internals.
- **`worldbox-mcp`**: a Python MCP server distributed via PyPI (`uvx worldbox-mcp`) that translates MCP tool calls into HTTP requests against the mod.

The mod uses **only reflection** to access game types (no static binding) so it survives WorldBox updates as long as core class names stay stable. All asset ids (tile types, actor races, powers, world speeds) are discovered at runtime from the running build, so the catalog is always correct.

## Quickstart

### 1. Install the in-game mod

Requires WorldBox installed (Steam) and **Experimental Mode** enabled in-game (Settings → Experimental Mode).

```powershell
# Windows
iex (irm https://raw.githubusercontent.com/fullya99/worldbox-mcp/main/scripts/install-mod.ps1)
```

```bash
# Linux / macOS
curl -fsSL https://raw.githubusercontent.com/fullya99/worldbox-mcp/main/scripts/install-mod.sh | bash
```

This downloads BepInEx + the latest `WorldBoxBridge.dll`, installs both into your WorldBox folder, and generates a per-install random auth token. Launch WorldBox once and look for this line in `BepInEx/LogOutput.log`:

```
[Info: WorldBoxBridge] listening on http://127.0.0.1:8723
```

### 2. Plug it into your AI client

The MCP server runs via `uvx` (zero install) thanks to the [uv](https://docs.astral.sh/uv/) tooling.

<details>
<summary><strong>Claude Code</strong>, one command</summary>

```bash
claude mcp add worldbox -- uvx worldbox-mcp
```
</details>

<details>
<summary><strong>OpenCode</strong></summary>

```jsonc
// ~/.config/opencode/config.json
{
  "mcp": {
    "worldbox": { "type": "local", "command": ["uvx", "worldbox-mcp"] }
  }
}
```
</details>

<details>
<summary><strong>Codex CLI</strong></summary>

```toml
# ~/.codex/config.toml
[mcp_servers.worldbox]
command = "uvx"
args = ["worldbox-mcp"]
```
</details>

<details>
<summary><strong>Cursor</strong></summary>

```json
// .cursor/mcp.json
{
  "mcpServers": {
    "worldbox": { "command": "uvx", "args": ["worldbox-mcp"] }
  }
}
```
</details>

<details>
<summary><strong>Continue</strong></summary>

```yaml
# ~/.continue/config.yaml
mcpServers:
  - name: worldbox
    command: uvx
    args: [worldbox-mcp]
```
</details>

Any other MCP-compatible client: see [`docs/install/manual.md`](docs/install/manual.md). The server also supports a `--http` flag for Streamable HTTP transport.

### 3. Try it

In your agent client:

```
> Call worldbox_health to verify the bridge is connected.
> Now list_actors and pick a few interesting races, then build me
  a small ecology experiment near the center of the map.
```

Or paste the full god-mode ecology prompt from [`examples/prompts/godmode-ecology.md`](examples/prompts/godmode-ecology.md) to exercise every tool category in one go.


## The <!-- gen-docs:begin total -->29<!-- gen-docs:end total --> tools

| Category | Tools | What for |
|---|---|---|
| **Meta** | `worldbox_health`, `worldbox_capabilities`, `worldbox_whoami`, `worldbox_session_info`, `worldbox_turn_advance`, `worldbox_objective_status` | Liveness, introspection, multi-agent identity, turn rotation, scoreboard |
| **Discovery** | `worldbox_list_tiles`, `worldbox_list_actors`, `worldbox_list_powers`, `worldbox_list_speeds` | Enumerate asset ids the running game exposes, ~20 tiles, ~320 actors, ~340 powers, 10 speeds on stock 0.51.x |
| **Action** | `worldbox_invoke_power`, `worldbox_spawn`, `worldbox_paint_tile` | Modify the world: trigger powers, spawn creatures, paint terrain |
| **Read** | `worldbox_get_world_state`, `worldbox_get_tile`, `worldbox_list_kingdoms`, `worldbox_list_cities`, `worldbox_query_actors`, `worldbox_get_ui_state`, `worldbox_screenshot` | Observe before deciding (fog-of-war filtered in multi-agent mode) |
| **Control** | `worldbox_pause`, `worldbox_resume`, `worldbox_dismiss_window`, `worldbox_set_speed`, `worldbox_generate_world`, `worldbox_save_world`, `worldbox_load_world` | Simulation flow + world lifecycle |
| **Bus** | `worldbox_send_message`, `worldbox_recv_messages` | Inter-agent messaging (1-to-1 or broadcast) for multi-agent sessions |

Each tool has rich docstrings the agent reads at startup to decide when to call what. Asset ids that don't exist trigger `UNKNOWN_ASSET` errors with **Levenshtein `did_you_mean` suggestions** drawn from the live catalog.

Full reference: **[docs/command-reference.md](docs/command-reference.md)**.

## Design principles

- **Survives game updates.** Every game type/method is resolved by reflection with caching and explicit warnings if a symbol disappears, a renamed class disables only the affected command, not the whole bridge. The mod also reports the WorldBox version and the SHA256 of `Assembly-CSharp.dll` in every `/health` so bug reports are traceable.
- **100% coverage through 3 primitives.** `invoke_power` alone covers ~270 actions because the in-game GodPower model unifies spawns, disasters, and toggles. `spawn` reaches the actor catalog directly for non-power creatures. `paint_tile` handles terrain. The discovery tools tell the agent what's valid right now in this build.
- **Local-only by design.** The HTTP bridge binds **only** to `127.0.0.1`, `0.0.0.0` is refused at startup. Auth is a per-install random token in `BepInEx/config/WorldBoxBridge.cfg`, or one bearer per agent in multi-agent mode via `BepInEx/config/WorldBoxBridge.agents.json`. Constant-time token comparison. No telemetry, no remote endpoints.
- **Multi-agent ready (v0.3).** N AIs on one world via a session layer: per-agent role (god / faction_player / observer / narrator), permission gates, fog-of-war scoping, turn-based mode, bounded inter-agent message bus, scoreboard primitive. Four ready-to-customize presets (PvP / coop / hierarchical / sandbox) in `examples/scenarios/multi-agent/`.
- **Production-grade.** Typed (mypy strict + C# nullable enabled), tested (129 unit + integration cases across xUnit and pytest), formatted (ruff + csharpier), signed releases via `release-please`, automated CI/CD across Win/Linux/Mac × Py 3.11 to 3.13.

## Compatibility

| WorldBox version | Unity | Mod version | Status |
|---|---|---|---|
| 0.51.2 | 2022.3.60f1 (Mono) | 0.3.x | ✅ multi-agent layer end-to-end (PvP smoke, fog-of-war, turn-based, bus) |
| 0.51.2 | 2022.3.60f1 (Mono) | 0.2.x | ✅ generate/save/load world |
| 0.51.2 | 2022.3.60f1 (Mono) | 0.1.x | ✅ baseline 20-tool surface |

A daily CI cron monitors WorldBox releases on Steam and opens a tracking issue when a new version ships, so update breakage gets caught the day-of. See [`docs/compatibility.md`](docs/compatibility.md).

## Architecture deep-dive

- [`docs/architecture.md`](docs/architecture.md), component layout, thread model, lifecycle
- [`docs/multi-agent.md`](docs/multi-agent.md), session layer: agents, permissions, fog-of-war, turns, message bus, objectives
- [`docs/protocol.md`](docs/protocol.md), exact HTTP/JSON contract between server and mod
- [`docs/game-api-notes.md`](docs/game-api-notes.md), reflection paths into WorldBox's internals (lives alongside the code, updated per game version)
- [`docs/development.md`](docs/development.md), local dev setup + testing
- [`examples/scenarios/ecology_smoke.py`](examples/scenarios/ecology_smoke.py), single-agent end-to-end demo (legacy mode)
- [`examples/scenarios/multi-agent/pvp_smoke.py`](examples/scenarios/multi-agent/pvp_smoke.py), multi-agent PvP end-to-end demo

## Contributing

Issues, PRs, new commands welcome. See [CONTRIBUTING.md](CONTRIBUTING.md). The shortest path to a useful contribution: pick a power id from `list_powers` that has interesting arguments (like a disaster with a radius), and add a typed convenience wrapper around `invoke_power` for it.

## License

[MIT](LICENSE) © 2026 fullya99

> _worldbox-mcp is an unofficial community project and is not affiliated with or endorsed by the WorldBox developers._
