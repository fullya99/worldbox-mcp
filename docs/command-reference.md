---
title: Command reference
---

# Command reference

<!-- gen-docs:begin total -->29<!-- gen-docs:end total --> MCP tools, grouped by six categories. Asset ids (tile / actor / power / speed) come
from the running game's registry, call the discovery tools to enumerate them; never
hardcode. The v0.3 multi-agent additions (Meta `whoami` / `session_info` /
`turn_advance` / `objective_status`, plus the Bus category) only carry meaningful payload
when an `agents.json` is deployed; in legacy single-token mode they return the synthetic
`"legacy"` god agent.

The agent can also dump this list at runtime by calling **`worldbox_capabilities`**, which
returns the live JSON-Schema for every registered command. That's the source of truth, this
page tracks it but may lag.

---

## Meta, basics + multi-agent introspection

| Tool | What it returns |
|---|---|
| `worldbox_health` | Plugin liveness + mod version + WorldBox version + Unity version + `Assembly-CSharp.dll` SHA256 + current tick + (v0.3) `multi_agent`, `scenario`, `agent_count`. **Call this first.** |
| `worldbox_capabilities` | The full live tool list with JSON-Schema. **Call this second** if you want to know what's actually available in the build you're talking to. |
| `worldbox_whoami` _(v0.3)_ | The caller's `agent_id`, `role`, `claimed_kingdom_id`, `permissions[]`, `scenario`, `partial_intel`. The third call every multi-agent client should make. |
| `worldbox_session_info` _(v0.3)_ | The full session: scenario, partial_intel + turn_based flags, agent roster (without tokens), `turn_order`, `current_turn`. |
| `worldbox_turn_advance` _(v0.3)_ | Ends the caller's turn in turn-based sessions. Returns `{previous, next, forced_by_god}`. 409 `TURN_NOT_YOURS` if it's not your turn. 400 `BAD_ARGS` if the session is not turn-based. |
| `worldbox_objective_status` _(v0.3)_ | Per-agent declared objectives + live kingdom-population snapshot. The scoreboard primitive, the agent computes its own score. |

---

## Discovery, what asset ids does this build accept?

| Tool | Returns |
|---|---|
| `worldbox_list_tiles` | All `TileType` ids (e.g. `sand`, `soil_high`, `lava0`, `deep_ocean`, ...). On stock 0.51.x: 20 entries. Inputs for `worldbox_paint_tile`. |
| `worldbox_list_actors` | All `ActorAsset` ids (`human`, `elf`, `dragon`, `wolf`, ...). 322 on stock 0.51.x. Inputs for `worldbox_spawn`. |
| `worldbox_list_powers` | All `GodPower` ids (spawn-by-race + disasters + toggles). 339 on stock 0.51.x. Inputs for `worldbox_invoke_power`. Items carry `supports_radius: true` when the power accepts `invoke_power`'s `radius` argument, and `is_toggle: true` for global on/off switches (x/y ignored); both flags are omitted when false. |
| `worldbox_list_speeds` | All `WorldTimeScaleAsset` ids with `multiplier`, plus `current`. 10 on stock 0.51.x (`slow_mo`, `x1`–`x5`, `x10`, `x15`, `x20`, `x40`). Inputs for `worldbox_set_speed`. |

---

## Action, modify the world

| Tool | Args | Effect |
|---|---|---|
| `worldbox_invoke_power` | `power_id`, `x`, `y`, `radius?`, `pulses?`, `x2?`, `y2?` | Universal, every spawn-by-race, every disaster, the drops/bombs families (`rain`, `fire`, `bomb`, `volcano`, ...), brush tools and global toggles. Optional `radius` (1-50) applies brush-driven powers (`supports_radius` in `list_powers`) over a circular area; `radius` on any other power returns `GAME_REJECTED`. Optional `pulses` (1-200) applies the power once per game frame that many times, the equivalent of holding the mouse button (~60/s); with `x2`/`y2` the pulses sweep from (x, y) to (x2, y2) like a click-hold-drag. Without `radius`, brush-only powers run at a minimal radius-1 brush and `is_toggle` powers flip their global state (x/y ignored but must be in-bounds; `pulses` rejected). Returns `{accepted: bool, via}` plus `{radius, brush}` when a brush was used; multi-pulse calls return `{pulses, accepted_count, pulses_applied}` instead of `accepted`, take pulses/60 seconds, and stop early with `stopped: turn_ended \| world_changed \| deadline \| error` when the caller's turn ends, the world is replaced mid-run, the 25 s budget runs out, or a pulse throws (`error_code`/`error_message` then carry the failure). `via` is the game delegate driven (`click_action`, `click_power_action`, `click_brush_action`, `click_power_brush_action` or `toggle_action`); `accepted=false` means the game declined this time (some powers roll a chance). Powers needing live mouse state (`finger`) return `GAME_REJECTED` with a reason. Requires the global action scope (God role) in a multi-agent session, same as `worldbox_paint_tile`; a FactionPlayer uses `worldbox_spawn`. |
| `worldbox_spawn` | `entity_id`, `x`, `y`, `count=1`, `adult=false`, `spawn_height=6` | Spawn actors **not exposed as GodPowers**, `dragon`, `cthulhu`, `kraken`, specific animals. Wild kingdom auto-assigned via `ActorAsset.kingdom_id_wild`. |
| `worldbox_paint_tile` | `x`, `y`, `tile_id?`, `top_id?`, `radius=0` | Paints a Euclidean disc. Provide `tile_id` (ground), `top_id` (decoration), or both. Out-of-map cells silently skipped. |

---

## Read, observe the world

| Tool | Returns |
|---|---|
| `worldbox_get_world_state` | `{width, height, seed, tick, paused, population_alive, population_lifetime, kingdoms_alive, kingdoms_ever_created, cities_alive, cities_ever_created}` |
| `worldbox_get_tile` | `{x, y, tile_id, top_id, height, actors[], actor_count}` |
| `worldbox_list_kingdoms` | Alive kingdoms (filter via `include_wild`): `{id, name, race, king_name, capital_id, cities_count, units_count, wild}`. |
| `worldbox_list_cities` | Alive cities (optional `kingdom_id` filter): `{id, name, kingdom_id, kingdom_name, leader_name, building_count, unit_count}`. |
| `worldbox_query_actors` | Filtered actor list: args `{race?, kingdom_id?, in_rect?, alive=true, limit=500, offset=0}`. Default limit 500, max 5000. Pagination via `offset` + `has_more`. |
| `worldbox_get_ui_state` | `{window_active, current_window, config_paused, effective_paused, world_loading}`. `current_window` is the open window's id (`welcome` = startup screen). `effective_paused` is what the simulation does: `config_paused` OR any window open. `world_loading` is true while a world is generating/loading (`save_world` refuses then). |
| `worldbox_screenshot` | Args `{max_dimension=1280, format="jpg"(default)\|"png", quality=80}`. Returns an MCP image content block plus `{format, width, height, source_width, source_height, quality, bytes}`. Longest edge is downscaled to `max_dimension` (0 = full frame); a 1280 px JPEG is ~150-250 KB, a full Retina PNG ~2.8 MB. Last completed frame. |

---

## Control, simulation flow + world lifecycle

| Tool | What for |
|---|---|
| `worldbox_pause` | Freeze the world before building a scenario. Returns `previous_paused` so you can detect state change. |
| `worldbox_resume` | Unpause. |
| `worldbox_dismiss_window` | Closes any open in-game window (startup `welcome` screen, settings, info panels, confirmations) via the game's own `ScrollWindow.hideAllEvent`. Returns `{dismissed, window}`. Gated on `AdvanceTime` like pause/resume, but unlike them it is exempt from the turn gate in `turn_based` sessions: an open window blocks everyone, so any agent holding the permission can clear it. |
| `worldbox_set_speed` | Pass a `WorldTimeScaleAsset` id from `worldbox_list_speeds` (`slow_mo`, `x1`, `x2`, `x3`, `x4`, `x5`, `x10`, `x15`, `x20`, `x40`). Higher = simulation runs faster than real time. Returns `{speed_id, multiplier, previous}`; unknown ids get `UNKNOWN_ASSET` listing every valid id. |
| `worldbox_generate_world` | Wipes the world and regenerates a map. Optional `zone_x`/`zone_y` (each zone = 64 tiles). Async, poll `get_world_state` until `tick` advances. |
| `worldbox_save_world` | Required `folder`: absolute path, or a name resolved under the game's `saves/` directory (in-game slots are `save1`, `save2`, ...; `..` is rejected). Returns the resolved `path`. Save format compatible with the in-game load UI. Refuses if no world loaded. |
| `worldbox_load_world` | One of `path` (save file, save folder, or a name under `saves/` such as `save1`) or `bytes_b64` (base64 zipped save). `bytes_b64` wins if both are given. Async. The response reports what was actually read: `source` is `path` or `bytes_b64`, and `path` carries the resolved file when one was read, `null` otherwise. The file is read off the Unity main thread, and refused before it is opened if it is not a regular file or is larger than the ceiling in `SaveFileReader.DefaultMaxBytes`: `path` accepts absolute paths, and reading an arbitrary one during a frame used to be able to freeze the game outright. A save being written at that moment comes back `GAME_REJECTED`, retry once the write finishes. |

---

## Bus, inter-agent messaging (v0.3+)

| Tool | What for |
|---|---|
| `worldbox_send_message` | Send to another agent's inbox, or `to="*"` to broadcast. Args `{to, content, kind?}`. Returns `{seq, recipients, broadcast}`. Broadcast requires `send_broadcast` permission (god / narrator only). Unknown recipient -> 400 with a helpful "Known: [...]" list. |
| `worldbox_recv_messages` | Poll the caller's inbox. Args `{since_seq=0, max=50}` (hard ceiling 500). Non-destructive, pass the previous response's `next_cursor` as `since_seq` to avoid re-reading. Returns `{items: [{seq, from, to, kind, content, sent_utc}], count, next_cursor}`. |

Inboxes are bounded (default 200 per agent, drop-oldest). Sequence numbers are bus-wide
and monotonic, one cursor works across recipients and across reconnects. Nothing is
persisted to disk in v0.3.

---

## Error model

Every error response follows this shape:

```json
{
  "ok": false,
  "error": {
    "code": "<UPPERCASE_CODE>",
    "message": "<human-readable>",
    "command": "<name>",
    "args": { ... },
    "did_you_mean": ["..."],   // only for UNKNOWN_ASSET
    "exception": { "type": "...", "message": "...", "stack_top": "..." }
  }
}
```

| Code | Meaning |
|---|---|
| `UNKNOWN_COMMAND` | No tool with that name. Call `worldbox_capabilities`. |
| `BAD_ARGS` | JSON shape doesn't match the tool's schema, or a command rejected an argument value (a missing coordinate, a count out of range, an unresolvable save path). Commands raise it explicitly, so a fault inside the bridge or the game is never reported as one. |
| `UNKNOWN_ASSET` | An asset id wasn't found in the live registry. Use the included `did_you_mean` (Levenshtein top-5) to self-correct. |
| `OUT_OF_BOUNDS` | `(x, y)` outside the current map dimensions. |
| `GAME_REJECTED` | Game logic refused the action (e.g. spawning a land animal on water, a brush-only power, `save_world` while the world is still loading). |
| `GAME_CRASH` | Game-side exception. `exception` field carries full type + message + stack top. Argument validation does not land here: commands raise `BAD_ARGS` directly rather than throwing, so a 500 means the bridge or the game actually broke. |
| `PERMISSION_DENIED` _(v0.3)_ | The agent's role lacks the permission this command requires. HTTP 403. |
| `FACTION_SCOPE_VIOLATION` _(v0.3)_ | Reserved, currently never raised. A kingdom claim scopes reads, not writes, see [multi-agent.md](multi-agent.md#a-kingdom-claim-scopes-reads-not-writes). HTTP 403. |
| `TURN_NOT_YOURS` _(v0.3)_ | Turn-based mode is active and another agent currently holds the slot. HTTP 409. |
| `MAIN_THREAD_TIMEOUT` | A command waited more than 30s for a Unity frame and was dropped before it ran. The deadline is checked before the action starts, so it never abandons work already under way. |
| `UNAUTHORIZED` | Missing or wrong bearer credential (either `Authorization: Bearer <token>` or the legacy `X-WB-Token: <token>`). Should never happen if `worldbox-mcp` auto-discovered your config. |
| `DISABLED` | `enabled = false` in `BepInEx/config/WorldBoxBridge.cfg`. The kill-switch is engaged. |
| `BUSY` | The bridge already runs as many commands at once as it admits (`max_concurrent_requests`, default 8), or the dispatcher's per-frame job registry is full. Safe to repeat: refused at admission nothing ran, and the one thing a job-registry refusal can have done already is clone a brush asset, which is keyed by name. HTTP 503, see [protocol.md](protocol.md#concurrency-limit). |

---

## Discovery first, action second

The fundamental design choice: rather than hardcoding ~200 command variants per asset type,
three action primitives accept a string id resolved at runtime against the game's own
registry. **Always call the discovery tools to learn what's valid in this build**, they're
your version-proof contract:

```text
list_tiles()  → [{id, color_hex?, has_biome_tags?}, ...]
list_actors() → [{id, race?, asset_type?}, ...]
list_powers() → [{id, tab_id?, target_type?, supports_radius?, is_toggle?}, ...]
```

Asset ids returned here are valid inputs for the action primitives **in the same session**.
After a WorldBox update, re-call them, new dragons / tiles / disasters may have arrived.
