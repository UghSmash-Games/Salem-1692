# Salem 1692 — Town Hall Character Spec (Phase 5 source of truth)

This is the authoritative build reference for the 15 Town Hall characters — the
`protocol.md` equivalent for character abilities. Built from a rulebook-vs-code
reconciliation done before any Phase 5 implementation.

## Tier & authority

- **All ability logic lives in Unity C#** (authoritative game logic). The server is a
  pure relay. The `/add-character` skill's `server/src/characters/*.js` path is the WRONG
  TIER — use the skill only as conceptual guidance (hook taxonomy, edge-case discipline,
  one-test-per-edge-case).
- **Authority order for rules:** Rulebook pages 12–14 (authoritative for numbers/edge
  cases) > physical card text. The dev guide is NOT authoritative (it had the Burroughs
  number wrong — corrected). The rulebook glossary only covers edge cases + the numeric
  characters; **base abilities for the other characters live on the physical cards** (the
  in-game `TownHallCard.RulesText` is the digital proxy — flagged below where it can't be
  cross-checked against the rulebook).
- Abilities hang off existing Unity events: `GameTurnManager.OnTurnStart`,
  `CardEffectManager.OnCardPlayed`, `PlayerService.OnPlayerEliminated`,
  `Player.TryalCardRevealed` / `TrialService.OnTrialCardRevealed`,
  `Player.AccusationCountChanged` / `AccusationThresholdReached`.
- **Approach:** implement in priority order (Tituba first); introduce a minimal
  `ICharacterAbility` convention to stop name-check sprawl; promote to a full
  event-dispatcher when the first inheritance character (John Proctor / Martha Corey)
  demands it. Reuse the Phase-4 `IPlayerInput` seam for abilities needing networked input.

## Characters needing NETWORKED player input (via `IPlayerInput`)

These cannot be passive name-checks — they prompt the holder for a choice on their phone,
the same masked/relayed pattern as Phase 4 secret phases:

- **Tituba** ✅ — deck-rearrange UI (drag/reorder the deck for a timed window).
- **Samuel Parris** ✅ — choose up to 2 cards from the discard pile (reuses the `card_pick` event +
  `RequestCardPick` with a Done/decline affordance; turn-ending like Draw 2).
- **John Proctor / Martha Corey** ✅ — when both hold Proctor's ability, take turns picking 3
  cards each from an eliminated player's cards (John first).

All other abilities are passive/automatic (no prompt).

---

## Locked accusation-threshold spec

Order of operations (verified against every rulebook number):

```
effectiveBase = baseAccusationLimit              // 7 normal; 8 for George Burroughs
if (accuser is Thomas Danforth)                  // −1 on the BASE, BEFORE piety
    effectiveBase = max(1, effectiveBase - 1)
threshold = hasPiety ? effectiveBase * 2 : effectiveBase
reveal when accusationCount >= threshold
```
(Only piety doubles the threshold and only Danforth reduces it. **Curse does NOT affect the
threshold** — it's a green card that discards a blue card; see the card-rule note below.)

| Scenario | Threshold |
|---|---|
| Normal target | **7** |
| Normal, Danforth accuses | **6** |
| Piety target | **14** |
| Piety target, Danforth accuses | **12** |
| George Burroughs | **8** |
| George, Danforth accuses | **7** |
| George + piety | **16** |
| George + piety, Danforth accuses | **14** |

**Status: ✅ FIXED (Phase 5).** `CheckAccusations` now applies Danforth's −1 to
`baseAccusationLimit` *before* the piety ×2 (non-Danforth uses `currentAccusationLimit` as-is;
the Danforth branch recomputes `effBase = baseAccusationLimit − 1` → ×2 if piety). Every row of
the table above was verified in playtest. `baseAccusationLimit = 7` default (Player.cs:92);
George's `baseAccusationLimit++` → 8 (Player.cs:149). (The earlier bug applied the −1 to the
already-piety-doubled `currentAccusationLimit`, so piety rows read 13/15 instead of 12/14.)

Accusation card values (Player.cs `RecalculateAccusations`, ~568): Accusation = 1,
**Evidence = 3** (1 vs Cotton Mather), Witness = 7.

Status legend: ✅ done · ◐ partial · ⊘ stub · ✗ bug · ✗✗ not built

---

## 1. Tituba ⊘ (FIRST to build; needs `IPlayerInput`)

- **Ability:** Once per game, on her turn before drawing, view and rearrange the deck. May
  move **any** cards including conspiracy and night. May rearrange **and** draw cards in the
  same turn.
- **Numbers:** 1 charge (`townHallAbilityCharges = 1`, Player.cs:154). Card says a 60-second
  rearrange window.
- **Edge cases (rulebook p14):** When night is drawn (during normal play after a rearrange),
  carry out the night phase and re-form the deck as normal. Moving conspiracy/night around is
  legal. The black-card "drawn out of turn" rule still applies.
- **Interactions:** Martha Corey can copy Tituba (gets 1 charge).
- **Code status:** ⊘ stubbed to a plain shuffle (GameTurnManager:365); charge + gating exist.
- **Build:** Replace the shuffle with a real deck reorder + a **networked rearrange input**
  (deck order shown to Tituba's phone, she reorders, host applies). Keep once/game (D3).

## 2. Cotton Mather ◐

- **Ability:** Evidence cards played **against him** count as **1** accusation (normal
  evidence = 3).
- **Numbers:** Evidence 3→1 for the Cotton Mather holder.
- **Edge cases (rulebook p12):** If **Martha** is copying Cotton Mather and Cotton Mather is
  **eliminated**, evidence cards immediately count as 3 against Martha again — even cards
  already in front of her.
- **Interactions:** Martha (copy), Danforth/Burroughs (independent threshold math),
  Will Grigs (Witness still 7).
- **Code status:** ✅ base (`RecomputeStatusFromStatusCards`, Player.cs:572; live path =
  CardEffectManager → `ApplyAccusation` → recompute). ✅ Martha-revert edge: on elimination,
  `PlayerService.Eliminate` now recomputes + re-checks every alive Martha (`ApplyAccusation(0)`),
  so evidence reverts 1→3 immediately and an over-threshold revert reveals at once.
- **Note:** a dead legacy `Player.ApplyCardEffect` (Player.cs:358) has a broken display-name
  check `PlayerNameText == "Cotton Mather"` (and "Sarah Good"); no live caller — delete in a
  cleanup pass.

## 3. Thomas Danforth ✅ (FIXED)

- **Ability:** When **he** is the accuser, the reveal threshold is reduced by **1**.
- **Numbers:** −1 on the **base**, before piety doubling (see locked spec). Normal target
  6th accusation triggers; piety target 12; George 7; George+piety 14.
- **Edge cases (rulebook p13):** Interaction with George Burroughs and with piety holders is
  the whole point — see the verified table.
- **Interactions:** George Burroughs, Piety. (Curse does NOT interact — it discards a blue
  card, not a threshold modifier; see the card-rule note.)
- **Code status:** ✅ FIXED (Phase 5). `CheckAccusations` now applies Danforth's −1 to the
  BASE before the piety ×2: non-Danforth uses `currentAccusationLimit` as-is; the Danforth
  branch recomputes `effBase = baseAccusationLimit − 1` → ×2 if piety. Verified against every
  row of the table (normal 6, piety 12, George 7, George+piety 14). Keyed on the ACCUSER's
  `HasTownHall(ThomasDanforth)` (so a Martha copying Danforth also applies). Required the
  **Piety card-`Op` data fix** (below) before piety rows could be tested.

## 4. George Burroughs ◐ (base ✅, Danforth-interaction via #3 fix)

- **Ability:** Harder to accuse — needs **8** accusations to reveal a tryal (vs the normal 7).
- **Numbers (rulebook-locked, D2 confirmed):** base **8**; **16** with piety (8×2); **7** when
  Danforth accuses; **14** with piety when Danforth accuses. (The dev guide's "14 per tryal"
  was WRONG — corrected.)
- **Edge cases:** All four numbers above; piety doubles his base like anyone else.
- **Interactions:** Thomas Danforth (relative math), Piety.
- **Code status:** ✅ base 8 (`baseAccusationLimit++`, Player.cs:149). Danforth+George numbers
  (7 / 14) now correct via the landed #3 Danforth fix; all four rows verified in playtest.
- **Build:** Nothing — base 8 + the #3 fix cover all four numbers.

## 5. John Proctor ✅ (dispatcher + networked draft — Group B)

- **Ability (rulebook-CORRECTED, reviewer-confirmed):** When a player is eliminated, choose **up to
  THREE cards from their HAND** to take; discard the rest of the hand. The eliminated player's
  cards **in play** (status: red + blue) are eliminated/discarded — **not** taken. (The earlier
  "take all blue cards + all hand" here and the auto-transfer in code were WRONG — John takes from
  the HAND only, and it's a CHOICE, so even the single-John case needs the pick UI when hand > 3.)
- **Edge cases (rulebook p12–13):** If **Martha** has inherited John's ability, John and Martha
  **look at the eliminated player's hand and take turns picking ONE card each, John first**, up to
  3 each, **alternating until the pool is exhausted** for short hands; leftovers are discarded.
- **Interactions:** Martha Corey (inheritance/split), Cotton Mather (Martha's evidence value),
  Matchmaker cascade ordering (a drafter may die in the cascade → recomputed at draft time).
- **Code status:** ✅ `JohnProctorAbility` (`IOnPlayerEliminated`) driven by
  `CharacterAbilityDispatcher`'s serialized draft queue; `Player.OnElimination` now leaves the HAND
  in place for the draft when a live drafter exists (else discards it), and ALWAYS discards status +
  Black Cat. Networked pick over the new `card_pick` socket event (`RequestCardPick` on `IPlayerInput`;
  AI drafters auto-pick). Single-John and John+Martha split both go through the same coroutine.
- **Build:** Done in Group B. (This is where the **event-dispatcher / `ICharacterAbility`**
  foundation was introduced, per #6.)
- **⚠️ Known minor gap (deliberately NOT fixed):** John's "up to 3" currently cannot **voluntarily
  decline early** — his draft loop only stops when the pool is exhausted or the 3-cap is hit, so he
  effectively takes `min(3, pool)`. Choosing to take *fewer* is rules-allowed but rarely desirable
  (cards are an advantage). The "Done/decline" affordance built for Samuel Parris (#12, `allowDone` on
  `RequestCardPick` + a Done button on `CardPickScreen`) could be extended to John's draft to close this,
  but it's out of scope for now. Recorded here so it isn't silently dropped.

## 6. Martha Corey ✅ (inheritance — dispatcher foundation lives here)

- **Ability:** Has the same ability as the **first living player to her right** (recomputed
  as neighbors die).
- **Edge cases (rulebook p12):** John Proctor split (see #5). Cotton Mather revert on Cotton's
  death (see #2). Inheriting a charge-based ability (Tituba/Parris/Phipps) grants the charge.
- **Interactions:** Every character she can copy; especially John, Cotton Mather.
- **Code status:** ✅ `GetEffectiveTownHallName` recomputes live and `HasTownHall` honors it
  (Player.cs). Copied charges/limits now **re-resolve mid-game** via `ReResolveMarthaCopy`
  (Player.cs), driven by the dispatcher on every elimination — no longer a set-once-at-setup bug.
- **Mechanism (the landmine, solved):** `ReResolveMarthaCopy` is **reset-then-reapply gated on a
  source change**: it returns early when the effective source is unchanged (preserving a spent
  Tituba/Parris charge — never resurrected), and only on a real change resets copied modifiers to
  `_intrinsicBase` (captured once at card assignment in `ApplyTownHallAbility`, immune to
  re-capture pollution) before reapplying the new source's fresh charge/limit (so George's +1 never
  double-counts). `ApplyMarthaCoreyCopy` (setup) and the mid-game path share this one method.
- **Dispatcher:** `CharacterAbilityDispatcher` (`Assets/Project/Scripts/Characters/`) is the
  foundation introduced here — a self-bootstrapping singleton that subscribes to
  `PlayerService.OnPlayerEliminated` and, per elimination, runs the Martha re-resolve + Cotton
  revert (relocated out of `PlayerService.Eliminate`) and drives the John draft. Remaining
  name-check characters migrate onto it incrementally (see the migration note below).

## 7. Mary Warren ✅ (matchmaker chain — rulebook model, D1)

- **Ability:** Immune to the **ill effects** of Matchmaker and Black Cat.
- **Rulebook model (D1, decided):** "Unaffected by matchmaker" = she IS linkable, but is
  **immune to the elimination chain**:
  - If her matchmaker partner is night-killed, the chain would kill Mary — **Mary survives**.
  - If **Mary** is eliminated, her partner **still dies** (chain fires for the partner).
  - If the chain would make **both teams lose simultaneously**, only the intended target dies
    (not the matched partner). *(Deferred Phase-4 matchmaker exception, landing here.)*
  - Black Cat: she CAN be given and hold the black cat, but is immune to its ILL EFFECT (the
    Conspiracy step-1 tryal reveal) — held-but-inert, not refused. (Her card says "immune to the
    ill effects"; the rulebook has no Mary+black-cat entry, so the card is authoritative.)
- **Code status: ✅ DONE (Phase 5 #7).**
  - **Linkable ✅:** the un-linkable early-return in the `ActionOp.Matchmaker` handler
    (`CardEffectManager`) was REMOVED — Mary now receives the Matchmaker card and links via
    `Player.TryFormMatchmakerLink` like anyone else.
  - **Chain immunity ✅:** inline guards at the captured-bond cascade in `PlayerService.Eliminate`
    (the `mmPartner.EliminateNow()` branch). `partnerIsMary` (cheap, first) skips the partner
    elimination when the cascade victim is Mary; because the guard checks `mmPartner`, a Mary who is
    the *initially*-eliminated player still cascades to her (non-Mary) partner.
  - **Both-teams-lose ✅ (GENERAL — guards EVERY matchmaker cascade, not just Mary):**
    `GameManager.CascadeWouldEndBothTeams(intendedTarget, partner)` — a non-mutating hypothetical that
    returns true only if eliminating the partner would satisfy BOTH win conditions at once (villagers'
    all-witch-tryals-revealed AND witches' parity). It models the double-kill by treating BOTH the
    intended target's and the partner's tryals as revealed. Win logic stays centralized in GameManager.
    - **Verification: MANUAL/code-review only (not live-fire tested)** — same posture as the
      cascade-orphan edge. The guard is only *reachable* when an alive player is `IsWitch == true` with
      NO unrevealed Witch tryal (so villagers-win and witches-parity can hold at once). That state only
      arises from the rulebook's "a player who loses their only witch card remains a witch" rule (a
      conspiracy swap), preserved by the STICKY `IsWitch` in `Player.DetermineRole`
      (`if (!IsWitch) IsWitch = hasWitchTryal;`). Manufacturing that state live is highly artificial and
      the current TestManager harness has no tryal/role/reveal control, so a live-fire test would be more
      fragile than valuable. A future live-fire test would need a `SetTryalsOnSeat(seat, TryalCard[])`
      debug method (assign `TryalCards` + `DetermineRole`) to build: intended target `[NotAWitch]`
      matchmaker-linked to a non-Mary partner holding the last unrevealed `[Witch]`, plus a third seat
      made a sticky lost-card witch (`SetTryals([Witch])` then `SetTryals([NotAWitch])`) for parity —
      then Eliminate the intended target and expect `SPARED (both-teams-lose)`.
  - **Spared partner's card PERSISTS ✅ (rulebook-corrected):** a SPARED partner (Mary or
    both-teams-lose) KEEPS their now-partnerless Matchmaker card — blue cards persist per rulebook; the
    bond is already cleared by `ClearMatch`, leaving them free to re-link if a new Matchmaker is played.
    (The earlier `DiscardMatchmakerStatus` auto-discard was REVERTED — nothing in the rules discards a
    survivor's matchmaker card, and the re-link is legitimate, not a bad state.) The real safety guard is
    the **"can't receive a 2nd matchmaker" refusal** below.
  - **"Can't receive a 2nd matchmaker" ✅ (GENERAL, rulebook p13):** the `ActionOp.Matchmaker` handler
    (`CardEffectManager`) now refuses the play (card not placed) when the target already
    `HasStatus("Matchmaker")`. Applies to every player, not just Mary; closes the two-cards-on-one-player
    hole and makes a spared holder's persistent card safe.
  - **Base matchmaker cascade ✅ (Phase 5):** capture-before-`OnElimination` ordering fix (see the
    card-rule note); "both die even if the partner was saved/confessed" works. The #7 guards are
    conditions on that working branch.
  - **Black Cat: held-but-inert ✅ (rulebook-corrected):** Mary CAN be given and hold the Black Cat —
    she's immune to its ILL EFFECT, not refused. The old `AssignBlackCat` Mary-discard and the human-draw
    target exclusion were REMOVED (the AI path never excluded her; Dawn routes through `AssignBlackCat`).
    Her immunity is relocated to **Conspiracy step 1** (`GamePhaseManager.ConspiracyRoutine`): when the
    black-cat holder is Mary, the tryal reveal is SKIPPED (treated as "no black cat," no redirect).
    Non-harmful holder effects (e.g. "goes first" at dawn) are unaffected — only the ill effect is negated.

## 8. William Phipps ◐ (human UI deferred)

- **Ability:** Once per game, confess **without** revealing one of your tryal cards (still
  gains night immunity).
- **Numbers:** 1 charge (Player.cs:154).
- **Code status:** ◐ AI fake-confess wired in 4c (`GamePhaseManager.AiConfessSelection` →
  `ConfessFake`). ✗✗ **human** fake-confess UI not built — a human Phipps can't fake-confess
  through the masked confess window yet.
- **Build:** A masking-compatible fake-confess control for a human Phipps in the confess
  window (a Town Hall design question — only that holder's phone differs, like other private
  data). Networked but small.

## 9. Abigail Williams ✅

- **Ability:** When she triggers a tryal reveal (her accusation crosses the threshold),
  discard all accusations in front of her own tryals.
- **Code status:** ✅ Player.cs:830 (`accuser.ResetAccusationCount()` on triggered reveal).
- **Build:** Verify against rulebook wording ("discard accusations in front of your own
  tryals" vs. reset count) when touched.

## 10. Anne Putnam ✅

- **Ability:** When she triggers a tryal reveal, draw 2 cards **before** the tryal is
  revealed.
- **Code status:** ✅ Player.cs:803 (draws 2 in the threshold-reached path before reveal).

## 11. Giles Corey ✅

- **Ability:** If you draw 2 Accusation cards on your turn, show them and draw a 3rd card.
- **Code status:** ✅ GameTurnManager:281, 519.
- **Build:** Verify the "exactly 2 Accusation cards drawn" detection when touched.

## 12. Samuel Parris ✅ (networked discard-pick)

- **Ability:** Twice per game, draw up to 2 cards from the **discard pile** instead of the
  deck. **No black cards.** (Card-text authority — Parris isn't in the rulebook glossary.)
- **Numbers:** 2 charges (Player.cs:157); 1 charge per use, up to 2 cards each.
- **Code status: ✅ DONE.**
  - **No-black-card filter FIXED:** the old predicate checked `Type == CardColor.Black`, but Night and
    Conspiracy are authored as `CardColor.White` (Black Cat is Blue) — so it rejected NOTHING and Parris
    could draw a Conspiracy (which reaches the discard via `CardEffectManager`). Now filtered by NAME via
    `Card.IsBlackCard(c)` (`c.Name == "Night" || "Conspiracy"`), matching how the rest of the code
    identifies black cards. Shared by `TryDrawFromDiscard` (local) and `RunParrisDiscardPick` (networked).
  - **Networked pick:** `GameTurnManager.RunParrisDiscardPick` builds the filtered discard pool and calls
    `IPlayerInput.RequestCardPick` up to twice (removing the chosen card between picks via
    `DeckManager.TakeSpecificFromDiscard`). **Reuses the `card_pick` socket event** (John's machinery) — no
    new event. `NetworkInput.RunTurn` offers a `"parris"` action (gate: his turn, no action yet,
    HasTownHall, charges > 0).
  - **TURN-ENDING (like Draw 2, NOT Tituba):** `"parris"` runs the pick then the turn ENDS
    (`RunParrisDiscardPick` does `ConsumeTownHallCharge` + `currentTurnAction = DrawTwoCards` + `EndTurn`;
    `NetworkInput` sets `turnOver = true`). Unlike `"tituba"`, it does NOT loop back to offer draw/play.
  - **"Up to 2" / decline:** `RequestCardPick` gained an `allowDone` flag → the phone `CardPickScreen`
    shows a **Done** button that submits index `-1` (skip sentinel). Caller-side interpretation: Parris
    treats `-1` (Done OR timeout) as "stop, take what I have"; John's draft (allowDone=false) still treats
    `-1` as its existing timeout safety-pick. Timeout behavior itself is unchanged.
  - **Tier:** turn-mechanics (`GameTurnManager`/`NetworkInput`, like Tituba), NOT the
    `CharacterAbilityDispatcher` (which only handles `OnPlayerEliminated`).

## 13. Rebecca Nurse ✅

- **Ability:** Each time a tryal is revealed on **another** player **from accusations**, draw
  1 card.
- **Code status:** ✅ TrialService:51 (gated on `fromAccusation`, excludes self).

## 14. Sarah Good ✅

- **Ability:** Robbery and Arson cards have no effect on her (discarded).
- **Code status:** ✅ CardEffectManager:87–88 (Arson/Robbery skipped for her).
- **Edge (rulebook p13, Scapegoat & Robbery):** Robbery never moves the user's own cards;
  disabled at 2 players.

## 15. Will Grigs ✅

- **Ability:** May use Alibi cards as Witness cards, worth **7** total accusations.
- **Code status:** ✅ CardEffectManager:72.
- **Build:** Verify the "worth 7" value when touched.

---

## Card-rule edge cases that affect character math (rulebook p12–14)

- **Card-`Op` data fix ✅ (Phase 5):** every blue/persistent SO had `Op:0` (= `ActionOp.Accusation`,
  the enum default), so playing them routed to the Accusation handler and never attached
  (`CardEffectManager.ExecuteActionOp` keys on `Op`). Fixed the `Op` on **6 SOs** — Piety (13),
  Asylum (11), Matchmaker (12), Stocks (1), Scapegoat (10), Black Cat (5). This unblocked
  Piety/Danforth-piety, and made Asylum (night immunity) and Matchmaker (link + cascade)
  actually work — all three re-verified in playtest. (Code was correct; data was wrong.)
- **Piety:** doubles the base threshold (Player.cs:541 ✅; now attaches after the `Op` fix).
  **If a player loses piety while at ≥7 accusations, they immediately lose a tryal; the player
  who removed piety chooses which.** ⚠️ Not found in the accusation code — **verify / build**
  alongside Danforth/Burroughs.
- **Matchmaker:** cannot receive a 2nd (✅ #7 — `ActionOp.Matchmaker` handler refuses the play if the
  target already `HasStatus("Matchmaker")`; general, any player); if one linked player is night-killed
  both die even if the other confessed or was saved (✅ `PlayerService.Eliminate`, Phase 5: cascade
  ORDERING fixed — capture the bond before `OnElimination` clears it; re-verified in playtest). Mary
  Warren + both-teams-lose exceptions → see #7 (guards land at `mmPartner.EliminateNow()`). A SPARED
  partner keeps their now-partnerless Matchmaker card (blue cards persist; bond cleared by `ClearMatch`;
  free to re-link) — NOT auto-discarded.
- **Black Cat:** witches may self-give at dawn (✅ 4b); the owner who draws conspiracy chooses
  which of their tryals is revealed (verify in the conspiracy path); Mary Warren held-but-inert (✅ #7 —
  she holds the card but the Conspiracy step-1 reveal is skipped for her; NOT refused at assignment).
- **Curse:** a base-game **green** card — *"discard one blue card currently in front of another
  player"* (rulebook p12: blue cards stay "until moved or discarded by another card such as
  scapegoat or curse"). Targets any blue card — Asylum / Piety / Matchmaker — **and the Black
  Cat** (rulebook p12: after dawn the black cat "can be discarded by a curse card"). ✅
  `CardEffectManager` `ActionOp.Curse` handler (discards a blue status card; black cat
  special-cased). Stocks is a **green** card, so it is correctly NOT curse-targetable.
  **It does NOT modify accusation thresholds** — the earlier "curse −1 threshold" was a phantom
  and has been removed from the code + spec.
  - ⚠️ **Deferred fidelity item (own task):** the handler auto-discards the *first* blue card it
    finds (and forces the black cat first if held), rather than letting the curse-player CHOOSE
    which blue card. The rulebook implies the player picks. Needs **networked player-choice
    input** (the `IPlayerInput` pattern) — its own task, likely pairing with the deferred
    accusation-reveal tryal-choice. Do NOT build now.

## Build priority (per `/add-character` skill)

1. Tituba ✅ → 2. Cotton Mather ✅ → 3. Thomas Danforth ✅ → 4. George Burroughs ✅ →
5. John Proctor ✅ → 6. Martha Corey ✅ → 7. Mary Warren ✅ → **8. remaining (NEXT)**.
Fix the Danforth piety-ordering bug (#3) before/with Burroughs (#4). Introduce the
event-dispatcher at John/Martha (#5–6). Mary Warren (#7) folded in the deferred Phase-4
matchmaker exceptions.

**#1–#7 DONE.** The `CharacterAbilityDispatcher` (`Assets/Project/Scripts/Characters/`) is the
foundation: a self-bootstrapping singleton subscribed to `PlayerService.OnPlayerEliminated`, keyed by
`GetEffectiveTownHallName()` (so Martha's inheritance routes automatically), with a serialized
re-entrant-safe draft queue. It owns the Martha copy re-resolve, the Cotton revert (relocated from
`PlayerService.Eliminate`), and the John draft (`JohnProctorAbility : IOnPlayerEliminated`). Mary Warren
(#7) is NOT a dispatcher ability — her chain-immunity + the GENERAL both-teams-lose rule are inline
prevention guards at the `mmPartner.EliminateNow()` cascade call (the dispatcher fires post-elimination,
too late to prevent a death); `GameManager.CascadeWouldEndBothTeams` holds the hypothetical win-check.
Remaining #8 characters are mostly done/passive (Abigail, Anne Putnam, Giles, Rebecca, Sarah Good, Will
Grigs ✅); the open items are **Samuel Parris** (networked discard-pick) and **William Phipps** (human
fake-confess UI) — migrate the elimination-time/holder-triggered ones onto the dispatcher incrementally.

**Deferred (verified manually, no automated harness):** the cascade-orphan regression — the sole John
drafter dies in the same matchmaker cascade that left a hand dangling, so the draft finds no drafter and
the orphaned hand must discard cleanly (`JohnProctorAbility` empty-drafters branch). Verified via the
TestManager debug harness (Link Matchmaker + Eliminate partner + Dump Dispatcher State); a scripted
regression test is a follow-up whenever a Unity play-mode test harness exists.
