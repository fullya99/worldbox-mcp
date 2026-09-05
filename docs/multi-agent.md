---
title: Multi-agent sessions
---

# Multi-agent sessions (v0.3)

Put **several AI agents in the same WorldBox world**. Two rival kingdoms duking it out? One
ecology-AI cooperating with a civilization-AI? A DM-driven roleplay with two players and a
narrator? All four work through the same session layer, only the config differs.

The bridge keeps a per-process registry of authenticated agents. Each agent has its own
bearer token, role, optional kingdom claim, and inbox. Every command that modifies or
observes the world receives a `RequestContext` carrying that identity, so the bridge can
gate actions and scope reads per agent.

## <!-- gen-docs:begin total-words -->Twenty-nine<!-- gen-docs:end total-words --> MCP tools, six categories

v0.3 added **six new tools** on top of the v0.2 set, and the existing twenty stay
backwards-compatible. v0.4 adds three more (`list_speeds`, `get_ui_state`, `dismiss_window`).
Total surface = <!-- gen-docs:begin total -->29<!-- gen-docs:end total -->.

| Category | Tools | New in v0.3 |
|---|---|---|
| Meta | `worldbox_health`, `worldbox_capabilities`, `worldbox_whoami`, `worldbox_session_info`, `worldbox_turn_advance`, `worldbox_objective_status` | 4 |
| Discovery | `worldbox_list_tiles`, `worldbox_list_actors`, `worldbox_list_powers`, `worldbox_list_speeds` | — |
| Action | `worldbox_invoke_power`, `worldbox_spawn`, `worldbox_paint_tile` | — |
| Read | `worldbox_get_world_state`, `worldbox_get_tile`, `worldbox_list_kingdoms`, `worldbox_list_cities`, `worldbox_query_actors`, `worldbox_get_ui_state`, `worldbox_screenshot` | — |
| Control | `worldbox_pause`, `worldbox_resume`, `worldbox_dismiss_window`, `worldbox_set_speed`, `worldbox_generate_world`, `worldbox_save_world`, `worldbox_load_world` | — |
| Bus | `worldbox_send_message`, `worldbox_recv_messages` | 2 |

## Four scenario presets

The same architecture supports four interaction shapes, picking one is a JSON edit, not a
code change. Side-by-side examples live in `examples/scenarios/multi-agent/`.

| Scenario | What it means | Distinguishing |
|---|---|---|
| **PvP** | N rival factions, one per agent | `partial_intel: true` (fog-of-war), agents bound to their own kingdom, mutual "wipe kingdom" objectives |
| **Coop** | N AIs co-managing the same world | `partial_intel: false`, no claims, message bus is central, "I just triggered a famine, you handle the migration response" |
| **Hierarchical** | 1 god + N players + (optional) narrator | Mixed roles, partial_intel on, narrator broadcasts colorful summaries |
| **Sandbox** | N co-gods, no constraints | Closest to legacy v0.2 mode but with per-agent inboxes |

## Quick start, PvP at two agents

1. **Generate two bearer tokens** (48 alphanumeric chars each). PowerShell snippet:

    ```powershell
    $alphabet = [char[]]([char]'A'..[char]'Z' + [char]'a'..[char]'z' + [char]'0'..[char]'9')
    1..2 | ForEach-Object {
      $bytes = New-Object byte[] 48
      [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
      -join ($bytes | ForEach-Object { $alphabet[$_ % $alphabet.Count] })
    }
    ```

2. **Drop an `agents.json`** at `<worldbox>/BepInEx/config/WorldBoxBridge.agents.json`:

    ```json
    {
      "scenario": "pvp",
      "partial_intel": true,
      "turn_based": false,
      "agents": [
        {
          "id": "athena",
          "token": "<token-1>",
          "role": "faction_player",
          "kingdom_claim": "auto:0",
          "objectives": [
            { "id": "dominate", "label": "Wipe ares", "kind": "wipe_kingdom", "target": "auto:1" }
          ]
        },
        {
          "id": "ares",
          "token": "<token-2>",
          "role": "faction_player",
          "kingdom_claim": "auto:1",
          "objectives": [
            { "id": "dominate", "label": "Wipe athena", "kind": "wipe_kingdom", "target": "auto:0" }
          ]
        }
      ]
    }
    ```

3. **Wire each agent in their MCP client** with their own bearer. Two options:

    Stdio path, spawn one `worldbox-mcp` process per agent:

    ```powershell
    # Terminal 1 (athena)
    $env:WORLDBOX_MCP_TOKEN = '<token-1>'
    uvx worldbox-mcp
    ```

    HTTP path, share one MCP server across many clients:

    ```powershell
    # Once, anywhere:
    uvx worldbox-mcp --http --host 127.0.0.1 --port 8724

    # Then in each AI client:
    claude mcp add worldbox-athena --transport http http://127.0.0.1:8724/mcp `
      --header "Authorization: Bearer <token-1>"
    ```

4. **Verify** with `worldbox_whoami`, each agent sees their own identity, role, and
   claimed kingdom.

## Roles and permissions

Four roles, each with a default permission bitmask. The roles map onto the four scenarios
naturally: pick the one that fits your agent.

| Role | Read all | Read own faction | Action global | Action faction | Control world | Advance time | Send msg | Recv msg | Broadcast |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| **God** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **FactionPlayer** |   | ✓ |   | ✓ |   | ✓ | ✓ | ✓ |   |
| **Observer** | ✓ | ✓ |   |   |   |   | ✓ | ✓ |   |
| **Narrator** | ✓ | ✓ |   |   |   |   | ✓ | ✓ | ✓ |

**Permission scope**:

- `ActionGlobal`: terraforming, anything that's truly map-wide (paint_tile, etc.).
- `ActionFaction`: actions an agent can perform on/from their kingdom (spawn, invoke_power).
- `ControlWorld`: **destructive** world-lifecycle ops (generate_world wipes everything, save_world / load_world rewrite state). God-only on purpose.
- `AdvanceTime`: **non-destructive** simulation-flow controls: pause, resume, set_speed, dismiss_window. Granted to active-player roles so PvP agents can fast-forward through quiet phases (`worldbox_set_speed("x20")`) or pause to think without needing a god agent in the session. Spectator roles (Observer, Narrator) intentionally do NOT have it, they shouldn't be able to skip ahead while the actual players are still deliberating.

Every command declares the permission it needs via `ctx.Require(...)` at the top of its
`ExecuteAsync`. Missing permission returns HTTP 403 with `code: PERMISSION_DENIED`. Cross-
faction action attempts return HTTP 403 with `code: FACTION_SCOPE_VIOLATION`.

## Fog-of-war (partial_intel)

When `partial_intel: true` in the scenario AND the agent doesn't have `ReadAll`:

- `worldbox_list_cities` filters to the agent's claimed kingdom.
- `worldbox_query_actors` filters to the agent's claimed kingdom.
- `worldbox_screenshot` is rejected (`PERMISSION_DENIED ReadAll`), otherwise an agent
  could bypass fog by snapping a picture.
- `worldbox_list_kingdoms` is **intentionally NOT filtered**, agents need to know the
  enemy factions exist for diplomacy and threat awareness.

`worldbox_objective_status` is also not faction-filtered: scoreboard metrics need to be
visible to the agents that are competing on them.

## Turn-based mode

Add `"turn_based": true` to the scenario. Optional `"turn_order": ["athena", "ares"]`
explicitly orders the rotation; otherwise rotation = agents in declaration order.

Behavior:

- Only the current-turn agent may issue **Action** or **Control** commands. Other agents
  attempting them get HTTP 409 `TURN_NOT_YOURS`.
- One exception: `worldbox_dismiss_window` stays open to every agent that holds
  `AdvanceTime`, whoever has the turn. An open window freezes the simulation for the whole
  session, so clearing it is a shared unblock and not a move, and no command can open one in
  the first place. Observer and Narrator still cannot call it, they do not have the
  permission.
- **Read / Discovery / Meta / Bus** commands stay open. You can always check the state and
  message peers, regardless of whose turn it is.
- `worldbox_turn_advance` ends your turn. The next agent in `turn_order` becomes active.
- God-role agents (`Permission.ActionGlobal`) **bypass the gate**, useful for the
  hierarchical scenario where a DM watches over a turn-based PvP.

## Inter-agent messaging

`worldbox_send_message` + `worldbox_recv_messages` give agents an in-memory pub-sub. Each
agent has a bounded inbox (default 200 messages, drop-oldest on overflow). Sequence
numbers are bus-wide so a single `since_seq` cursor works across recipients.

Broadcast (`to: "*"`) requires `Permission.SendBroadcast`, God and Narrator roles only.
FactionPlayers can only send 1-to-1 (no propaganda spam to opponents).

```python
await client.call("send_message", {"to": "ares", "kind": "diplomacy", "content": "truce?"})
inbox = await client.call("recv_messages", {"since_seq": last_seen_seq})
```

## Objectives

Each agent can declare `objectives: [{id, label, kind, target}]`. The bridge stores them
verbatim but does **not** compute scores, interpretation lives client-side, where the AI
has the scenario context to know whether "maximize pop of kingdom 3" is competitive (for
you, against you) or cooperative.

`worldbox_objective_status` returns:

- Every agent's declared objectives (free-form `kind`, `target` strings).
- A live kingdom-population snapshot: `{id, name, units, cities, wild}` per alive kingdom.

The combination is enough for an agent to evaluate "am I winning?". The bridge stays out
of the scoring loop, which keeps it robust across WorldBox updates, there's no
game-mechanics evaluator that could break when the game patches.

## Backwards compatibility

If `WorldBoxBridge.agents.json` is **absent**, the bridge falls back to legacy single-token
mode:

- One agent named `"legacy"`, role `God`, no kingdom claim, full permissions.
- Token = `BridgeConfig.Token` (the existing per-install random secret from
  `WorldBoxBridge.cfg`).
- All v0.1 / v0.2 single-tenant clients continue to work without changes.

The legacy header `X-WB-Token: <token>` is also still accepted, in addition to the v0.3
`Authorization: Bearer <token>`. Mixing is fine, the bridge tries Bearer first, falls
back to X-WB-Token.

## Wire format reference

| Header | Value | When |
|---|---|---|
| `Authorization` | `Bearer <agent-token>` | v0.3 multi-agent, the preferred path |
| `X-WB-Token` | `<token>` | v0.1 / v0.2 legacy clients, still accepted |

New error codes (HTTP status + JSON `code`):

| Code | Status | Meaning |
|---|---|---|
| `PERMISSION_DENIED` | 403 | The agent's role lacks the permission this command requires. |
| `FACTION_SCOPE_VIOLATION` | 403 | The agent tried to act on a kingdom it doesn't claim. |
| `TURN_NOT_YOURS` | 409 | Turn-based mode is active and another agent holds the current slot. |

## End-to-end smoke

`examples/scenarios/multi-agent/pvp_smoke.py` walks the canonical PvP flow with two
`BridgeClient` instances against a single running bridge. Run it after deploying the PvP
preset to verify your setup:

```powershell
uv --project server run python examples/scenarios/multi-agent/pvp_smoke.py
```

Expect: whoami x2, session_info, message round-trip with `since_seq` cursoring, 4
permission rejections, objective_status payload, then "all checks passed".
