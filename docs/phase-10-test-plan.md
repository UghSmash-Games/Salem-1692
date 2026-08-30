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
**Status:** ✅ automated (client + server) / ◐ code-verified (host discard, traced 2026-08-28).
The server forwards every submission without inspecting `acting` (`dispatch.test.js`), and the phone
renders the same confirmation either way. The discard is `RunNetworkedSecretPhase.OnSubmit`: a
non-acting submit updates the live tally not at all and never reaches `recordActing`, while
**every** player's Confirm counts toward the wait — so resolution timing cannot separate who acted
from who didn't. Both halves verified by reading; the behaviour still wants one live sitting.
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
**Status:** ◐ code-verified (traced 2026-08-28). `witchVoteTimeout` (45s) resolves with whatever was
recorded; `NightResolver` random-fills any witch with no valid vote, and dawn has its own fallback
(`votes.Count == 0` → a random placement) so the black cat still lands.
⚠ **The window is not shortened by everyone else finishing.** The phase waits for every CONNECTED
human to confirm, by design — so this test takes the full 45s even if the witch is the only one
idle. Use the lobby's timer setting to shorten it rather than assuming something has hung.
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
**Status:** ◐ code-verified (traced 2026-08-28). `AddTryalCardAndNotify` → `EvaluateEndGame(this)`
excludes that player from the winners. See CLAUDE.md → Win Conditions.
**Checked specifically — the mid-loop evaluation is SAFE.** Conspiracy step 2 applies as all removals
then all additions, so the win check can fire while other players' cards are still in flight, held in
`passedCards` and belonging to nobody's `TryalCards`. That could in principle make the villagers'
"all witch tryals revealed" test true spuriously. It cannot here: the check only runs when a player
JUST became a witch, and their witch card is added *before* the evaluation — so at least one
unrevealed witch card is always visible at that moment.
⚠ **Known arbitrary edge:** if two players turn witch in the same conspiracy round and the second is
the last non-witch, the loop order decides which one is the loser. They turned simultaneously in
fiction; nothing in the rulebook breaks that tie.
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
**Status:** ◐ code-verified (traced 2026-08-28). The Curse handler orders it correctly — remove the
card, `RecomputeStatusFromStatusCards` (so the limit is back to the un-doubled base), *then*
`TriggerPietyLossReveal`. A networked remover chooses which tryal for real, over
`PendingTryalRevealTarget` with reason `"piety_loss_reveal"`.
🐛 The method's own doc comment still claimed networked removers fell back to random — a gap closed
in the Phase 5 close-out. Corrected, since a stale comment is how the next reader "re-discovers" a
bug that no longer exists.
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
**Status:** ◐ code-verified (traced 2026-08-28); still ⬜ for a live run.
The trigger cannot be bypassed by reordering, because it fires on the DRAW, not on deck position:
`DeckManager.DrawCard` → `CardEffectManager.HandleCardDrawn` → `GamePhaseManager.HandleNightCardDrawn`.
`SetDeckOrder` validates that the submitted order is a true permutation and leaves the deck untouched
otherwise, so a malformed phone submission cannot corrupt the deck either.
**Repro:** hold Tituba, use the rearrange, put Night on top, end turn, draw.
🐛 This trace surfaced an unrelated defect on the neighbouring branch — see Finding 1.

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
**Status:** ✅ automated (the client half, after a fix) / ◐ code-verified (the host half).
`NetworkGameCoordinator.UniqueName` appends " (2)", " (3)" … because targets resolve by
`PlayerNameText`, so a duplicate would make targeting ambiguous. It is correct and unbounded.
🐛 Tracing it found the phone did NOT honour that renaming — see Finding 2, now fixed and locked by
`gameStore.myName.test.ts`.
**Repro:** two phones, same name, in the lobby. The second must appear as "Name (2)" on the board
**and on its own phone**, and if that player is the constable, selecting themselves during the
constable save must still be refused on their screen.

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


---

## Findings from the Phase 10 traces

### Finding 1 — a drawn Black Cat is assigned AT RANDOM in networked play ⚠ OPEN (needs a rules call)

`CardEffectManager.HandleCardDrawn` offers the drawer a choice of recipient only when
`drawer.IsHuman && drawer.IsLocalPlayer && tableLayoutController != null`. **In networked mode no
player is ever `IsLocalPlayer`** — every human is remote — so the branch is dead and every networked
draw falls through to `AITargetingHelper.SelectRandomTarget`. The cat lands on a random player with
no agency from anyone.

This is the same bug class as the Robbery recipient, the Curse blue-card pick and Will Grigs' mode
choice — all three already fixed by routing through `IPlayerInput`. The two neighbouring gates were
checked and are FINE: the accusation reveal (`HandleAccusationRevealChoice`) and conspiracy step 1
both handle the networked chooser first and use `IsLocalPlayer` only as the local-host fallback.

**Reachability is narrow.** `GameSetup` extracts the Black Cat before dealing and holds it for dawn,
so it is not in the draw deck at the start. It returns only if it reaches the discard pile (a Curse
discards it) AND the deck later re-forms from the discard. So: a long game, after a Curse.

**Why it is NOT fixed here:** the fix depends on a rules answer this document should not invent.
Drawing the Black Cat mid-game has no explicit rulebook entry (the glossary covers giving it at dawn,
cursing it, and moving it with scapegoat). Two readings:
1. **It is a blue card you drew** → it should go to your HAND and be played on a target later, like
   any blue card. That would mean deleting the immediate-assign path entirely.
2. **It resolves on draw** → the drawer chooses a recipient now, over `RequestTarget`, exactly like
   the Robbery recipient.

Either is defensible; they differ in feel and in timing. **Decision needed before implementing.**

### Finding 2 — the phone showed the name the player TYPED, not the one the table uses ✅ FIXED

The host uniquifies duplicate names (`UniqueName` → "Cris (2)"). The phone went on displaying the
typed name, so the player's own screen and the board disagreed. That was cosmetic in itself — but
`SecretPhaseScreen` compares "my name" against a target name **that came from the host** to warn a
constable off protecting themselves. For the renamed player the comparison could never match: no
warning, they tap themselves, and the host — which enforces the rule for real
(`target != constable`) — silently places no gavel.

A wasted constable save that LOOKS like a save is the worst version of that bug: the player believes
someone is protected, and the night resolves as if nobody was.

**Fixed** by `selectMyDisplayName`, which reads this player's name from the public board and falls
back to the typed one only before the first board arrives. It also fixes the reconnect case, where
nothing typed a name at all. The rule itself was never at risk — the host was always the backstop.
