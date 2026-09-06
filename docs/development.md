# Development

> See also [CONTRIBUTING.md](contributing.md) for code style, commit conventions, and PR flow.

## Local setup

You need the .NET SDK 8 and [uv](https://docs.astral.sh/uv/). **No WorldBox install is required to
build or test**. Unity references come from the `UnityEngine.Modules` NuGet package, and the mod
reaches the game's own code only through reflection.

```powershell
# Windows
.\scripts\dev-setup.ps1          # or: winget install Microsoft.DotNet.SDK.8 astral-sh.uv
```

```bash
# macOS. The Homebrew cask wants an interactive sudo, the formula does not.
brew install dotnet uv
export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec DOTNET_ROLL_FORWARD=Major

# Linux
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir ~/.dotnet
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
```

SDK 10 builds the net462 mod fine. The net8.0 test project and csharpier 0.x need
`DOTNET_ROLL_FORWARD=Major` on that setup.

```bash
git clone https://github.com/fullya99/worldbox-mcp.git
cd worldbox-mcp
```

## Working on the mod

```bash
dotnet restore mod/WorldBoxBridge.sln --locked-mode
dotnet build mod/WorldBoxBridge.sln --configuration Release -warnaserror
dotnet tool install -g csharpier --version 0.30.6
dotnet csharpier --check mod
```

Build output: `mod/src/WorldBoxBridge/bin/Release/WorldBoxBridge.dll`. Note there is no
target-framework folder in that path: `AppendTargetFrameworkToOutputPath` is off.

`--locked-mode` is real here, `packages.lock.json` is committed for both projects. If you change
a package version, regenerate it or the build fails with NU1004:

```bash
dotnet restore mod/WorldBoxBridge.sln --force-evaluate
```

### Deploying to a local game install

Throughout, `<worldbox>` is your Steam install directory.

```powershell
# Windows
.\scripts\install-mod.ps1 -Local
```

```bash
# macOS / Linux, by hand
WB="<worldbox>"
cp mod/src/WorldBoxBridge/bin/Release/WorldBoxBridge.dll "$WB/BepInEx/plugins/"
```

Then **fully close and relaunch WorldBox**, BepInEx loads plugins once at startup. On macOS and
Linux the game must be started through `run_bepinex.sh`, otherwise BepInEx never loads and nothing
tells you why. Set the Steam launch option to `"<worldbox>/run_bepinex.sh" %command%`.

Liveness probe, with the token the mod generated into its config:

```bash
TOKEN=$(sed -n 's/^token = //p' "<worldbox>/BepInEx/config/WorldBoxBridge.cfg")
curl -s -H "Authorization: Bearer $TOKEN" http://127.0.0.1:8723/health
```

### Tests

```bash
cd mod
dotnet test
```

The mod test suite (xUnit) runs **without the game**. The pattern is "linked sources": pure-logic files from the mod project are referenced as `<Compile Include="..\..\src\..." Link="..." />` in the test csproj so they compile under net8 without Unity. That block is the inventory of what is covered, 26 files behind 15 test classes, so read it rather than a list here. This sentence used to carry such a list and it named five of those fifteen, which is the same way every hand-kept count in this repo has gone.

What the pattern asks of a new class is one thing: no Unity and no BepInEx type anywhere in it. `SavePathResolver` and `ConcurrencyGate` are the shape to copy, and the second is why `MainThreadDispatcher` holds its per-frame job cap in a separate small class rather than inline. Anything that genuinely needs WorldBox to be running lives in the server-side e2e suite instead.

### Decompiling the game

Open `<worldbox>/worldbox_Data/Managed/Assembly-CSharp.dll` (macOS: `worldbox.app/Contents/Resources/Data/Managed/`) in ILSpy. The mod itself never references this assembly, everything game-specific goes through reflection (`GameRefs`), which is what lets it build on a bare CI runner from the `UnityEngine.Modules` NuGet package alone. Record findings in [game-api-notes.md](game-api-notes.md).

## Working on the server

```bash
cd server
uv sync --all-extras
uv run worldbox-mcp --self-check
```

`--self-check` validates that the server can be loaded and emits its tool schemas without needing the mod online.

### Tests

```bash
cd server
uv run pytest tests/unit tests/integration
```

The integration suite spins up a fake bridge in pure Python (`aiohttp`) that mimics the mod's HTTP contract, no game required.

### End-to-end smoke tests

```bash
cd server
uv run pytest tests/e2e --run-e2e
```

These need:

1. WorldBox running with the latest mod installed.
2. `WORLDBOX_MCP_TOKEN` exported (or auto-discoverable).

CI skips this suite by default.

### Keeping the documented tool surface honest

The tool count is stated in six files, and it has drifted three times. `scripts/gen-docs.py`
makes that impossible. It imports the MCP server in-process, asks it which tools are
registered, counts the commands the mod declares, and compares both against the docs. No game
and no network are involved, so it runs anywhere the server installs.

```bash
cd server
uv run python ../scripts/gen-docs.py --check   # what CI runs
uv run python ../scripts/gen-docs.py --write   # refresh the generated regions
```

Two mechanisms, on purpose:

- **Generated regions.** Counts live between markers and `--write` rewrites them:
  `<!-- gen-docs:begin total -->29<!-- gen-docs:end total -->`. Three regions exist, `total`,
  `total-words` for the spelled-out headings, and `bridge-commands` for the C# side. Prose
  outside the markers is never touched, which is how the per-version asset counts, the
  argument columns and the error model survive. The count in
  [compatibility.md](compatibility.md) is deliberately left alone: that row records what a
  released version shipped and must not move when the surface grows.
- **Inventory checks.** The category tables carry editorial columns, so rewriting them would
  cost more than it saves. They are verified instead. `README.md`, `index.md`,
  `multi-agent.md` and `command-reference.md` must each name every registered tool, and any
  `worldbox_`-prefixed identifier anywhere in the docs must resolve to a real tool. An
  identifier that looks like a tool without being one, a payload field for instance, goes in
  the script's `NOT_A_TOOL` set. That includes examples: naming a tool that does not exist
  fails the check, which is the point.

It also cross-checks the two sides of the bridge. The mod declares one command fewer than the
server exposes tools, because `/capabilities` is served by the HTTP layer rather than by an
`ICommand`. Any other gap means a tool was added on one side only.

The same idea covers the screenshot defaults, which are stated three times by necessity: the
MCP schema has to tell the model what it will get, `ScreenshotScaler` has to apply it when the
caller says nothing, and the `worldbox_screenshot` row in
[command-reference.md](command-reference.md) documents it for people. The check reads
`SCREENSHOT_MAX_DIMENSION`, `SCREENSHOT_QUALITY` and `SCREENSHOT_FORMAT` off `tools/read.py`,
compares them with `DefaultMaxDimension`, `DefaultQuality` and `DefaultFormat`, and then
against the `max_dimension=` and `quality=` tokens in that row.

Three failure modes are reported rather than passed over: a constant renamed on either side,
a `const` that is an alias of another (`DefaultFormat = Jpg` resolves through), and a
command-reference row reworded until the tokens no longer match. The last one matters most:
a check that goes quiet because it can no longer find what it was comparing is worse than no
check. The one thing still hand-maintained in that row is the "~150-250 KB" size estimate,
which is prose about a value rather than the value.

The last thing it checks is the release version, which four files state and release-please
bumps through the `extra-files` entries in `release-please-config.json`. They are compared with
each other, because a broken updater entry is silent and the release then ships with one of the
four a version behind. The agreed version must then have a row in
[compatibility.md](compatibility.md), which is the one document recording whether a release
actually works and the only one still written by hand. Nothing used to check that the row
existed, so the failure mode was a matrix quietly a release behind, which reads exactly like a
release nobody has reported a problem with.

That one check stands down on release-please's own branch, passed `--skip-release-version`:
that PR bumps the four files and the row is written once the release is out. Only that check is
dropped, the counts, the inventories and the screenshot defaults all keep running there. The
next ordinary PR fails until the row is written.

The script itself is covered by `server/tests/unit/test_gen_docs.py`, which drives it against
a throwaway tree. A check that stays quiet when the docs drift would be worse than no check.

## Adding a new MCP tool

1. **Mod side**, `mod/src/WorldBoxBridge/Commands/<Category>/<Name>Command.cs`:
   - Implement `ICommand`, pick a `CommandCategory` (Meta, Discovery, Action, Read, Control, Bus)
     and set `RequiresMainThread`.
   - The signature is `Task<object?> ExecuteAsync(JObject args, RequestContext ctx, CancellationToken)`.
     Call `ctx.Require(Permission.X)` first to gate it, and `ctx.CanSeeKingdom` to filter a read
     under fog of war. There is no write-side counterpart on purpose, see
     [multi-agent.md](multi-agent.md#a-kingdom-claim-scopes-reads-not-writes).
   - Reuse `AssetCatalog.Resolve` for any asset id, which gives you `did_you_mean` for free, and
     `WorldAccess` for `MapBox`, units, kingdoms and cities.
   - Throw `BridgeRejectionException` for structured errors, including plain argument
     validation. `HttpBridge` maps it to the right status and envelope. Do not throw
     `ArgumentException` from a command: the router has no arm for it, so it comes back as
     500 `GAME_CRASH` and tells the agent the game broke when the agent asked wrongly. Pure
     helpers with no bridge dependency, `SavePathResolver` and `ScreenshotScaler`, do signal
     with `ArgumentException`, and their callers translate at the boundary.
   - Category semantics matter: **Action and Control are turn-gated** in `turn_based` sessions,
     Meta, Discovery, Read and Bus are not. That is why `turn_advance` lives in Meta rather than
     Control, otherwise a session could deadlock permanently. A gated command that unblocks the
     whole session rather than advancing one agent can opt out by name in
     `TurnGate.AlwaysAllowed`, which is how `dismiss_window` works. Permission gating still
     applies on top, so weigh that before reaching for it.
2. **Register it** in `Plugin.cs#RegisterCommands`, one line.
3. **Server side**, `server/src/worldbox_mcp/tools/<category>.py`: add a
   `@server.tool(name="worldbox_<your_name>", description=...)` function. The description is what
   the model reads to decide when to call your tool, so be concrete about inputs, outputs and
   edge cases.
4. **Update [command-reference.md](command-reference.md)**, and [multi-agent.md](multi-agent.md)
   if the tool is session-aware. Add it to the category table in `README.md` and
   `docs/index.md` too, they are checked for completeness.
5. **Run `uv run python ../scripts/gen-docs.py --write` from `server/`**, which refreshes every
   stated count. Then `--check`, and fix whatever it still reports. CI runs the same check.
6. Build, deploy and smoke-test against a running game.

## When something breaks

| Symptom | First thing to check |
|---|---|
| Mod doesn't load on launch | `<worldbox>/BepInEx/LogOutput.log`, look for `WorldBoxBridge vX.Y.Z starting up...`. If the line is missing, BepInEx never picked up the DLL, so it is in the wrong folder. On macOS and Linux, check you launched through `run_bepinex.sh`. |
| Log looks normal but the plugin never runs | Plugin *load* exceptions do not reach `LogOutput.log`. They only appear in Unity's own `Player.log` (macOS: `~/Library/Logs/mkarpenko/WorldBox/Player.log`). This is how a bad dependency bump hides. See gotcha 10 in [game-api-notes.md](game-api-notes.md). |
| Bridge says it is listening but `/health` refuses the connection | Confirm the port is really bound (`netstat`). If the log says `IsBound=True` and the OS disagrees, you hit gotcha 1 or 2. |
| Every command times out after 30s | The `MainThreadDispatcher` is not running. Look for `[dispatcher] injected into Unity PlayerLoop Update phase`. If absent, gotcha 3. |
| Asset id rejected with `UNKNOWN_ASSET` when you know it exists | Call the matching `list_*` in the same session, the game may have renamed it, and use the `did_you_mean` suggestions. |
| `list_kingdoms` / `list_cities` return 0 with kingdoms alive | Gotcha 4. Check `WorldAccess.GetSimpleList` still iterates through `IEnumerable`. |
| `dotnet restore` fails on `UnityEngine.Modules` | The BepInEx feed is unreachable, or the exact-id source mapping was removed from `mod/NuGet.config`. |
| `dotnet restore` fails with NU1004 | `packages.lock.json` is stale after a version change. Run a `--force-evaluate` restore and commit the result. |
| CI `Lint mod` says `dotnet-csharpier does not exist` | csharpier 1.x got installed instead of 0.30.6. The pin is deliberate. |
| CI `Build mod` fails after a WorldBox update | The game moved to a new Unity version. Bump `UnityEngine.Modules` in `Directory.Packages.props` to match `/health` → `unity_version`. |

## Releasing (maintainers)

Land work on `main` with Conventional Commits. `release-please` runs on every push and maintains a
`chore(main): release X.Y.Z` PR carrying the version bumps and the generated changelog. `feat:`
bumps the minor, `fix:` the patch, `feat!:` the major. Four version files are kept in sync through
`extra-files` in `release-please-config.json`.

**Merge PRs with a merge commit, not a squash.** The repo takes the PR title as the squash subject,
so squashing a PR titled `deps: ...` hides the `feat:` commits inside it and release-please skips
the minor bump.

**Give the merge commit a body that is not a Conventional Commit.** `gh pr merge --merge` puts the
PR title in the merge commit body by default. When that title is itself a Conventional Commit, and
PR titles here usually are, release-please counts the work twice: once on the real commit and once
on the merge. The 0.5.0 release PR was generated with every entry duplicated for exactly this
reason. Pass `--body` with a short review note, the way #42 and #47 did, or an empty string:

```bash
gh pr merge <n> --merge --body "Reviewed: 134 tests green, csharpier and ruff clean."
```

The release-please PR is the one exception to all of this and is squashed.

**Use `ci:` for CI-only work, not `fix(ci):`.** `fix` is a `fix` whatever its scope, so a
workflow tweak lands under "Bug Fixes" beside real user-facing fixes and bumps the patch
version of a package whose shipped code did not change. Several `fix(ci):` commits are in the
changelog for that reason. `ci:` is mapped to a hidden section and bumps nothing, which is
what a runner-only change deserves. The exception is a CI change that alters what ships or
whether it ships at all, a broken release workflow for instance: that is a real fix and the
bump is earned.

Merging the release PR tags the version, creates the GitHub Release, and triggers two jobs:

- `publish-pypi` publishes the wheel and sdist through [PyPI trusted publishing](https://docs.pypi.org/trusted-publishers/).
- `build-and-attach-mod` builds the DLL on the runner and attaches `WorldBoxBridge-vX.Y.Z.zip`
  plus its `.sha256` to the release.

Verify with `gh release view vX.Y.Z --json assets` and by checking the version on PyPI.

If `build-and-attach-mod` ever fails, the manual fallback is to build locally, stage
`WorldBoxBridge.dll` with `install-mod.ps1`, `LICENSE` and `README.md` into a `WorldBoxBridge/`
folder, zip it as `WorldBoxBridge-v<version>.zip`, write the SHA256 next to it, and
`gh release upload "v<version>" <zip> <zip>.sha256 --clobber`.

After the release lands, refresh the Python lockfile and commit it:

```bash
cd server && uv lock   # picks up the version release-please just wrote to pyproject.toml
```

`uv.lock` records the project's own version and release-please cannot update it. Its `generic`
updater needs an `x-release-please-version` annotation in the file, and `uv lock` rewrites the
lockfile whole, dropping every comment, so the annotation would survive exactly until the next
dependency change and then fail silently. `uv sync --frozen` does not revalidate the version
either, which is how `uv.lock` sat at 0.3.3 against a 0.4.0 `pyproject.toml` with CI green. The
`Lockfile matches pyproject` step in `ci.yml` is the backstop: it skips release-please's own
branch, so if this step is forgotten the next ordinary PR fails until someone runs `uv lock`.

Then add the row to `docs/compatibility.md`, status 🔵, saying what the release contains and
that nobody has run it against a game yet. It becomes ✅ only once the
[e2e smoke suite](#end-to-end-smoke-tests) passes against a real install. The row is still
written by hand, on purpose, since no script can know whether a release works. What is checked
is that the row exists: `gen-docs.py --check` fails while the matrix has no row for the version
the tree declares, so a forgotten row blocks the next ordinary PR rather than going unnoticed.

Last, bump the `mod_version` shown in the two sample responses under `docs/install/`, one for
`/health` in `index.md` and one for `capabilities()` in `manual.md`. `gen-docs.py --check`
enforces this one too, and stands down on release-please's branch for the same reason. The check
exists because that sample stated `0.3.0` through three releases, and 0.3.0 to 0.3.3 is the one
family whose DLLs never load, so a reader with a dead mod found the number the page had told them
to expect. It is checked rather than generated: the JSON around the field is illustrative, and a
`gen-docs` region would render as a visible comment inside the fenced block.
