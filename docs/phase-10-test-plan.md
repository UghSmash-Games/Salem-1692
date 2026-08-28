# Phase 10 — Testing Checklist (status + how to reproduce)

The dev guide's Phase 10 is a list of 18 scenarios to sign off before deployment. This document is
the working record: for each item, what it actually asserts, what backs it **today**, and the exact
steps to reproduce it.

**Why this document exists.** Most of the list is not automatable at the tier it lives in. Nine of
the items are Unity C# game-rules edges, and Unity has no play-mode test harness in this project —
so "tested" for those means *a human reproduced the situation and watched it behave*. Writing that
down is the difference between a checklist that was genuinely exercised and one that was read.

## Status legend

| | Meaning |
|---|---|
| ✅ **automated** | A test in `server/__tests__` or `webclient/src/**` fails if this breaks. Re-run at will. |
| ✅ **playtested** | A human reproduced it against the real Unity host. Dated. |
| ◐ **code-verified** | The code path was read and is correct, but nothing has exercised it. The weakest kind of "done". |
| ⬜ **unexercised** | Neither tested nor traced. |

⚠️ **Do not promote an item to ✅ playtested without a date and a note on what was actually seen.**
The value of this table is that it distinguishes evidence from confidence.

---

## The checklist

### 1. Host + mirror show the same state within 100ms
**Asserts:** the two screens are one board, not two renderings that can drift.
**Status:** ✅ playtested (2026-08-28, 9- and 12-seat) + ✅ automated in part.
`webclient/src/screens/MirrorScreen.test.tsx` locks that the mirror renders every public fact, and
`ringLayout.test.ts` locks that both screens seat the same player in the same chair. The 100ms
latency claim itself is a network property, not a code property — the sync mechanism is item 1's real
subject and is covered by `useSynchronizedReveal.test.ts` (screens schedule against `revealAt`, not
message arrival).

### 2. Mirror syncs when joining mid-game
**Asserts:** a display that connects late is immediately current, not blank until the next tick.
**Status:** ✅ playtested (Phase 7, the "mirror-blank-on-join fix").
**Repro:** start a game, then open `/display` and enter the code. The board must paint at once.

### 3. Non-witch submits during the night vote — discarded silently, phone still confirms
**Asserts:** the masking model's core: a non-acting submission changes nothing and *looks* identical.
**Status:** ✅ automated (client + server) / ◐ code-verified (host discard).
The server forwards every submission without inspecting `acting` (`dispatch.test.js`), and the phone
renders the same confirmation either way. The **discard itself** is Unity
(`GamePhaseManager.RecordWitchVote` ignores non-acting submits) and is code-verified only.
**Repro:** see item 4 — same sitting.

### 4. Every phone shows an identical masking screen at dawn/night
**Asserts:** no player can be identified by their screen, controls, flow, or timing.
**Status:** ✅ automated (structure) + ✅ playtested (Phase 4c).
`SecretPhaseScreen` tests lock that the confess/skip structure is identical across roles and that the
only role-conditional control is the host-gated William Phipps button (public identity — see
CLAUDE.md's masking line).
**Repro:** two phones, one witch one not, side by side during the night vote. Compare screens *and*
the moment each becomes interactive.

### 5. Witch timeout — a random target after 45s
**Status:** ◐ code-verified. `GamePhaseManager.witchVoteTimeout` (45s) resolves with whatever was
recorded; `NightResolver` random-fills any witch with no valid vote.
**Repro:** ⚠ needs a witch who deliberately does nothing — easiest with an AI-filled game where you
hold the only witch phone and never tap.

### 6. Constable timeout — no gavel placed
**Status:** ◐ code-verified. `constableTimeout` (30s); no submission means no `plan.ConstableTarget`,
so `NightResolver` places nothing.
**Repro:** as item 5, holding the constable phone.

### 7. 4-player game (1 witch, 1 constable, 2 townspeople) — verify all win paths
**Status:** ⬜ unexercised as a deliberate matrix. Individual paths have come up in play.
**Repro:** needs deterministic role assignment → **`TestManager` seat setup** (below).

### 8. Conspiracy turns the last townsperson into a witch — that player LOSES
**Asserts:** the rulebook's "in which case that player loses" — they are excluded from the winning
witch team.
**Status:** ◐ code-verified. `AddTryalCardAndNotify` → `EvaluateEndGame` records that player as the
loser. See CLAUDE.md → Win Conditions.
**Repro:** needs a hand-built end state → **seat setup** (below).

### 9. Constable is also a witch — the evil-constable path
**Status:** ✅ automated (client) / ◐ code-verified (rules).
`RoleIndicator` tests lock that a dual-role player sees BOTH roles. `Player.IsWitch` is sticky and
`IsConstable` is reveal-aware, and they are independent — so the dual role is representable.
**Repro:** seat setup — give one seat `[Witch, Constable]`.

### 10. A player with 2 witch cards has one revealed — announces, is NOT eliminated
**Status:** ◐ code-verified. `TrialService.OnDoubleWitchRevealed`; elimination waits for the LAST
witch card. `gameEventCopy.test.ts` locks the announcement copy.
**Repro:** seat setup — `[Witch, Witch, NotAWitch]`, then reveal index 0.

### 11. Matchmaker: one linked player saved, the other eliminated — BOTH die
**Status:** ✅ playtested (Phase 5, the cascade-ordering fix).
**Repro:** `TestManager → Link Matchmaker Seat<->SeatB`, then eliminate one.

### 12. Piety removed at 7+ accusations — immediate tryal reveal
**Status:** ◐ code-verified. `Player.TriggerPietyLossReveal`, fired from the Curse handler only when
the removed blue card was Piety.
**Repro:** seat setup — add Piety, drive accusations to ≥7, then Curse the Piety away.

### 13. George Burroughs accusation math with piety (16 normal / 14 vs Danforth)
**Status:** ✅ playtested (Phase 5 — all four rows of the locked table).
Also locked client-side: `MirrorSeat` renders a dynamic limit rather than a hardcoded 7.

### 14. Scapegoat/Robbery disabled at 2 players
**Status:** ✅ automated (client) / ◐ code-verified (host).
`TargetingPolicy.NeedsThreePlayers` is the single source of truth; the host refuses the play AND
`action_request.unplayableCards` greys the card out. A webclient test locks that an unplayable card
*says* so rather than merely looking dim.

### 15. Tituba rearranges a night card to the top — night still triggers on draw
**Status:** ⬜ unexercised. The interaction (a black card moved by a player, then drawn) has not been
run since the real reorder replaced the stubbed shuffle.
**Repro:** hold Tituba, use the rearrange, put Night on top, end turn, draw.

### 16. 2–3 player ghost mode
**Status:** ⛔ **not applicable — superseded.** Ghost mode will not be built (CLAUDE.md → Phase 6);
AI fill covers small groups. This item is closed, not pending.

### 17. All clients drop simultaneously — the host waits for reconnects
**Status:** ✅ playtested (2026-08-28, single seat) + ✅ automated (the simultaneous case, relay tier).
`dispatch.test.js` → "a whole-table disconnect": three seats drop together and reclaim in a DIFFERENT
order, each getting its own `private_state` and no one else's; every reclaimed seat can act again; and
a seat nobody reclaims stays reserved without blocking the others or erroring on its absent socket.
Seats are reserved by the relay and held by the host mid-game; a dropped seat leaves the
secret-phase wait set so it cannot stall a phase, and returns via `rejoin_room`.
**Repro:** mid-phase, put every phone in airplane mode, then restore them. Each must land back in its
own seat with board + private state, and any open prompt must reappear.
⚠ The **open-prompt replay** is the specific part not yet seen live.

### 18. A player joins with the same display name as an existing player
**Status:** ◐ code-verified. `NetworkGameCoordinator.UniqueName` appends " (2)", " (3)" … because
targets resolve by `PlayerNameText`, so a duplicate would make targeting ambiguous.
**Repro:** two phones, same name, in the lobby.

---

## Deterministic seat setup — BUILT (the enabling work)

Seven items (7, 8, 9, 10, 12, and parts of 3/5/6) need the game to be in a SPECIFIC state — particular
tryal cards on particular seats. Nothing could do that: `GameSetup` deals at random, and `TestManager`
could eliminate a seat, add Evidence and link a Matchmaker but had **no tryal, role or accusation
control**. Reproducing "the last townsperson becomes a witch" by playing until it happens is not a
test, it is waiting.

`TestManager` now has that control (⚠ still TEMP scaffolding — it goes when a real Unity play-mode
harness lands, together with `GameSetup.DEBUG_forcedTownHall`):

| Menu item | What it does |
|---|---|
| **Set Tryals On Seat** | Deals an exact row from a spec — `"W,N,N"`, `"W,W,N"`, `"W,C"` — then re-runs `DetermineRole`, the same two steps `GameSetup` performs, so the seat is indistinguishable from a dealt one. |
| **Reveal Tryal On Seat** | Flips one card through the REAL `Player.RevealTryalCard` path, so `TrialService`, the double-witch announcement, Rebecca Nurse's draw and the win check all fire as in play. |
| **Add Accusations To Seat** | Places N real Accusation cards, so the count comes from `RecalculateAccusations` and honours Piety doubling, George's base 8 and Cotton Mather's discount. |
| **Add Status Card To Seat** | Attaches any blue card (Piety, Asylum, Stocks…). |
| **Dump Win State** | Prints both win-condition inputs — alive witches / non-witches AND unrevealed witch tryals — because the two checks are asymmetric and easy to confuse. |

**These do not bypass the rules, only the randomness.** Every hook writes the same state the real deal
produces (instantiated `TryalCard` SOs, never the shared asset — writing `TryalCardType` on the asset
would persist into every later game) and drives reveals through the normal path.

⚠️ **`IsWitch` is sticky**, by rulebook ("a player who loses their only witch card remains a witch"),
so re-dealing a seat's row does NOT clear a witch role. That is correct behavior, not a harness bug —
restart the game for a clean seat.

### Recipes for the blocked items

| Item | Setup | Then | Expect |
|---|---|---|---|
| 8 — last townsperson turns witch and LOSES | every seat `W` except one `N` | give the `N` seat a Witch tryal (conspiracy, or Set Tryals) | game over, witches win, **that player is not among the winners** |
| 9 — evil constable | one seat `"W,C"` | play normally | that phone shows BOTH roles; the gavel still works until the constable tryal is revealed |
| 10 — two witch cards | one seat `"W,W,N"` | Reveal Tryal index 0 | announcement fires, player is **NOT** eliminated; reveal index 1 to eliminate |
| 12 — piety loss at threshold | Add Status Card (Piety), Add Accusations 7 | Curse the Piety away | a tryal reveals immediately, chosen by the remover |
| 7 — 4-player win paths | seats `W` / `C` / `N` / `N` | run each ending | Dump Win State before and after each reveal |

Run **Dump Win State** before and after the decisive action in every one of these — it is the
cheapest way to see WHICH win condition fired, and the two are easy to mistake for each other.
