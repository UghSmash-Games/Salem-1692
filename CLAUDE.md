# Salem 1692 — Digital Adaptation

## Project Architecture

Three-part project: Unity host, Node.js server, and web client (player phones + mirror screens).
Unity project at repo root, server and web client alongside it.

REPO ROOT
├── Assets/          ← Unity game content (scripts, art, prefabs)
├── Packages/        ← Unity packages
├── ProjectSettings/ ← Unity project settings
├── server/          ← Node.js + Socket.io (message relay)
├── webclient/       ← React (player phones + mirror screens)
├── docs/            ← Rulebook PDF and development guide
├── CLAUDE.md
└── .claude/

NOTE: The unity/ folder at root contains only Unity-generated files (Library,
Logs, UserSettings) and can be ignored. The working Unity project is at the
repo root — open Unity Hub and point it to D:\github\Salem-1692 directly.

## Build & Run Commands

```bash
# Server
cd server && npm install && npm run dev
cd server && npm test

# Web client (once created)
cd webclient && npm install && npm run dev
cd webclient && npm test

# Unity
# Open Unity Hub → Add project from disk → D:\github\Salem-1692 (repo root)
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

## Game Rules Gotchas

- A player who loses their witch card is still a witch for the remainder of the game
- Win conditions must be checked after every tryal reveal, elimination, AND conspiracy
- Accusations do not carry over after a tryal is revealed
- Scapegoat/Robbery are disabled when only 2 players remain
- Matchmaker elimination chain fires even if the second player confessed or was saved
- Multiple witch cards: player is not eliminated until their LAST witch card is revealed

## Known Bugs in Unity Alpha

Verified against active code 2026-06-13. The original alpha bug list was mostly
stale — only the dawn witch-reveal remains. Still broken:

- Dawn does NOT reveal witches to each other — `//TODO` stub in
  `GamePhaseManager.StartDawnPhase` (GamePhaseManager.cs:175). (Black Cat
  placement IS implemented; dawn does NOT skip to Day.)
- Tituba's deck rearrange is stubbed to a plain shuffle (GameTurnManager.cs).

Already fixed (do NOT re-budget in Phase 4): asylum is checked at resolution
(NightResolver.cs:50); the matchmaker cascade is central and fires from night
kills (PlayerService.Eliminate, PlayerService.cs:104-114 ← NightResolver.cs:91);
a confession round exists (GamePhaseManager.ExecuteConfessionRound).

## What Phase 4 Actually Needs (networked night & dawn)

The real Phase 4 work is an architecture conversion, not bug-fixing. The active
flow (GamePhaseManager → GameTurnManager → AIPlayer; everything in `_Archive/`
is dead) assumes a SINGLE local human and drives all secret-phase input through
local-UI callbacks (`WaitUntil(flag)`). To networked multiplayer:

- Drop the single-local-player model (PlayerService.cs:56-59); add a
  playerId ↔ Player registry.
- Add an input abstraction over the local-UI callback seams
  (`TableLayoutController.BeginTargetSelection`/`BeginTryalSelection`,
  `ConfessionChoiceUI.Open`, `GameTurnManager.waitingForHuman`) with a network
  impl that emits `action_request`/`secret_phase_prompt` and resolves from
  `NetworkManager` events. `NetworkManager` exists but is an unconnected bridge.
- Implement the `acting`-flag masking: prompt ALL players, discard non-acting
  submits. Today only the local witch/constable is prompted; other witches get
  RANDOM targets (NightResolver.cs:64-70) — collect all witch votes into
  `plan.WitchVotes` instead.
- Make witch vote + constable save resolve in parallel; rework confession into a
  masked, simultaneous, timed window.
- Emit `phase_resolve` for synchronized reveals (Unity reveals immediately now)
  and add per-phase timeouts (only a Day idle timer exists).

## Phase 4 Implementation Notes

- Idle timer = INACTIVITY window (not cumulative turn time): `GameTurnManager`
  re-arms `turnTimer` on each action (`NotifyCardPlayed`, `ResetIdleTimer`, and
  `NetworkInput` on each `player_action`). On timeout it ends the turn if the
  player already played a card, else forces Draw 2 (`HandleIdleTimeout`).
- Turn cancellation: `GameTurnManager.TurnId` bumps each `StartTurn`. Async inputs
  capture it and exit when it changes. This RESOLVES the earlier orphan-seat /
  parked-`WaitUntil` risk — a `NetworkInput` coroutine whose turn is force-ended
  (idle timeout, etc.) now wakes on `TurnId != myTurnId`, breaks, and unsubscribes
  instead of blocking forever. (Still wire real per-seat disconnect handling
  post-4a, but the coroutine no longer leaks.)

## Testing

Run server tests before any PR: `cd server && npm test`
Run webclient tests before any PR: `cd webclient && npm test`
Always test the `acting: false` path — confirm submissions are discarded silently
and the confirmation animation still plays.

## Branch Naming

`phase-N-description` for phase work (e.g. `phase-2-phone-client`).
`fix/description` for bug fixes. PRs require reviewer sign-off before merge to main.

## See Also

- Development guide: @docs/Salem_1692_Development_Guide.md
- Rulebook: @docs/Salem_Rulebook.pdf
- Socket protocol spec: @docs/protocol.md
