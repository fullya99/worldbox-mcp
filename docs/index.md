---
title: worldbox-mcp
---

# worldbox-mcp

> **Give your AI agent god-mode in [WorldBox](https://www.superworldbox.com/).** Spawn dragons. Burn continents. Run civilization speedruns. From a conversation.

`worldbox-mcp` is an open-source bridge that lets any MCP-compatible AI client (**Claude Code, OpenCode, Codex, Cursor, Continue, and more**) directly control the live game.

It ships as two open-source components:

1. **`WorldBoxBridge`**, a [BepInEx](https://bepinex.org/) plugin (C#) injected into the running game that exposes an authenticated HTTP API to the game's internals.
2. **`worldbox-mcp`**, a Python MCP server distributed via PyPI (`uvx worldbox-mcp`) that translates MCP tool calls into HTTP requests.

## Quick links

- 🚀 **[Install](install/index.md)**, pick your AI client
- 🏗 **[Architecture](architecture.md)**, how the two halves talk to each other
- 📚 **[Command reference](command-reference.md)**, every tool, auto-generated
- 👥 **[Multi-agent sessions](multi-agent.md)**, N AIs on one world (v0.3+)
- 🧩 **[Compatibility](compatibility.md)**, WorldBox × mod version matrix
- 🤝 **[Contributing](contributing.md)**, code, docs, issues

## <!-- gen-docs:begin total-words -->Twenty-nine<!-- gen-docs:end total-words --> tools across six categories

| Category | Tools |
|---|---|
| Meta | `worldbox_health`, `worldbox_capabilities`, `worldbox_whoami`, `worldbox_session_info`, `worldbox_turn_advance`, `worldbox_objective_status` |
| Discovery | `worldbox_list_tiles` (20), `worldbox_list_actors` (322), `worldbox_list_powers` (339), `worldbox_list_speeds` (10) |
| Action | `worldbox_invoke_power`, `worldbox_spawn`, `worldbox_paint_tile` |
| Read | `worldbox_get_world_state`, `worldbox_get_tile`, `worldbox_list_kingdoms`, `worldbox_list_cities`, `worldbox_query_actors`, `worldbox_get_ui_state`, `worldbox_screenshot` |
| Control | `worldbox_pause`, `worldbox_resume`, `worldbox_dismiss_window`, `worldbox_set_speed`, `worldbox_generate_world`, `worldbox_save_world`, `worldbox_load_world` |
| Bus | `worldbox_send_message`, `worldbox_recv_messages` |

See [command-reference.md](command-reference.md) for arguments + return shapes and
[multi-agent.md](multi-agent.md) for putting N AIs on the same world.

## Design principles

- **100% game coverage** through three generic action primitives (`paint_tile`, `spawn`, `invoke_power`) backed by the game's own asset registry, see [protocol.md](protocol.md).
- **Survives game updates**: zero static binding to WorldBox internals; everything resolves through cached reflection with explicit logging when a symbol disappears.
- **Local-only by design**: HTTP bound to `127.0.0.1`, per-install random auth token, no telemetry.
- **Production-grade**: typed, tested, signed releases, automated CI/CD.

---

> _worldbox-mcp is an unofficial community project and is not affiliated with or endorsed by the WorldBox developers._
