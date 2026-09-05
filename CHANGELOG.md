# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This file is maintained automatically by [release-please](https://github.com/googleapis/release-please)
from Conventional Commits.

## [0.6.0](https://github.com/fullya99/worldbox-mcp/compare/v0.5.0...v0.6.0) (2026-09-05)


### Features

* **mod+server:** Drive brush and toggle delegates; radius, pulses and drag for invoke_power ([4613d73](https://github.com/fullya99/worldbox-mcp/commit/4613d73517ba516bbe88f58a0b19a6c3bf891a98))


### Bug Fixes

* Harden multi-frame pulse runs and list_powers per adversarial review ([1a62c0e](https://github.com/fullya99/worldbox-mcp/commit/1a62c0e2b36d781b92c5a7b573ae8c092cc8cee0))
* **mod:** Read the save off the main thread so load_world cannot freeze the game ([a2334f3](https://github.com/fullya99/worldbox-mcp/commit/a2334f33d3884c18a4384c7e3a60b25ea2a08fe6))
* **mod:** Refuse a pipe before opening it, and pin the reader's untested paths ([d447ab1](https://github.com/fullya99/worldbox-mcp/commit/d447ab167d0f2dadf98cdfac80812fee14acddc6))
* **mod:** Warn when the game's brush id disagrees, and correct the threading note ([9f4096f](https://github.com/fullya99/worldbox-mcp/commit/9f4096f2aace5b1cf3589116a307bee03a9b92da))


### Refactors

* **mod:** Drop the kingdom guard nothing called, and say what a claim scopes ([c9abd5b](https://github.com/fullya99/worldbox-mcp/commit/c9abd5b5f8a58fd8e607a8e23de5b848f5ad1870))

## [0.5.0](https://github.com/fullya99/worldbox-mcp/compare/v0.4.0...v0.5.0) (2026-09-05)


### ⚠ BREAKING CHANGES

* **mod:** agents with the FactionPlayer role can no longer call invoke_power and receive PERMISSION_DENIED. Use spawn for creature placement, or run the agent as God.

### Features

* **mod:** Let any agent dismiss a blocking window in turn_based mode ([733bcf7](https://github.com/fullya99/worldbox-mcp/commit/733bcf735f70e1a819f46259f1ee45daf71f3931))
* **mod:** Let any agent dismiss a blocking window in turn_based mode ([e6f7af8](https://github.com/fullya99/worldbox-mcp/commit/e6f7af869ac4b6682fef45132f8bd1159e8d27a4))


### Bug Fixes

* **ci:** Make the compat check actually detect WorldBox updates ([f2d4f4f](https://github.com/fullya99/worldbox-mcp/commit/f2d4f4fcea29802b439901ff18d4ad7b3889fd5b))
* **ci:** Make the compat check actually detect WorldBox updates ([6295ccb](https://github.com/fullya99/worldbox-mcp/commit/6295ccbcb7253e266b36c0a41d117d94641eae59))
* **mod:** Close the drive-relative hole in the save path rules ([521475d](https://github.com/fullya99/worldbox-mcp/commit/521475d5c54fcc5966905cdf7a4eee2d020cc940))
* **mod:** Decide save-path containment by resolving, not by shape ([a26ac03](https://github.com/fullya99/worldbox-mcp/commit/a26ac03c4860b5b97ce99135feaf0c5fecfa98d9))
* **mod:** Gate invoke_power on the global action scope ([16ed7cf](https://github.com/fullya99/worldbox-mcp/commit/16ed7cfffdd2b1dce8285077abb05b199bea1f76))
* **mod:** Raise BAD_ARGS from the commands instead of catching a base type ([d835136](https://github.com/fullya99/worldbox-mcp/commit/d83513607ca5d201dd33a88d841229f55b802621))
* **mod:** Report a bad argument as BAD_ARGS, not as a game crash ([75b9cee](https://github.com/fullya99/worldbox-mcp/commit/75b9cee4f6e36f6836d6798208dd5362b2f18329))
* **mod:** Report the source load_world actually read ([042f041](https://github.com/fullya99/worldbox-mcp/commit/042f041c1babe7f6224ed56dbf51fe826b0bdb3d))


### Performance

* **mod:** Stop paying for work the frame does not need ([10b4580](https://github.com/fullya99/worldbox-mcp/commit/10b45809195a388857e9e50f12154f42371c4b84))


### Dependencies

* Bump xunit.runner.visualstudio from 3.1.5 to 4.0.0 ([10c0e01](https://github.com/fullya99/worldbox-mcp/commit/10c0e0148895a2a15f0ce4d9e7fe65e9324bdd3d))


### Refactors

* **mod:** Give the UI layer an interface seam and test its branches ([7d9223f](https://github.com/fullya99/worldbox-mcp/commit/7d9223fa9231fdfcd6623ca0099c0fc5c88613c5))
* **mod:** One reader for the active simulation speed ([5cae2d9](https://github.com/fullya99/worldbox-mcp/commit/5cae2d9b390a39bcbfd5995a2c15a3472b82a789))


### Chores

* Cut 0.5.0 rather than 1.0.0 ([fb3d4cd](https://github.com/fullya99/worldbox-mcp/commit/fb3d4cdf2e49f527b09f58336f0850b2043cf98a))

## [0.4.0](https://github.com/fullya99/worldbox-mcp/compare/v0.3.3...v0.4.0) (2026-09-05)


### Features

* **mod+server:** Detect, dismiss and suppress the startup window ([9e117f9](https://github.com/fullya99/worldbox-mcp/commit/9e117f95821a8f2f58b7785fa7b1af68fbf09fd1))
* **mod+server:** Downscale screenshots and return them as MCP image blocks ([74f1f45](https://github.com/fullya99/worldbox-mcp/commit/74f1f455e50b454130c06b354ebebc8bfb996c56))
* **mod+server:** List_speeds discovery tool and richer set_speed errors ([df400c0](https://github.com/fullya99/worldbox-mcp/commit/df400c0f5c0868c0ac29aa9a1bbff20b4fe9bcbb))
* **mod:** Resolve save_world/load_world names under the game's saves directory ([30d387a](https://github.com/fullya99/worldbox-mcp/commit/30d387a36a9cd209901cd9095be067a133f18528))
* **server:** Migrate to mcp 2.x MCPServer API ([14f9a24](https://github.com/fullya99/worldbox-mcp/commit/14f9a24f47b44d4ff9ed3feac8d0a20d5b5ed15d))


### Bug Fixes

* Build, install and run the mod correctly on macOS ([cb59764](https://github.com/fullya99/worldbox-mcp/commit/cb5976403a22cbfe94151c0538bd96601d179ef8))
* **examples:** Request lossless PNG for the screenshots ecology_smoke writes ([f188f38](https://github.com/fullya99/worldbox-mcp/commit/f188f387a0b8547b9e358e2e3bff2156aee1eb6f))
* **mod:** Destroy the downscaled texture when GPU readback fails ([a88ba1c](https://github.com/fullya99/worldbox-mcp/commit/a88ba1c897fc46f7a79402fc7c95637b19d6a31a))
* **mod:** Drive click_power_action powers, reject input-dependent ones, guard save_world during loading ([3e2cd2d](https://github.com/fullya99/worldbox-mcp/commit/3e2cd2d8b17248f7ba30ba2ebf4b458b32c13c6a))
* **mod:** Drop unused HarmonyX reference that broke plugin load ([1c2a3cf](https://github.com/fullya99/worldbox-mcp/commit/1c2a3cf1a6570380e24fe2df87fe1c388e4f3cf4))
* **mod:** Keep UI reflection and startup-window suppression fail soft ([bb4ad9f](https://github.com/fullya99/worldbox-mcp/commit/bb4ad9fcd0a5a5034a0770afa484d1f559628d61))
* **mod:** Log the real exception before rejecting a power as pointer-dependent ([c456e2c](https://github.com/fullya99/worldbox-mcp/commit/c456e2c12d5a505ad0bfdc2fc152b3fc7cdd67e6))
* **mod:** Pin Newtonsoft.Json to the game's bundled 13.0.2 ([a7682c8](https://github.com/fullya99/worldbox-mcp/commit/a7682c8c94aaf7eb07b08499b46c15ac260c6a70))
* **server:** Surface bridge errors to the model as ToolError with did_you_mean ([4c4bc6d](https://github.com/fullya99/worldbox-mcp/commit/4c4bc6d6ad0b66838be719ed5b911bf2d7ea83ba))


### Dependencies

* **server:** Refresh Python lockfile ([a98bb28](https://github.com/fullya99/worldbox-mcp/commit/a98bb28928831bfe57aab1552f10d54b610a1cca))

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
