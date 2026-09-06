# TODOS, worldbox-mcp

> What to do next. This file keeps only the future. Anything done leaves here and lives in the
> CHANGELOG, which release-please generates from the commits.

## 🔄 Pick up here, 2026-09-06, closing the fourth session of 2026-09-05

**Where things stand**: `main` is at `b5d82a2` and **`v0.6.0` is the released version**.
PyPI serves it and the GitHub release carries `WorldBoxBridge-v0.6.0.zip` with its `.sha256`.
#68 landed on top of it and 0.7.0 is now sitting in release-please's PR, unreleased. 223 xUnit
and 84 pytest are green here, `gen-docs.py --check` is clean, and the tool count is still 29.

**#68 was the work of this session**, and it closed the last Debt item that needed no running
game. Two counted bounds over one `ConcurrencyGate`: an admission gate in
`HttpBridge.ExecuteCommandAsync` capped by `max_concurrent_requests` (default 8), and a cap of 32
registered per-frame jobs in `MainThreadDispatcher`, the same number of queued actions it already
drained per frame. Both refuse with the new `503 BUSY`. Its description carries the two placement
arguments in full, and the section below repeats the short form. Only half the in-flight Debt
item closed, see the entry, which now states the residual instead of ticking the box.

**#70 exists because a code review took #68 apart**, and it is worth reading before touching any
of this. #68 shipped the handler's 60-second backstop on the main-thread branch only, while
`load_world` reports `RequiresMainThread => false` and does its whole file read before its first
`await`. So a wedged read held its admission slot for the life of the process, and the eighth one
took the bridge down entirely where before #68 it would have leaked a thread and left the other
28 commands working. A bound that turns a partial failure into a total one is worse than no
bound. #70 puts the backstop over both branches, which needs `Task.Run` on the pool-thread branch
for the reason the comment there gives. The same review found the refusal path echoing a 4 MB
`bytes_b64` back to the caller while refusing work to save memory, a 5s admission wait that ate
the Python client's entire 35s headroom so a real `MAIN_THREAD_TIMEOUT` could not reach it, a
cancellation still labelled `GAME_CRASH`, and three doc sentences of mine that were simply false.
All fixed there. Two were promoted to Debt entries instead, below.

**#66 is now an honest release, and cutting it is the next release decision.** It is
release-please's own PR, the one exception that gets squashed. It proposed 0.6.1 only because a
commit in #65 was typed `fix(docs)` while shipping no code, and merging it in that
state would have published a release whose only content was a corrected sample response. #68 is
a real `feat:`, so the PR now proposes **0.7.0**, and its changelog reads one Features line and
one Bug Fixes line with nothing duplicated, which is the merge-body convention working. Cutting
it owes the three chores below. Nothing forces the release today: another `feat:` would simply
roll into the same PR.

**The one thing owed to a running game, and it now costs more**: two releases in a row, 0.5.0 and
0.6.0, ship on static evidence alone, and 0.7.0 will make three unless someone runs the live
pass. Every test this repo has is green on this machine and not one of them can see the game. The
machine these sessions run on has no WorldBox install, so the live pass is blocked on hardware,
not forgotten. The Blocked section names the four checks, and the ZIP to install downloads
straight off the 0.6.0 release instead of needing a rebuild.

**Do not redo these five arguments**, each cost a review round.

- `load_world` reads off the main thread and marshals only `loadMapFromBytes`. The reason is
  that the dispatcher's 30s deadline is a *queueing* deadline, tested before the action runs,
  so it stops nothing that has started. Gotcha 11 in
  [game-api-notes](docs/game-api-notes.md) is the canonical statement.
- **An `await` inside a command that reports `RequiresMainThread => true` does not resume on a
  pool thread.** Unity installs `UnityEngine.UnitySynchronizationContext` on the main thread, it
  is in the UnityEngine.Modules reference assembly the mod compiles against, and the engine
  pumps it from the player loop. The continuation comes back to the main thread but outside the
  dispatcher, so it escapes the deadline and the `maxPerFrame` bound. #62 first shipped the
  opposite claim and an adversarial pass caught it. Reaching for `ConfigureAwait(false)` or
  `Task.Run` to get back onto the main thread is what leaves it.
- **`BrushAccess` deliberately does not trust the id the game hands back.** Preferring it over
  the constructed `circ_<radius>` swaps a value that is correct on stock builds for one whose
  provenance nobody has verified, because `Brush.get(int, string)`'s return type is recorded
  nowhere. It logs disagreement instead. The Debt item says what one live call turns that into.
- **The admission gate sits after authentication, not around the connection** (#68). Wrapping
  `HandleClientAsync` would have been simpler and wrong twice: unauthenticated traffic could
  spend the slots, and a client that dribbles its request in over the 35s read timeout would
  hold a slot it is not using. Socket reads and writes stay outside the gate.
- **The per-frame job cap is not left to follow from the request cap** (#68). It would almost
  hold, since every job is registered by a request that awaits it. But the handler's 60-second
  backstop can abandon a job that outlives it, and a bound that holds only while `PulseRunBudget`
  (25s) stays under that backstop (60s), two constants in separate files, is not a bound.

**Know before you touch anything**

- `packages.lock.json` is committed for both mod projects, and `mod/Directory.Build.props` sets
  `RestorePackagesWithLockFile`, so this is real: change a package version and CI fails with
  NU1004 until you regenerate with `dotnet restore mod/WorldBoxBridge.sln --force-evaluate`.
- Merge PRs with a merge commit, never a squash, and give that merge a prose body that is not a
  Conventional Commit. Both halves matter. #60's clean changelog is the evidence.
- A change that ships no code takes a type that does not bump: `ci:` for workflows, `docs:` for
  prose, `chore:` for tooling. Reaching for `fix:` is what opened #66 for nothing.
- Prose, comments and commits are all in English, and the repo is deliberately free of em dashes
  outside code blocks and table notation.
- Every stated tool count is generated. Run `uv run python ../scripts/gen-docs.py --write` from
  `server/`, never edit a count by hand.
- Every release owes three chores, all done for 0.6.0 and all owed again by 0.7.0: run `uv lock`
  from `server/` and commit it, write the new row in [compatibility.md](docs/compatibility.md),
  and bump the `mod_version` shown in the two sample responses under `docs/install/`. Skip any of
  them and the next ordinary PR fails on the lockfile step or on `gen-docs.py --check`, which is
  the design.
- `.NET` on this box: `export DOTNET_ROOT=$HOME/.dotnet` and
  `PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"`.
- There is no `CODEMAP.md` and no `archives/` here, on purpose. A context audit flags both, plus
  a missing `docs/README.md`, the CHANGELOG date format, missing status headers on the `docs/`
  pages, a PowerShell snippet in `multi-agent.md` read as dead links, and two test tokens one of
  which is literally named `test-token-do-not-use`. It also claims this file has no resume block,
  because it looks for the French heading and this one is called "Pick up here". That is 29
  alerts, all of them checked on 2026-09-06, none of them real. `archives/` gets created the
  first time something is genuinely obsolete, and nothing has been yet: the drifting statements
  found so far were all sections of live files, which get corrected in place.

**Next step**: the live pass, and nothing else is close in value. Install the 0.6.0 ZIP from the
GitHub release on a machine that has WorldBox and run the four checks the Blocked section names.
One session there closes three Debt items and turns both 🔵 rows into ✅ or into bug reports, and
it would stop 0.7.0 from becoming the third release in a row cut on static evidence. If the live
pass has to wait for hardware, the two items left that need no game are the `GameRefs` cache key
and the cancelled request reported as a main-thread timeout. Neither is large.

---

## 🔴 Blocked

- **Every live verification, on hardware rather than on a decision.** The machine these sessions
  run on has no WorldBox install, so nothing in the Debt list that needs a running game can be
  closed from here. Four items are waiting on one session at a machine that has the game: the
  `load_world` load path, the `IsWorldLoading` pre-flight, what `Brush.get(int, string)` returns,
  and dewet22's two untested guards in `invoke_power`. The ZIP no longer needs rebuilding:
  `WorldBoxBridge-v0.6.0.zip` and its `.sha256` hang off the
  [v0.6.0 release](https://github.com/fullya99/worldbox-mcp/releases/tag/v0.6.0), built by CI the
  way `release.yml` does it, Release plus `-warnaserror` plus `restore --locked-mode`, with the
  DLL staged by `scripts/install-mod.ps1` alongside the README and the LICENSE.

## 🎯 Next up

1. **The live pass on a machine that has the game.** Everything else in this file is either
   blocked behind it or worth less than it. See the Blocked section for the four checks and the
   install recipe.
2. **`GameRefs` caches members without their binding flags.** Needs no game, and the fix is a
   better cache key plus three warning messages that stop claiming a consequence they cannot
   know. See the Debt entry.
3. **A cancelled request is reported as a main-thread timeout.** Also needs no game, also small,
   and the wrong label lands in the log of whoever is already debugging a hang.
4. Then the rest of the Debt section, which is the natural queue.

## 🧹 Debt

- [ ] **One live `load_world` is still owed.** The fix landed in #58 without it, on static
      evidence, and the merge commit of `ccf4261` records exactly what that evidence was. A probe
      harness linked `SaveFileReader`, `SavePathResolver` and `GameSavePaths` out of the branch
      and ran them against real special files under a watchdog: a FIFO with no writer is refused
      in 2 ms where `File.ReadAllBytes` on that same FIFO was still blocked after 3 seconds,
      `/dev/zero` goes the same way, a 300 MB file is refused after allocating 1232 bytes, and a
      save name resolves under a sampled `persistentDataPath` and reads a real zip back whole,
      which is also the proof that the `Capture` handoff works. So the refusal half is settled.
      What is not: that a load still completes now that the command starts on a pool thread,
      which needs one `load_world` with `path: "save1"` and one with `bytes_b64`. Note also that
      the probe ran on Linux under .NET 8, and the zero-length signal for special files is a
      measurement on the wrong runtime for a plugin that ships against Mono net462.
- [ ] **Still no deadline on the save read itself.** A regular file on a dead network mount
      blocks in `load_world`'s read forever. Socket timeouts are spent once the request is parsed
      and the dispatcher's deadline is not on this path, so nothing interrupts it and nothing in
      net462 can. What each wedged call costs now is one pool thread and one descriptor for the
      life of the process, which is what it cost before #68 too. It no longer costs an admission
      slot past 60s, because the handler's backstop covers the pool-thread branch as well since
      #70. That correction matters: #68 shipped the backstop on the main-thread branch only, so
      for one release the eighth wedged read took the whole bridge down rather than leaking a
      thread, which is worse than the problem it was bounding. The fix is still a deadline around
      the read. The FIFO and character-device cases, the easy way to trigger this, are refused
      before the open since #58.
- [ ] **The accept loop is unbounded and the read has no deadline either.** `AcceptLoopBlocking`
      hands every connection to `Task.Run` with nothing capping how many are in flight, and the
      `ReceiveTimeout` set on the socket does not apply to the reads the bridge actually does:
      it covers synchronous `Socket.Receive` only, and `ReadRequestAsync` goes through
      `NetworkStream.ReadAsync`. A client that sends half a request line and stops therefore
      parks in `ReadHeadersAsync` with no deadline, costing a socket and a pool thread until the
      process ends. #68's admission gate does not reach this: it deliberately sits after the
      read, so an idle connection cannot hold a slot, which also means it cannot be counted.
      Wants a real read deadline, a cap before `Task.Run`, or both. Found reviewing #68, which
      claimed the 35s timeout bounded this case; the comment saying so is corrected in #70.
- [ ] **Admission runs before the permission and turn gates, so a refused caller holds a slot.**
      `ctx.Require(Permission.X)` is the first statement of each command, and the turn gate sits
      just inside the admission gate, so both refuse only after a slot is taken, and a
      main-thread command refuses a frame later still. An authenticated agent with no useful
      permissions can therefore loop `paint_tile` and occupy every slot while being denied every
      time. Moving the turn gate ahead of admission is easy; moving `ctx.Require` means hoisting
      the per-command permission out of the commands, which is the part that needs a design
      rather than an edit. Found reviewing #68.
- [ ] **`load_world` has no `IsWorldLoading` pre-flight, where `save_world` does.** Two loads can
      now do their reads in parallel and queue two `loadMapFromBytes` invokes that the dispatcher
      can drain in the same frame, the second landing on a load the game just started. Mirroring
      the `save_world` guard means checking `_world.IsWorldLoading` inside the marshalled
      delegate, so the check and the invoke share a frame, and injecting `WorldAccess` into the
      command. Not verifiable without the game, so it wants the same live pass as the item above.
- [ ] **Nobody has written down what `Brush.get(int, string)` returns.** The brush-machinery
      section of [game-api-notes](docs/game-api-notes.md) is headed "verified against the 0.51.2
      decompile" and records that this overload clones `circ_1` as `circ_N`, but not its return
      type, while the same section says the `Config.current_brush` setter fills
      `current_brush_data` "via `Brush.get(id)`". So at least one overload of that name answers
      with brush data rather than with the library asset, and `BrushAccess` cannot safely prefer
      the returned asset's `id` over the name it constructs. It now logs, once per build, when
      the two disagree, which is what a single live `invoke_power` with a radius turns into an
      answer. Write the return type down, then let `TryEnsureCircleBrush` prefer the real id and
      drop the guess. Same live pass as the `load_world` item above.
- [ ] **`GameRefs` caches members without their binding flags, and claims a consequence it
      cannot know.** `Field`, `Property` and `Method` all key on `$"{owner.FullName}.{name}"`
      with the flags left out, so two call sites asking for the same type and member under
      different flags silently share the first one's answer, including a cached null.
      `owner.FullName` is also null for some constructed types, which collapses every such
      lookup onto `".id"`. Nothing collides today, every live `Field` call site passes `Static`.
      Include the flags and a non-null identity in the key. While there, drop "Dependent
      commands disabled." from the three warnings: a missing member sometimes disables nothing,
      and the message is read by whoever is already debugging the wrong thing.
- [ ] **A cancelled request can be reported as a main-thread timeout.** The 60-second backstop in
      `HttpBridge` builds its timer from a token linked to the request token, so when that token
      is cancelled the timer task completes as cancelled, can win the `Task.WhenAny`, and the
      handler throws `TimeoutException` with a message about 60 seconds that did not elapse.
      Nothing in the handler catches `OperationCanceledException` either, so before #57 the same
      path produced a 500 `GAME_CRASH`. Both labels are wrong for "the bridge is shutting down".
      Cheap to fix by checking the token before deciding it was a timeout, and worth doing
      because the wrong label lands in the log of whoever is debugging a hang.
- [ ] **`save_world` can still stall a frame, and the load fix does not transfer.** The write
      that blocks is the game's own `SaveManager.saveWorldToDirectory`, which serializes the
      live world and writes it in one call, so it has to hold the main thread. Our own
      pre-invoke work there is `Path.GetFullPath` and two `MapBox` reads, no filesystem call, so
      there is nothing to move off-thread the way `load_world` moved its read. The residual is
      real but smaller: it needs an attacker who can plant a non-regular `map.wbox` at the
      destination, where `load_world` only needed a bad `path` argument. Fixing it properly
      means bounding the game's call, which nothing in net462 can do, or reimplementing the
      save format. Recorded rather than attempted.


## 💡 Not committed to

Ideas nobody has signed up for. The pointer that used to sit here, to a roadmap section in
CLAUDE.md, was dead: no such section exists, so the reasoning lives in each line below and in
the docs each one names.

- Single multi-tenant MCP server, so N agents no longer means N server processes.
- Report gate saturation in `/health`, which already carries `LastTick` and `UpdateCount`, so a
  `503 BUSY` can be explained rather than guessed at. `ConcurrencyGate` had an `Available`
  property for this and nothing in production read it, so #70 removed it rather than ship a
  number no shipped code proves. Adding it back means touching `/health`'s output, which is a
  documented surface and whose sample responses `gen-docs.py --check` verifies.
- Auto-resolve `kingdom_claim: "auto:N"` on first world load. On its own this buys nothing: #59
  established that a kingdom claim scopes reads and not writes, and that no Action command a
  FactionPlayer can reach even names a kingdom. Real PvP write scoping needs both this and a
  command that takes a kingdom. See the section in [multi-agent.md](docs/multi-agent.md).
- `get_actor(name_or_id)`, and `terraform(action_id, x, y, radius)`.
- Opt-in JSONL message log for replay and post-mortem.

The remaining power delegates (`click_brush_action`, `toggle_action`, `click_special_action`)
have left this list: PR #57 implements the brush and toggle ones. Review it against gotcha 7 in
[game-api-notes.md](docs/game-api-notes.md), which is where the delegate families are written up.
