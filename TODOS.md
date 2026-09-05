# TODOS, worldbox-mcp

> What to do next. This file keeps only the future. Anything done leaves here and lives in the
> CHANGELOG, which release-please generates from the commits.

## 🔄 Pick up here, 2026-09-05

**Where things stand**: v0.4.0 shipped and the four items that were queued after it are done.
PyPI has `worldbox-mcp 0.4.0`, the GitHub release carries `WorldBoxBridge-v0.4.0.zip` plus its
`.sha256`, and CI attached them on its own for the first time. The mod builds and tests from a
bare checkout with no game installed, on Windows, Linux and macOS.

Anyone still running a 0.3.x mod DLL has a plugin that silently fails to load. Tell them to
upgrade, because `LogOutput.log` looks perfectly normal in that state and the exception only
shows up in Unity's own `Player.log`.

What landed after the release:

- `compat-check.yml` works again (#48). It had failed on every scheduled run since at least
  2026-08-24, and the missing `wb-update` label was only the outermost of three faults. Steam's
  `UpToDateCheck` endpoint does not know appid 1206560 and answers with an error body, which the
  workflow stored as the current version, then compared against a file that never existed. It now
  reads the `public` branch build id from `api.steamcmd.net` and compares it against
  `.github/worldbox-build-baseline.txt`, seeded with build `19962337`, which is 0.51.2. The
  `wb-update` and `needs-triage` labels exist now; `needs-triage` is referenced by both issue
  templates, so every bug report filed so far had silently lost it.
- `xunit.runner.visualstudio` 4.0.0 (#46), reviewed and merged. The major is an alignment with
  the core framework, not a break: same target frameworks, still runs xunit v1/v2/v3, and 104
  tests were discovered before and after. Worth knowing for later: upstream says the package
  will probably be deprecated once the third-party VSTest runners move to Microsoft Testing
  Platform.
- `dismiss_window` is no longer turn-gated (#50). An open window freezes the simulation for the
  whole session, so clearing it is a shared unblock, not a move. The decision moved into a
  `TurnGate` class the test project can link, which `HttpBridge` cannot.
- `scripts/gen-docs.py` (#51) generates the tool counts and verifies the inventories, with
  `--check` wired into CI. See [development.md](docs/development.md) for how it works.

**In flight**: nothing.

**Next step**: nothing is blocking. The Debt section below is the natural queue, and the two
protocol-correctness items in it are the ones worth doing first, `load_world` lying about its
source and `invoke_power` letting a FactionPlayer trigger global disasters.

**Know before you touch anything**

- `packages.lock.json` is committed for both mod projects. Change a package version, restore
  normally, and CI fails with NU1004. Regenerate with
  `dotnet restore mod/WorldBoxBridge.sln --force-evaluate` and commit the result.
- Merge PRs with a merge commit, never a squash. The repo takes the PR title as the squash
  subject, so squashing hides the `feat:` commits inside and release-please skips the minor bump.
- Prose, comments and commits are all in English, and the repo is deliberately free of em dashes
  outside code blocks and table notation. Keep it that way.
- Every stated tool count is generated. Run `uv run python ../scripts/gen-docs.py --write` from
  `server/` after adding or removing a tool, do not edit the numbers by hand.

---

## 🔴 Blocked

Nothing.

## 🎯 Next up

- [ ] `actionlint` is not wired into anything. `CLAUDE.md` says it catches a mangled
      `[email protected]` action ref, and it would, but nothing runs it: it is absent from
      `ci.yml` and from `.pre-commit-config.yaml`, and `.gitignore` only ignores the binary
      someone once downloaded by hand. Either add a step to `ci.yml`, which is a few lines and
      validated clean on all four workflows today, or delete the sentence. A rule nothing
      enforces is worse than no rule.
- [ ] `server/uv.lock` still declares `worldbox-mcp 0.3.3` while `pyproject.toml` is at 0.4.0.
      release-please bumps the version but not the lockfile, and `uv sync --frozen` does not
      revalidate it, which is why CI never noticed. Harmless today, confusing later. Decide
      whether release-please should own the file or whether the release checklist should.

## 🧹 Debt

Found during the pre-merge review of #37 to #42. None of these are regressions from that batch,
they are pre-existing and were surfaced, not introduced.

- [ ] `Commands/Control/LoadWorldCommand.cs:140` reports `source: "path"` and echoes the caller's
      raw path whenever `path` is non-empty, even when `bytes_b64` was supplied and actually used.
      The guard only rejects the case where both are empty. The response lies about what was read.
- [ ] `invoke_power` gates on `RequireAny(ActionFaction, ActionGlobal)`, while its sibling
      `paint_tile` requires `ActionGlobal` with an explicit comment about not letting a
      FactionPlayer reshape an opponent's territory. #42 widened which powers actually fire, so a
      FactionPlayer in a PvP session can now drop a volcano anywhere on the map. Either match
      `paint_tile`, or classify per power.
- [ ] `Commands/Control/SavePathResolver.cs` documents that a relative name can never escape the
      saves directory. That is not quite true: `Path.IsPathRooted` returns true for Windows
      drive-relative forms like `C:foo` and `\foo`, which skip the `..` check entirely. Behaviour
      is no worse than before the helper existed, but the stated invariant is false.
- [ ] `Commands/Control/SetSpeedCommand.cs:129` duplicates `ListSpeedsCommand.CurrentSpeedId`
      almost line for line, and its copy bypasses the `GameRefs` cache by calling `GetField`
      directly on every read. Extract one helper.
- [ ] `server/src/worldbox_mcp/tools/read.py` repeats the screenshot defaults (1280, jpg, 80) as
      bare literals that must track `ScreenshotScaler`'s constants. Nothing catches the drift.
      Either comment the coupling or pass `None` and let the bridge decide.
- [ ] `LoadWorldCommand.ResolveMapFile` has three untested branches. It cannot be linked into the
      test project today because it reads `GameSavePaths.SavesRoot`, which touches
      `Application.persistentDataPath`. Parameterise it the way `SavePathResolver.ResolveFolder`
      already takes `savesRoot`, then test it.
- [ ] `GameUiAccess` has no interface seam, so the branch logic in `DismissWindowCommand` and
      `GetUiStateCommand` cannot be unit tested even though it is Unity-type-free at the surface.
- [ ] Roadmap item 9: `fix(ci):` commits land under "Dependencies" in the generated changelog.
      Cosmetic, but easier to fix before the next minor than after.

## 💡 Not committed to

Carried over from the CLAUDE.md roadmap. Read that section for the reasoning behind each.

- Single multi-tenant MCP server, so N agents no longer means N server processes.
- Auto-resolve `kingdom_claim: "auto:N"` on first world load, which would make PvP scoping real
  rather than best-effort.
- The remaining power delegates: `click_brush_action`, `toggle_action`, `click_special_action`.
- `get_actor(name_or_id)`, and `terraform(action_id, x, y, radius)`.
- Opt-in JSONL message log for replay and post-mortem.
