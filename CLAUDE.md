# Salem 1692 — Digital Adaptation

## Project Architecture

Three-part project: Unity host, Node.js server, and web client (player phones + mirror screens).

/unity      → Unity project (game logic host)
/server     → Node.js + Socket.io (authoritative message relay)
/webclient  → React (player phones + mirror screens)
/docs       → Rulebook PDF and development guide

## Build & Run Commands

```bash
# Server (once created)
cd server && npm install && npm run dev
cd server && npm test

# Web client (once created)
cd webclient && npm install && npm run dev
cd webclient && npm test

# Unity
# Open unity/ in Unity Hub — D:\github\salem-1692\unity\
```

## Critical Architecture Rules

**Client roles** — every socket connection declares one of three roles at join:
- `host` — Unity only; sends authoritative game state, receives player actions
- `mirror` — passive browser display; receives public state only, never player prompts
- `player` — phone client; receives private state + masked prompts, sends actions

**The server enforces roles.** A mirror that sends `player_action` is silently ignored.
Never rely on the client to self-police.

**The `acting` flag** — during secret phases (dawn/night), every player receives an
identical `secret_phase_prompt`. Witches/constable get `acting: true`; all others get
`acting: false`. The server processes submissions only from `acting: true` players.
The phone client renders the same UI regardless. This is the identity masking system
— do not break this pattern under any circumstances.

**Private state isolation** — tryal cards and role (witch/constable) are NEVER sent
to the host screen, mirror screens, or other players' phone clients. Enforced at the
server dispatch layer, not the UI layer.

## Socket Event Names (source of truth)

Room management:         create_room | join_room | join_mirror
                         room_created | joined | player_joined | room_closed

Server → clients:        game_state_update | private_state | secret_phase_prompt
                         action_request | phase_resolve | elimination_result | game_over

Client → server:         player_action | secret_phase_submit | confess

NOTE: game_state_update is passed through by the server without inspection.
Unity is solely responsible for ensuring it never contains private player data
(tryal cards, role, acting flag).

`phase_resolve` carries a UTC timestamp — both Unity and mirror screens animate
reveals at the same wall-clock moment. Do not trigger reveal animations on
message receipt.

## Game Rules Gotchas

- A player who loses their witch card is still a witch for the remainder of the game
- Win conditions must be checked after every tryal reveal, elimination, AND conspiracy
- Accusations do not carry over after a tryal is revealed
- Scapegoat/Robbery are disabled when only 2 players remain
- Matchmaker elimination chain fires even if the second player confessed or was saved
- Multiple witch cards: player is not eliminated until their LAST witch card is revealed

## Testing

Run server tests before any PR: `cd server && npm test`
Run webclient tests before any PR: `cd webclient && npm test`
Always test the `acting: false` path — confirm submissions are discarded silently
and the confirmation animation still plays.

## Branch Naming

`phase-N-description` for phase work (e.g. `phase-1-architecture`).
`fix/description` for bug fixes. PRs require reviewer sign-off before merge to main.

## See Also

- Development guide: @docs/Salem_1692_Development_Guide.md
- Rulebook: @docs/Salem_Rulebook.pdf
