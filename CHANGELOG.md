# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This file is maintained automatically by [release-please](https://github.com/googleapis/release-please)
from Conventional Commits.

## [0.3.3](https://github.com/fullya99/worldbox-mcp/compare/v0.3.2...v0.3.3) (2026-05-17)


### Documentation

* Revert site URLs back to fullya99.github.io (custom CNAME was dead) ([184c943](https://github.com/fullya99/worldbox-mcp/commit/184c94385dfdebd77ab872baacfc1450a21513b7))

## [0.3.2](https://github.com/fullya99/worldbox-mcp/compare/v0.3.1...v0.3.2) (2026-05-17)


### Documentation

* Sync CLAUDE.md + project URLs to post-v0.3.1 state ([d3d6ac8](https://github.com/fullya99/worldbox-mcp/commit/d3d6ac807580682624c5247a45b6ba357f8de568))

## [0.3.1](https://github.com/fullya99/worldbox-mcp/compare/v0.3.0...v0.3.1) (2026-05-17)


### Dependencies

* Bump Microsoft.NET.Test.Sdk from 17.11.1 to 18.5.1 ([#8](https://github.com/fullya99/worldbox-mcp/issues/8)) ([9483cbd](https://github.com/fullya99/worldbox-mcp/commit/9483cbd3789492e486200f1c1657983088a03600))

## [0.3.0](https://github.com/fullya99/worldbox-mcp/compare/v0.2.0...v0.3.0) (2026-05-17)


### Features

* **mod+server+examples:** Objectives, scoreboard, scenario presets, e2e smoke (Phase 6) ([9d70fa8](https://github.com/fullya99/worldbox-mcp/commit/9d70fa8bec48e07f183df091db37430b5c29443b))
* **mod+server:** In-memory MessageBus + send/recv tools (Phase 5) ([29ce923](https://github.com/fullya99/worldbox-mcp/commit/29ce9233fb913e6443ded0f532e5f4bcc5288977))
* **mod+server:** Turn-based opt-in + TurnAdvanceCommand (Phase 4) ([dd3350a](https://github.com/fullya99/worldbox-mcp/commit/dd3350aec6363132f4b8bc5940b0b471b627f261))
* **mod:** Agents.json loader + whoami / session_info commands (Phase 2 C# side) ([6bd9296](https://github.com/fullya99/worldbox-mcp/commit/6bd92961b081d49c60fced2ff5df06f270779507))
* **mod:** Introduce Session/Agent identity plumbing (Phase 1 of multi-agent v0.3) ([c6a83ae](https://github.com/fullya99/worldbox-mcp/commit/c6a83ae87aff23d838a22ecbb82b0d0ed11e2531))
* **mod:** Split AdvanceTime permission so FactionPlayers can fast-forward ([3077e8b](https://github.com/fullya99/worldbox-mcp/commit/3077e8ba4afbe1e5c85414ad20ad022200ebce53))
* **mod:** Wire permissions + faction binding + fog-of-war (Phase 3) ([88083ff](https://github.com/fullya99/worldbox-mcp/commit/88083ff9e9205ea156daca2927afd2d020497aad))
* **server:** Switch to Authorization: Bearer + add whoami / session_info tools (Phase 2 Python) ([c1546fd](https://github.com/fullya99/worldbox-mcp/commit/c1546fda8559c5298a6795b7d4a990bcc4ac5441))


### Bug Fixes

* **ci:** Auto-enable GitHub Pages on first docs deploy ([3a8060d](https://github.com/fullya99/worldbox-mcp/commit/3a8060d82ca4d7a2e8110f568f372156d6e66db5))
* **ci:** Exclude github issue forms from check-yaml + drop csharpier from pre-commit + simplify MCP self-check ([8546502](https://github.com/fullya99/worldbox-mcp/commit/8546502595aba1597f30d4fa5113a7662e65522c))
* **ci:** Pass ruff/mypy/pre-commit on server + tolerate mod-build limitation ([dba154b](https://github.com/fullya99/worldbox-mcp/commit/dba154b102698a9ec2b5bc4b9fdf61c26a85596a))
* **ci:** Unstick csharpier lint + mkdocs strict build (both pre-existing breakage) ([6bf983a](https://github.com/fullya99/worldbox-mcp/commit/6bf983a9ea62097560a29bf9930463b237312cb6))


### Dependencies

* Bump FluentAssertions from 6.12.1 to 6.12.2 ([#13](https://github.com/fullya99/worldbox-mcp/issues/13)) ([7fbf32e](https://github.com/fullya99/worldbox-mcp/commit/7fbf32e1e9985a786dc5d711bfaf3f8de77a8f98))
* Bump HarmonyX from 2.10.2 to 2.16.1 ([#7](https://github.com/fullya99/worldbox-mcp/issues/7)) ([506bc65](https://github.com/fullya99/worldbox-mcp/commit/506bc6599895f458490aa363d7a289bceba3ea8d))
* Bump Newtonsoft.Json from 13.0.3 to 13.0.4 ([#9](https://github.com/fullya99/worldbox-mcp/issues/9)) ([5cf0bdd](https://github.com/fullya99/worldbox-mcp/commit/5cf0bddd2256cac3c680e58e21189c94ebbc4373))
* Bump xunit from 2.9.2 to 2.9.3 ([#10](https://github.com/fullya99/worldbox-mcp/issues/10)) ([562aea8](https://github.com/fullya99/worldbox-mcp/commit/562aea844ff5357e07724cba165d18075112eb27))


### Documentation

* Add CLAUDE.md (auto-loaded context for Claude Code sessions) + cross-ref from CONTRIBUTING ([acfcdb1](https://github.com/fullya99/worldbox-mcp/commit/acfcdb1f40e319be45101139b4e7027c714de55f))
* **multi-agent:** Walkthrough + protocol + command-reference updates (Phase 7) ([194a419](https://github.com/fullya99/worldbox-mcp/commit/194a4194d6169350680410d390c9da9eb02fcb1d))
* Refresh all .md to v0.3 — 26 tools, multi-agent, both auth headers ([b73dabb](https://github.com/fullya99/worldbox-mcp/commit/b73dabb78bfbf29ec21e05f838e4dfd318773184))

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
