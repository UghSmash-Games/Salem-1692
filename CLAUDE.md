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
`acting: false`. The host processes submissions only from `acting: true` players.

**Masking definition (canonical).** Masking means the **prompt, the target controls,
the two-stage tentative→Confirm flow, and the timing are identical for every player**.
An observer cannot identify who is acting from screen structure, controls, or
interaction timing. Witch-only coordination data — fellow-witch identities and the
live tentative tally — is **private-channel information of the same class as tryal
cards** (`private_state`, routed to one socket, never broadcast); it is shown only on
a witch's own device and legitimately differs per phone, exactly like a player's
tryals/role/hand. Do not break this pattern: never let a phone become structurally
distinguishable by role (layout, controls, flow, or timing), and never put witch
coordination data on any broadcast/public channel.

**Public vs secret info — the masking line (which UI pattern to use).** Masking-with-identical-controls
is reserved for genuinely SECRET information: witch/constable identity, tryal cards, and whether a player
is *acting* this round. What is NOT secret: **Town Hall character identity is PUBLIC** — cards are dealt
face-up and each ability is read aloud to the group at setup. So a character's ability CONTROL may be shown
only on that holder's own phone, **host-gated per-player** (the host computes eligibility and sends a
per-player flag, e.g. the `action_request` `actions` array for Tituba/Parris, or `canFakeConfess` on the
per-player `secret_phase_prompt` for William Phipps). A holder-only button leaks nothing an opponent
doesn't already know. Do NOT reach for the universal-control-rendered-on-every-phone-with-silent-server-
discard pattern (like the `acting:false` night-vote) for a Town Hall ability — that pattern exists to hide
SECRET acting status, and using it for public character abilities is solving a problem that doesn't exist.
Rule of thumb: **secret status → universal control + server discard; public identity → host-gated
per-player control.** (Both still enforce the EFFECT server-side — never trust the client — but the
VISIBILITY differs.) The base secret-phase interaction (the shared confess/skip or target choice) stays
identical for everyone regardless; only a public-identity extra control may be role-conditional.

**Private state isolation** — tryal cards and role (witch/constable) are NEVER sent
to the host screen, mirror screens, or other players' phone clients. Enforced at the
server dispatch layer, not the UI layer.

## Socket Event Names (source of truth)

Room management:         create_room | join_room | join_mirror
                         room_created | joined | player_joined | room_closed

Server → clients:        game_state_update | private_state | secret_phase_prompt
                         action_request | confirm_request | target_request
                         phase_resolve | public_reveal | elimination_result | game_over

Client → server:         player_action | secret_phase_submit | confess
                         confirm_submit | target_submit

NOTE: game_state_update is passed through by the server without inspection.
Unity is solely responsible for ensuring it never contains private player data
(tryal cards, role, acting flag).

## Win Conditions (canonical — hard invariant, same weight as the masking model)

**Salem has NO parity win condition.** This is a core rules fact — treat it like the masking model.

- **Witches win ONLY when `nonWitches == 0`** — every alive player is a witch (≥1). This single check
  covers BOTH rulebook triggers: "witches eliminate all townspeople" AND "the final remaining
  townsperson becomes a witch" (that player now holds a witch tryal → `IsWitch` → no longer a
  non-witch). When the win fires because the last townsperson JUST turned (via conspiracy), **that
  player LOSES** — they are excluded from the winning witch team (rulebook: "in which case that player
  loses").
- **Townspeople win when the final "witch" tryal card is REVEALED** (`!anyUnrevealedWitch`),
  independent of how many witches are alive or eliminated. Reveal beats elimination count; this is
  checked FIRST so it wins any tie.
- **There is NO "witches win at parity/majority" rule.** `witches >= nonWitches` was a Werewolf/Mafia
  import (a real bug that ended games early — e.g. 1 witch vs 1 live townsperson falsely declared a
  witch win). A full rulebook search for parity/outnumber/majority returns zero hits. **Never
  reintroduce it.** The one authority is `GameManager.WitchesControl(aliveSet)`, used by both
  `EvaluateEndGame` and `CascadeWouldEndBothTeams` (Mary Warren's both-teams-lose guard).

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
a masked/timed confess window exists (GamePhaseManager.RunConfessWindow — 4c
replaced the legacy local ExecuteConfessionRound).

⚠️ CORRECTION (Phase 5): the Phase-4 "asylum checked at resolution" verification was
true about the resolution LOGIC but WRONG in practice — asylum (and matchmaker, stocks,
scapegoat, piety) never actually attached, so they were never exercised. Root cause: a
card-DATA bug, not code — every blue/persistent card SO had `Op: 0` (= ActionOp.Accusation,
the enum's default), so playing them routed to the Accusation handler and never reached
`PlayStatusCardOnTarget`/`AddStatusCard`. The effect map (CardEffectManager `_ops`),
`PlayStatusCardOnTarget`, and the piety/asylum threshold/flag logic were all correct.
Fixed by setting each SO's `Op` to its real value (Piety 13, Asylum 11, Matchmaker 12,
Stocks 1, Scapegoat 10, Black Cat 5 — Black Cat was insulated via name-handling at draw).
**Asylum night-immunity and matchmaker (linking + the cascade exceptions) must be
RE-VERIFIED now that cards actually attach** — this directly affects Mary Warren (#7,
matchmaker-based). Lesson: dispatch keys on `ActionCardSO.Op`, but several places match by
`Card.Name` — a card needs BOTH its `Op` and `Name` correct.

## Phase 4 — Networked Night & Dawn (COMPLETE: 4a / 4b / 4c)

Phase 4 was an architecture conversion (not bug-fixing): the active flow
(GamePhaseManager → GameTurnManager → AIPlayer; everything in `_Archive/` is dead)
used to assume a SINGLE local human driving secret-phase input through local-UI
callbacks. All of Phase 4 is now built and verified end to end:

- **4a** — dropped the single-local-player model; added the `PlayerService.byNetworkId`
  registry and the `IPlayerInput` abstraction (`LocalUIInput`/`NetworkInput`) over the
  former local-UI callback seams. Networked Day turns + inactivity idle timer +
  `GameTurnManager.TurnId` coroutine cancellation.
- **4b** — `acting`-flag masking: prompt ALL players, discard non-acting submits; all
  witch votes collected into `plan.WitchVotes` (NightResolver random-fill is a safety
  net only); two-stage tentative→Confirm; witch coordination (fellow witches + live
  tally) over `private_state`; two-round night (witch vote → constable save).
- **4c** — closed the masking-timing leak, added per-phase timeouts, synchronized
  reveals via `phase_resolve`, and the masked/timed confess window (see next section).

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
- MASKING-TIMING LEAK — CLOSED in 4c. Secret-phase rounds used to advance on "all
  ACTING players confirmed", letting an observer exclude the tardiest from being
  witches. Now the shared `AwaitAllConfirmedOrTimeout` predicate (the ONE place the
  wait/timeout lives) resolves only when EVERY connected human has Confirmed, or a
  uniform per-phase timeout fires — so timing reveals nothing about who acted. A
  mid-phase disconnect drops that seat from the wait set (`Player.IsConnected`,
  set from `NetworkManager.OnPlayerLeft`) so a dropped human can't stall the phase.
- Per-phase timeouts (4c): configurable `[SerializeField]` windows on GamePhaseManager
  (`dawnTimeout` 30 / `witchVoteTimeout` 45 / `constableTimeout` 30 / `confessTimeout`
  20). On timeout, resolve with whatever was recorded (existing safety nets: random-fill
  un-voted witches; no gavel if the constable didn't act). The Day idle timer (60s) is
  separate and only runs during Day (`isTurnActive`), so the two never overlap.
- Synchronized reveal (4c): `EmitSynchronizedReveal` emits `phase_resolve { revealAt =
  now + revealLeadSeconds }`, then DEFERS the whole reveal (model mutation included) to
  revealAt so the host screen, mirrors, and phones all flip together. Win conditions are
  checked at revealAt before the result is sent (reveal-then-`game_over` preserved); the
  reveal uses REALTIME waits because `pauseOnGameEnd` sets `Time.timeScale = 0`. Night
  elimination reveals route through `RevealTryalCard`→`TrialService` (multiple-witch-card
  rule reused). `NightResolver.Resolve` now returns a `NightOutcome` instead of
  eliminating inline.
- Masked confess window (4c): `RunConfessWindow` — every phone shows the same confess
  prompt; a confession reveals one of the player's OWN tryals for immunity, but the
  reveal is DEFERRED to the synchronized revealAt (timing masked during the window; the
  flip itself is PUBLIC, per the rulebook). Immunity via `plan.Confessors` is unchanged.
  Phone confess UI is the `confess` variant of `SecretPhaseScreen` (renders own face-down
  tryals + "don't confess"; selection = tryal index or `skip`).

## Phase 5 — Town Hall Characters (TIER NOTE)

All character abilities live in **Unity C# (authoritative game logic), NOT server JS** —
the server is a pure relay. The `/add-character` skill describes a `server/src/characters/
*.js` hook registry; that path is the WRONG TIER for this project. Use the skill only as
CONCEPTUAL guidance (hook taxonomy, edge-case discipline, one-test-per-edge-case). Abilities
hang off existing Unity events (`GameTurnManager.OnTurnStart`, `CardEffectManager.OnCardPlayed`,
`PlayerService.OnPlayerEliminated`, `Player.TryalCardRevealed`, `Player.AccusationCountChanged`/
`AccusationThresholdReached`) and reuse the Phase-4 `IPlayerInput` seam for abilities needing
networked/phone input (Tituba rearrange, Samuel Parris discard-draw, John/Martha card-pick).
Approach: implement in priority order (Tituba first), introduce a minimal `ICharacterAbility`
convention to stop name-check sprawl, and promote to a full event-dispatcher when the first
inheritance character (John Proctor / Martha Corey) demands it.

**`CharacterAbilityDispatcher` EXISTS now (Phase 5 #5/#6, `Assets/Project/Scripts/Characters/`).**
It is a self-bootstrapping (`[RuntimeInitializeOnLoadMethod]`, no scene wiring) singleton that
subscribes to `PlayerService.OnPlayerEliminated` and dispatches via `GetEffectiveTownHallName()`
(so a Martha copying an ability routes through that ability automatically). It owns, per
elimination: the Martha copied-charge/limit re-resolve (`Player.ReResolveMarthaCopy`), the
Cotton-Mather revert (relocated OUT of `PlayerService.Eliminate`), and the John Proctor draft
(`JohnProctorAbility : IOnPlayerEliminated`, via a serialized re-entrant-safe draft queue over the
`card_pick` socket event). **This is the pattern for migrating the remaining name-check characters**
(Parris, the passives): elimination-time abilities implement `IOnPlayerEliminated`; holder-triggered
ones register in the `_abilities` map keyed by `TownhallName`. Migrate incrementally — do NOT rip out
all existing `HasTownHall()` name-checks at once. Verified follow-up (no automated harness yet): the
cascade-orphan regression — sole John drafter dies in the same matchmaker cascade that held a hand →
`JohnProctorAbility` empty-drafters branch must discard the orphaned hand cleanly (checked manually
via the TestManager debug harness).

**Mary Warren (#7) DONE — with TWO rulebook corrections that overturned earlier assumptions (do NOT
reintroduce the old behavior):**
- **Matchmaker: linkable but chain-immune.** Mary receives the Matchmaker card and links normally; her
  immunity is inline guards at the `mmPartner.EliminateNow()` cascade in `PlayerService.Eliminate`
  (partner-is-Mary → spare; plus the GENERAL both-teams-lose guard via
  `GameManager.CascadeWouldEndBothTeams`, a non-mutating win-check). **CORRECTION:** a SPARED partner's
  Matchmaker card **PERSISTS** (blue cards persist per rulebook) — it is NOT discarded. Safety comes from
  the GENERAL **"can't receive a 2nd matchmaker"** refusal in the `ActionOp.Matchmaker` handler, not from
  discarding the card. The both-teams-lose guard is reachable only via the sticky-`IsWitch` lost-card
  witch (`Player.DetermineRole`), so it is code-review-verified, not live-fire tested.
- **Black Cat: held-but-inert, NOT refused.** Mary CAN be given and hold the Black Cat (her card says
  immune to the ILL EFFECTS). **CORRECTION:** `AssignBlackCat` no longer discards for her; her immunity
  is relocated to Conspiracy step 1 (`GamePhaseManager.ConspiracyRoutine` skips the tryal reveal when the
  black-cat holder is Mary — no redirect). Non-harmful holder effects (e.g. dawn "goes first") still apply.

**`docs/character-spec.md` is the Phase 5 source of truth** (the `protocol.md` equivalent for
characters): the rulebook-locked ability/numbers/edge-cases for all 15 characters, the
corrected accusation-threshold spec, current code status (done/partial/stub/bug), and which
characters need networked `IPlayerInput`. Read it before implementing any character. Key
locked facts: George Burroughs base = **8** (rulebook, not the dev guide's old "14"); Danforth
is **−1 on the base BEFORE piety doubling** (current code applies it after — a bug to fix);
Mary Warren uses the rulebook matchmaker model (linkable but immune to the elimination chain).

## Phase 5 — Deferred Matchmaker Work

The night-kill matchmaker cascade works (`PlayerService.Eliminate` → partner dies
even if saved/confessed). Two character-specific exceptions are NOT yet built and
belong to Phase 5 (Town Hall characters), per dev guide Step 5.4:
- Mary Warren is immune to the matchmaker chain — if matched with her when she is
  eliminated, the partner still dies but Mary is unaffected.
- If the cascade would make BOTH teams lose simultaneously, only the intended
  target is eliminated, not the matched partner.

Also deferred to Phase 5 (Town Hall): the **William Phipps human fake-confess UI**. The
AI fake-confess branch IS preserved (`AiConfessSelection` → `ConfessFake`: immune without
revealing a tryal), but a human William Phipps cannot fake-confess through the masked
confess window yet — adding a fake-confess control only to that holder's phone is a
masking-design question that belongs with the Town Hall character work.

## Deferred — orphaned `[SerializeField]` cleanup

A later serialization-safe sweep should remove these now-unused inspector fields on
`GamePhaseManager` (left in place for now so removal doesn't disturb scene/prefab
serialization): `constablePrompt`, `witchPrompt`, `dawnBlackCatPrompt`,
`constableCanSelfProtect` (also the intended toggle for the Phase 6 ghost-variant
self-protect exception), and `confessionChoiceUI` (orphaned in 4c when the local
`ExecuteConfessionRound` was replaced by the networked `RunConfessWindow`).

## Deferred — dead-code + hardening cleanup pass (end-of-Phase-5, not urgent)

Batch these into one sweep once Phase 5 verification is done:

- **`Player.PerformTurnAction` (Player.cs:~402‑419)** — dead (AI runs via `AITurnSequencer`); still uses
  the old `.target`/unconditional-`RemoveCard` pattern. Delete.
- **`Player.ApplyCardEffect` (Player.cs:~358)** — dead legacy; has the broken `PlayerNameText ==
  "Cotton Mather"/"Sarah Good"` name-checks and its own Arson bare-clear. Delete.
- **`Player.ClearHand`** — now caller-less (live paths use `BurnHand` / `HandManager.ClearHand`
  directly); kept as a documented primitive. Remove if still unused at cleanup.
- **`Card.target` field (Card.cs:41)** — no live reader/writer after the Robbery/Scapegoat SO-mutation
  fix (recipient is a parameter now). Only dead code (`_Archive`, `PerformTurnAction`) references it.
- **`TargetingPolicy.ValidateSecondary`** — add an `IsEliminated` check for defense-in-depth (currently
  guaranteed only by the caller's eligibility list + `RequestTarget` re-verification).
- **`forwardToHost` field ordering (server/src/dispatch.js)** — `...(data)` spreads AFTER
  `playerId: socket.playerId`, so a client can overwrite its own `playerId`. Not a data leak (host
  re-validates the sender), but a spoofing surface across the whole player→host forward family; put the
  trusted `playerId` last.

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
- Phase 5 character spec (source of truth): @docs/character-spec.md
