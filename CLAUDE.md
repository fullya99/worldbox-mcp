# CLAUDE.md

Context for AI coding sessions in this repo. Loaded automatically, so it stays short: only what
must be true every time. Everything else lives in `docs/`, which is the published site and the
right place for anything a human contributor also needs.

<!-- style-redaction
prose: en
code: en
commentaires: en
interface: en
git: en
-->

## What this is

`worldbox-mcp` lets any MCP client drive the live game [WorldBox](https://www.superworldbox.com/)
(Unity 2022.3.60f1, Mono). Two pieces shipped from this monorepo:

- **`mod/`**, a BepInEx 5 plugin (net462) injected into the game, exposing a token-authenticated
  HTTP API on `127.0.0.1:8723` and reaching game internals purely through reflection.
- **`server/`**, a Python 3.11+ MCP server on PyPI that proxies tool calls to that API.

<!-- gen-docs:begin total -->29<!-- gen-docs:end total --> tools across six categories. A multi-agent session layer (roles, permissions, fog of war, turn
order, message bus) activates when `BepInEx/config/WorldBoxBridge.agents.json` exists, otherwise
the bridge runs single-tenant.

## Status

- **Latest released**: `v0.6.0`, 2026-09-05, on PyPI and on the GitHub Release with the mod ZIP
  CI attaches by itself. Not yet verified against a live game, and neither was `v0.5.0`, so two
  releases stand on static evidence alone, see [compatibility](docs/compatibility.md).
- **`main` is ahead of it by a feature**, so what PyPI serves is not what the tree does. 0.7.0
  waits in release-please's PR with the two concurrency bounds of #68. Whoever cuts it makes a
  third release on static evidence unless the live pass happens first.
- **Breaking in 0.5.0**: a `FactionPlayer` agent can no longer call `invoke_power` and gets
  `PERMISSION_DENIED`. God powers are map-wide, so they carry the same gate as `paint_tile`.
  Creature placement stays available through `spawn`.
- **Careful**: the released DLLs for 0.3.0 to 0.3.3 do not load at all. If someone reports a dead
  mod on those versions, that is why, and `LogOutput.log` looks perfectly normal in that state.
- `main` is the shipping branch. release-please keeps a release PR open as commits land.

## Start here

1. `TODOS.md`, the "Pick up here" block first. That is the anchor after a `/clear`.
2. This file, for what is always true.
3. `docs/` for depth: [architecture](docs/architecture.md) for the layout and the request flow,
   [game-api-notes](docs/game-api-notes.md) for the reflection traps, [development](docs/development.md)
   for build, deploy, diagnostics and the release process, [command-reference](docs/command-reference.md)
   for the tool surface, [multi-agent](docs/multi-agent.md) for the session layer.

If an `archives/` directory exists, do not read it when picking up work. It is stale by
construction and only kept for history.

## Conventions

- **Commits**: [Conventional Commits](https://www.conventionalcommits.org/), in English.
  release-please reads them to bump SemVer and write the changelog. **A change that ships no
  code takes a type that does not bump**: `ci:` for workflows, `docs:` for prose, `chore:` for
  tooling and lockfiles. Reaching for `fix:` there opens a release PR for a version nobody can
  install anything new from, which is how #66 came to propose 0.6.1 for a corrected sample
  response and a `gen-docs` check. The rule used to name only `ci:`, and that was too narrow.
- **Merge PRs with a merge commit, never a squash.** The repo takes the PR title as the squash
  subject, so squashing a PR titled `deps: ...` hides the `feat:` commits inside it and the minor
  bump is silently skipped. The one exception is release-please's own PR, which is squashed.
- **Give the merge commit a body that is not a Conventional Commit.** `gh pr merge --merge`
  defaults the body to the PR title, and PR titles here are usually Conventional Commits, so
  release-please counts the work twice and the changelog ships duplicated. Pass `--body` with a
  short review note. See [development.md](docs/development.md).
- **Everything is written in English**, including prose, comments and commits.
- **C#**: nullable enabled, warnings as errors, formatted by csharpier.
- **Python**: ruff for lint and format, `mypy --strict`. Annotate everything, `Any` only at the
  MCP boundary.

## Rules that are easy to break

- **Every Unity API call is marshalled onto the main thread**, through
  `MainThreadDispatcher.RunOnMainThreadAsync` for a single call or
  `RunPerFrameOnMainThreadAsync` for one that repeats per frame. Anything else corrupts game
  state without an error. A command may legitimately report `RequiresMainThread => false` and
  marshal only the game call, which is what keeps blocking I/O out of a frame, see
  `LoadWorldCommand`. And `true` marshals the command's first thread, not its whole body: an
  `await` inside one escapes the dispatcher's deadline and per-frame cap. The remarks on
  `ICommand.RequiresMainThread` carry the detail.
- **No `System.ValueTuple`** in a signature, a field type or a dictionary key. It is not always
  loadable under Unity Mono on net462. Use a `readonly struct`.
- **Never add a package reference you do not use.** A dependency bump can break the plugin at load
  time without touching a line of game code, and the failure is invisible in the normal log. This
  has happened twice. Gotcha 10 in [game-api-notes](docs/game-api-notes.md) has the detail.
- **`packages.lock.json` is committed.** Change a package version and CI fails with NU1004 until
  you regenerate it with `dotnet restore mod/WorldBoxBridge.sln --force-evaluate`.
- **No `[email protected]` in workflows.** Cloudflare email obfuscation has mangled a real action ref
  here before. The `lint-workflows` CI job runs `actionlint` and catches it now.
- **Never edit a stated tool count by hand.** Six files state it and it drifted three times.
  `scripts/gen-docs.py --write`, run from `server/`, owns every one of them, and `--check` runs
  in CI. See [development.md](docs/development.md).
- **A release owes three chores**, and each one fails the next ordinary PR if skipped, on
  purpose: `uv lock` from `server/`, a new row in [compatibility](docs/compatibility.md), and the
  `mod_version` in the two sample responses under `docs/install/`. `gen-docs.py --check` enforces
  the last two.

## CI choices that look wrong and are not

Each of these fixed a real failure. Read the reason before tidying one away.

- **csharpier is pinned to 0.30.6.** 1.x changed the CLI invocation and would reformat the whole
  csproj and props tree. Upgrading is a deliberate decision, in a PR of its own.
- **`mkdocs.yml` sits at the repo root**, not in `docs/`. mkdocs rejects a config whose `docs_dir`
  is its own parent.
- **`release.yml` scopes `permissions:` per job.** `build-and-attach-mod` executes third-party
  MSBuild targets during restore, so it holds `contents: write` and deliberately no
  `id-token: write`. Only `publish-pypi` gets the PyPI token. Do not hoist these back to the top.
- **FluentAssertions is capped at 6.x** in dependabot config. v7 moved to a paid commercial
  licence. v6 is the last MIT release.
- **Two checks stand down on release-please's branch.** That PR bumps `pyproject.toml` and
  cannot run `uv`, so `uv.lock` is legitimately one version behind until the release lands, and
  the compatibility matrix has no row for a version that is not out yet. The lockfile step is
  skipped whole; gen-docs keeps running and drops only its version check, via
  `--skip-release-version`. Both fail on the next ordinary PR until someone runs `uv lock` and
  writes the row.
- **CI never starts by itself on release-please's PR.** That PR is opened by
  `github-actions[bot]`, whose author association is `CONTRIBUTOR`, so the repo's
  workflow-approval gate holds it: the run sits at `action_required` with zero jobs and the PR
  reads `UNSTABLE`, which means unmeasured, not failing. Approve it with
  `gh api -X POST repos/fullya99/worldbox-mcp/actions/runs/<id>/approve`, or merge on the strength
  of a green `main`, since that PR only touches version strings, the release manifest and the
  changelog. The same gate is why CI does not auto-run on an external contributor's PR.
- **The Pre-commit job does not run csharpier**, because that runner has no .NET SDK. The
  dedicated `lint-mod` job covers it.
- **The MCP conformance check calls `worldbox-mcp --self-check` directly**, not the MCP Inspector
  CLI, which now needs `--method` to produce anything useful.
- **GitHub Pages was bootstrapped once** with `gh api repos/.../pages -X POST -F build_type=workflow`,
  because the default token cannot create the Pages site. `actions/configure-pages` is idempotent
  afterwards.
- **No user-level CNAME.** One used to redirect every project site under this account to a dead
  domain. If a custom domain is ever needed, use a per-project `docs/CNAME` so the blast radius
  stays bounded.

## Working agreements

- Verify before asserting. A claim about the game's API, a version, or a CI behaviour needs a
  command that proves it, not a recollection.
- Adding a tool touches four places: the C# command, its registration in `Plugin.cs`, the Python
  tool, and `docs/command-reference.md`. The checklist is in [development.md](docs/development.md).
- What is not testable without the game: live `/health`, powers, screenshots, save and load round
  trips. Everything else, including the whole C# side, builds and tests on a bare machine.

## Context convention

The repo is the source of truth, not the conversation. `TODOS.md` holds what to do next, this file
holds what is always true, `docs/` holds the depth, and `CHANGELOG.md` is generated by
release-please and must never be hand-edited.

A document that stops being true is moved to `archives/<YYYY-MM>/<original path>` with a header
saying when, why and what replaced it, never deleted quietly. Create that directory the first
time you actually need it, there is no point carrying an empty one. The exception to the rule is
a durable statement in this file that has become false: that gets removed outright, because a
wrong rule is worse than a missing one.

No work in progress without a written task in `TODOS.md`. No secret in a tracked file: say where
the key lives, never what it is.

Before a `/clear`, run `/cloture` or just ask for the project to be tidied.
