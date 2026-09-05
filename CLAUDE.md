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

- **Latest**: `v0.4.0`, 2026-09-05. PyPI and the GitHub Release are both current, and CI attaches
  the mod ZIP by itself.
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
  release-please reads them to bump SemVer and write the changelog.
- **Merge PRs with a merge commit, never a squash.** The repo takes the PR title as the squash
  subject, so squashing a PR titled `deps: ...` hides the `feat:` commits inside it and the minor
  bump is silently skipped. The one exception is release-please's own PR, which is squashed.
- **Everything is written in English**, including prose, comments and commits.
- **C#**: nullable enabled, warnings as errors, formatted by csharpier.
- **Python**: ruff for lint and format, `mypy --strict`. Annotate everything, `Any` only at the
  MCP boundary.

## Rules that are easy to break

- **Every Unity API call goes through `MainThreadDispatcher.RunOnMainThreadAsync`.** Anything else
  corrupts game state without an error.
- **No `System.ValueTuple`** in a signature, a field type or a dictionary key. It is not always
  loadable under Unity Mono on net462. Use a `readonly struct`.
- **Never add a package reference you do not use.** A dependency bump can break the plugin at load
  time without touching a line of game code, and the failure is invisible in the normal log. This
  has happened twice. Gotcha 10 in [game-api-notes](docs/game-api-notes.md) has the detail.
- **`packages.lock.json` is committed.** Change a package version and CI fails with NU1004 until
  you regenerate it with `dotnet restore mod/WorldBoxBridge.sln --force-evaluate`.
- **No `[email protected]` in workflows.** Cloudflare email obfuscation has mangled a real action ref
  here before. `actionlint` catches it.
- **Tool counts drift.** Nine files can state the number. When you add or remove a tool, grep for
  the old count before you commit.

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
