# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This file is maintained automatically by [release-please](https://github.com/googleapis/release-please)
from Conventional Commits.

## [0.2.0](https://github.com/fullya99/worldbox-mcp/compare/v0.1.1...v0.2.0) (2026-05-16)


### Features

* **mod,server:** Add generate_world / save_world / load_world ([2694bc0](https://github.com/fullya99/worldbox-mcp/commit/2694bc0bf57813b204daee5f74fd3bf2a8656fb5))
* **mod,server:** Batch Phase 2/3 — spawn + paint_tile + 6 read commands ([cae7882](https://github.com/fullya99/worldbox-mcp/commit/cae788255604ea177fd28773a46e6e6d4d6c9633))
* **mod,server:** Control commands — pause, resume, set_speed ([aa276fc](https://github.com/fullya99/worldbox-mcp/commit/aa276fccd6d35487c34e4492201b6739ea1f3fcf))
* **mod,server:** Implement discovery primitives (list_tiles/actors/powers) ([a098056](https://github.com/fullya99/worldbox-mcp/commit/a098056bf3136fcb09eb2ec42ac4dd4d90db8fa8))
* **mod,server:** Invoke_power primitive (universal action via GodPower delegate) ([a47f8e3](https://github.com/fullya99/worldbox-mcp/commit/a47f8e33526557a547d2b21b34b5731219d5de44))
* **mod:** Scaffold BepInEx C# plugin ([ffb0278](https://github.com/fullya99/worldbox-mcp/commit/ffb02784d03b37fddaa803976aa7bbb5512b695f))
* **server:** Scaffold Python MCP server with auth-aware bridge client ([76d0c2f](https://github.com/fullya99/worldbox-mcp/commit/76d0c2fd605a1c57adaa1f28e1ab75071eae478d))


### Bug Fixes

* **ci:** Bump astral-sh/setup-uv from v4 (nonexistent) to v6 ([46fd1f2](https://github.com/fullya99/worldbox-mcp/commit/46fd1f28eae6f64e0d305106d76e68a4630be129))
* **ci:** Replace email-obfuscation artifact in pre-commit/action ref ([3544683](https://github.com/fullya99/worldbox-mcp/commit/35446832bb5fda4010531522834161b78b667cc1))
* **mod:** Inject dispatcher into PlayerLoop instead of MonoBehaviour.Update ([435583f](https://github.com/fullya99/worldbox-mcp/commit/435583f0d27a8b2e5d9a17f40402909d924aed8e))
* **mod:** List_kingdoms/list_cities + kingdoms_alive/cities_alive counters now report live entries ([98a8a1c](https://github.com/fullya99/worldbox-mcp/commit/98a8a1ccd7f254744698fe6237ea04aee15b5cbe))
* **mod:** Switch to TcpListener + IPAddress.Loopback, decouple from MonoBehaviour lifecycle ([c75d3b8](https://github.com/fullya99/worldbox-mcp/commit/c75d3b828bf3a860981bf866a813c4aba25ae063))


### Documentation

* Claude Code wiring recipe + dense god-mode prompt for live agent test ([b08a6ae](https://github.com/fullya99/worldbox-mcp/commit/b08a6ae7a916fdcb012407b56cffff4318daeb47))
* **readme:** Rewrite as proper public landing page ([5ee1ca0](https://github.com/fullya99/worldbox-mcp/commit/5ee1ca084f4721b3d15c8435cf8fbe698c55d894))
* **scenario:** Fix docstring path after move to examples/scenarios/ ([0bc6f0c](https://github.com/fullya99/worldbox-mcp/commit/0bc6f0caa508b3248a06655f3a5dfed9e32a2a83))

## [0.1.1] — 2026-05-16

### Fixed
- `worldbox_list_kingdoms` and `worldbox_list_cities` now return live entries instead of
  always an empty list. Root cause: the reflection helper looked for `getSimpleList()` on
  the manager, which only exists on `SimSystemManager<,>` (the actor side) — never on
  `MetaSystemManager<,>` (the kingdom / city side). Both manager hierarchies share a common
  `CoreSystemManager<,>` base that implements `IEnumerable<T>`, so the fix iterates via
  the C# interface instead of a specific method name.
- `worldbox_get_world_state.kingdoms_alive` / `cities_alive` now report the real counts
  (same root cause, same fix — replaced the manual list count with the `Count` property
  inherited from `CoreSystemManager`).

### Changed
- `docs/game-api-notes.md` updated with verified reflection paths for every command
  (spawn, paint_tile, invoke_power, pause/resume/set_speed, generate/save/load world,
  screenshot), the `WorldTimeScaleAsset` ids actually accepted (including the undocumented
  `x10`, `x15`, `x20`), and the `CoreSystemManager` iteration contract.
- `docs/command-reference.md` rewritten as a real reference (was a Phase-3 stub). Covers
  the 20 tools, their args, and the full error envelope.
- `docs/index.md` and `README.md` reconciled: tool count is **20** in both places (was
  inconsistently 19 and 20).
- `docs/install/claude-code.md`: simplified — `uvx worldbox-mcp` is now the primary path
  (v0.1.0 is published on PyPI as of this release cycle); local-clone path kept as a
  fallback for testing unreleased commits.
- `docs/compatibility.md` upgraded with a real entry: WorldBox 0.51.2 × mod 0.1.1 is
  marked ✅ validated end-to-end.

## [0.1.0] — 2026-05-16

### Added
- First public release. 19 mod commands surfaced as 20 MCP tools (+ `worldbox_capabilities`
  meta tool):
  - **Meta**: `health`, `capabilities`
  - **Discovery**: `list_tiles`, `list_actors`, `list_powers`
  - **Action**: `invoke_power`, `spawn`, `paint_tile`
  - **Read**: `get_world_state`, `get_tile`, `list_kingdoms`, `list_cities`, `query_actors`,
    `screenshot`
  - **Control**: `pause`, `resume`, `set_speed`, `generate_world`, `save_world`,
    `load_world`
- BepInEx 5.x C# plugin (`WorldBoxBridge`) with HTTP API on `127.0.0.1:8723` and per-install
  auth token.
- Python MCP server published on PyPI as `worldbox-mcp` — `uvx worldbox-mcp` for instant
  use from any MCP client.
- Universal reflection-based discovery: `AssetCatalog` enumerates any of the ~150 typed
  asset libraries on `AssetManager` via the uniform `AssetLibrary<T>` contract.
- `MainThreadDispatcher` injected into Unity's `PlayerLoop` Update phase (rather than a
  `MonoBehaviour.Update()` — that gets destroyed shortly after Awake on this game).
- Levenshtein-based `did_you_mean` suggestions on every `UNKNOWN_ASSET` error.
- End-to-end ecology demo at `examples/scenarios/ecology_smoke.py`.
- Per-client wiring recipes for Claude Code / OpenCode / Codex / Cursor / Continue.

### Known issues
- `list_kingdoms` / `list_cities` / `get_world_state.{kingdoms,cities}_alive` always return
  0 even when kingdoms exist — **fixed in 0.1.1**.
