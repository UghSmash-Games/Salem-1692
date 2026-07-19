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
    returns true only if eliminating the partner would satisfy BOTH win conditions at once: villagers'
    **all-witch-tryals-revealed** (modelled by treating BOTH the intended target's and the partner's
    tryals as revealed) AND witches' **`WitchesControl(aliveAfter)`** — i.e. after removing the partner,
    every remaining alive player is a witch (the partner was the last non-witch). **NOT parity** —
    Salem has no parity win; the witch half is `nonWitchesAfter == 0`, the same single authority
    `EvaluateEndGame` uses (see [win-conditions] / CLAUDE.md "Win Conditions (canonical)"). Win logic
    stays centralized in GameManager.
    - **Verification: MANUAL/code-review only (not live-fire tested)** — same posture as the
      cascade-orphan edge. **Reachability is UNCHANGED by the parity→`nonWitchesAfter==0` correction:**
      the guard fires only when, after removing the partner (the last non-witch), every other alive
      survivor is a witch WITH NO unrevealed Witch tryal — otherwise `wouldVillagersWin` (all witch
      tryals revealed) can't also hold. Such a survivor is a "sticky lost-card witch": `IsWitch == true`
      but the witch tryal already revealed/lost, arising from the rulebook's "a player who loses their
      only witch card remains a witch" rule (a conspiracy swap), preserved by the STICKY `IsWitch` in
      `Player.DetermineRole` (`if (!IsWitch) IsWitch = hasWitchTryal;`). Manufacturing that state live is
      highly artificial and the current TestManager harness has no tryal/role/reveal control, so a
      live-fire test would be more fragile than valuable. A future live-fire test would need a
      `SetTryalsOnSeat(seat, TryalCard[])` debug method (assign `TryalCards` + `DetermineRole`) to build:
      intended target `[NotAWitch]`... no — the intended target must hold the **last unrevealed `[Witch]`**
      (so its reveal is the villagers' winning reveal), matchmaker-linked to a **non-Mary partner who is
      the last non-witch `[NotAWitch]`**, plus a third seat made a sticky lost-card witch
      (`SetTryals([Witch])` then `SetTryals([NotAWitch])`) so removing the partner leaves all-witches —
      then Eliminate the intended target and expect `SPARED (both-teams-lose)`.
    - ⚠️ **CORRECTION LOG:** the earlier version of this entry described the witch half as "parity"
      (`witchesAfter >= nonWitchesAfter`), matching the then-current (WRONG) `EvaluateEndGame`. Parity was
      a Werewolf/Mafia import with no rulebook basis; it was removed project-wide. The corrected guard is
      strictly narrower, so cascades the old parity form wrongly flagged as both-teams-lose (and wrongly
      SPARED) now correctly eliminate the partner.
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

## 8. William Phipps ✅ (human fake-confess UI + security hardening)

- **Ability:** Once per game, confess **without** revealing one of your tryal cards (still
  gains night immunity).
- **Numbers:** 1 charge (Player.cs `WilliamsPhipps` case in `ApplyTownHallAbility`, shared with Tituba;
  Martha-copy grants 1).
- **Code status: ✅ DONE.**
  - **Human UI — host-gated button, NOT universal (corrected model):** the **"Confess without revealing"**
    control ([SecretPhaseScreen.tsx](../webclient/src/screens/SecretPhaseScreen.tsx), confess variant) is
    shown **only** when the per-player `prompt.canFakeConfess` flag is true — computed host-side in
    `NetworkInput.RequestSecretPhase` as `promptType=="confess" && HasTownHall(WilliamsPhipps) && charges > 0`,
    carried on that player's own `SecretPhasePromptEntry` and routed to their one socket by the existing
    per-player `secret_phase_prompt` unpack (same privacy class as the `acting` flag — never broadcast).
    **Why holder-only is fine:** Town Hall identity is **PUBLIC** (cards dealt face-up, ability read aloud at
    setup), so a Phipps-only button leaks nothing — exactly like the Tituba/Parris action buttons that
    already render only on the holder's screen. This is unlike witch/constable/tryal secrecy, which is what
    the confess window's CORE masking protects (every phone still gets the identical base confess/skip choice;
    only the third button's visibility is role-conditional). Sends the `"fake"` sentinel (`CONFESS_FAKE`,
    matches host `ConfessFake`). The flag naturally disappears once the charge is spent (`charges > 0`
    becomes false) — no "remove after use" logic.
  - **Immunity server-enforced (SECURITY FIX — defense in depth, kept even though the button is client-gated):**
    `RecordConfession`'s `ConfessFake` branch ([GamePhaseManager.cs](../Assets/Project/Scripts/GameFlow/GamePhaseManager.cs))
    grants immunity (`plan.Confessors.Add`) + consumes the charge **only if** `HasTownHall(WilliamsPhipps) &&
    charges > 0`. Previously it added to `Confessors` **unconditionally** (gated only the charge consume) — a
    real hole: a spoofed `"fake"` from any client granted free night immunity. A non-Phipps (or spent-charge
    Phipps) `"fake"` is now **silently discarded** (== "don't confess"). The host never trusts the client.
  - **Immunity path:** `plan.Confessors` → `NightResolver.Resolve` saves the confessor — the SAME path as
    a real confession, but the fake-confess is NOT added to `pendingConfessions`, so **no tryal flips** at
    `revealAt`. Indistinguishable from "not targeted" at the public reveal (night target is secret) — so the
    USE of the ability is still masked, even though the button's visibility isn't.
  - **AI path (unchanged):** `AiConfessSelection` still returns `ConfessFake` for a witch Phipps-with-charge
    (50%).
- **Masking model:** the confess window's core masking (who confesses this round; witch/tryal secrecy) is
  intact and universal; only the Phipps button's **visibility** is host-gated per-player, because Town Hall
  identity is public. The server-enforced effect is defense in depth.
- **Verification:** webclient tests lock (a) the base confess/skip structure identical across roles, (b) the
  button appears only with `canFakeConfess`, (c) it sends `"fake"`; the server test confirms `canFakeConfess`
  routes per-player only (never to others/mirror). The host gating (non-Phipps `"fake"` → no immunity) is
  **code-review-verified** (Unity C#, no play-mode harness — same posture as the cascade-orphan / both-teams-lose edges).

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

## 14. Sarah Good ✅ (verified against card text)

- **Ability (card text):** *"Robbery and arson cards played against you have no effect and are
  discarded."*
- **Code status: ✅ VERIFIED on all axes** (`CardEffectManager` `ActionOp.Arson` / `ActionOp.Robbery`,
  both guarded by `if (!t.HasTownHall(SarahGood))`):
  - **Both cards** covered; effect is **genuinely inert** (whole effect skipped, not reduced).
  - **Discarded ✅:** the guard is inside the op; the generic green-card removal
    (`CardEffectManager` ~265: `Type == Green && Op != Stocks → HandManager.RemoveCard`) runs AFTER
    `ExecuteActionOp` regardless, and `RemoveCard` → `AddToDiscardPile`. SO authoring confirmed
    correct (Arson `Type:0`/`Op:7`, Robbery `Type:0`/`Op:9`/`RequiresSecondTarget:1`) — no repeat of
    the `Op:0` data bug.
  - **Scoped to "against her" ✅:** the guard tests `t` (target), never `s` — she can still play
    Robbery/Arson against others.
  - **Robbery two-target ✅:** `ExecuteActionOp` passes `t` = primary = **victim**,
    `u` = `action.target` = **recipient**; the op is `t.TransferEntireHandTo(u)`. Immunity fires when
    she is the VICTIM. When she is the RECIPIENT it correctly does NOT block — the robbery isn't
    "against her" and she's benefiting.
  - **Martha copy ✅** via `GetEffectiveTownHallName`.
- **Edge (rulebook p13, Scapegoat & Robbery):** Robbery never moves the user's own cards;
  disabled at 2 players.
- **✅ RESOLVED (was a deferred 4a stopgap): the player now CHOOSES the recipient.** The old
  `ac.target = AITargetingHelper.SelectRandomTarget(p)` one-shot pick is gone. It was a real bug, not
  just low fidelity: `SelectRandomTarget` excludes only *self*, so it could pick the **victim**,
  `TargetingPolicy.ValidateSecondary` then rejected the play, `ExecuteCardEffect` early-returned —
  and `TryPlayCard` discarded the card anyway. Robbery silently did nothing `1/(alive−1)` of the time
  (33% at 4 players, **100% at 2**). Now: `NetworkInput.RequestTarget` (the `IPlayerInput` seam that
  was already declared for "sub-target" use and previously stubbed out) prompts the playing player
  over the new **`target_request`/`target_submit`** events with the host-computed eligible list
  (never self, never the victim, never eliminated), re-verified host-side. Declining or timing out
  leaves the card **in hand**. The local/host UI path (`TableLayoutController`) chains a second
  `BeginTargetSelection`; the AI keeps its retry-and-bail. **Scapegoat shares every one of these
  fixes** — identical shape, same code path.
- **✅ Shared-asset mutation fixed:** the recipient is no longer written to `ActionCardSO.target` (a
  *project asset* — it persisted across plays and would be shared by two copies of the same card).
  `ExecuteCardEffect(card, target, secondary)` takes it by parameter and threads it to
  `ExecuteActionOp`. `ExecuteCardEffect` also **returns bool** now, and every caller consumes the card
  only on `true` — killing the "rejected play still eats the card" bug class generally.
- **✅ 2-player disable implemented (rulebook p13):** `TargetingPolicy.NeedsThreePlayers` /
  `ValidatePlayable` is the single source of truth. Two layers: the host refuses the play in
  `ExecuteCardEffect`, AND `action_request` now carries `unplayableCards` so the phone greys the card
  out (same host-gated-eligibility pattern as the Tituba/Parris buttons).
- **⚠ Still deferred:** the **Curse** blue-card choice (auto-discards the first blue card instead of
  letting the player pick) — unchanged, still its own task.
- **🐛 Arson card-destruction bug — FIXED (found during the Sarah trace, not a Sarah bug):** the
  Arson op called `t.ClearHand()`, and `ClearHand` empties the hand **without discarding** — so every
  Arson permanently destroyed those cards. Because the deck re-forms from the discard pile
  (`ReshuffleDiscardPile`), burned cards never returned to circulation, shrinking the deck for the
  rest of the game. Fixed by adding **`Player.BurnHand()`** (discard-every-card → then clear) and
  pointing Arson at it; `Player.OnElimination`'s no-drafter branch (which already hand-rolled the same
  discard-then-clear) now reuses it. `Player.ClearHand()` kept for the ownership-already-moved case
  (`TransferEntireHandTo`) with a doc warning. Robbery was never affected — `TransferEntireHandTo`
  re-adds each card to the recipient before clearing.

## 15. Will Grigs ✅ (real mode choice + persistent Witness — 2 bugs fixed)

- **Ability (card text):** *"You may choose to use alibi cards as if they were witness cards, worth
  seven total accusations."*
- **The two modes are OPPOSITE** — normal Alibi *removes* up to 3 accusations from the target
  (defensive); the Witness conversion *adds* 7 (offensive). That is why "may choose" is a REAL choice.
- **🐛 BUG 1 FIXED — "may choose" was auto-applied.** The old `_ops[Alibi]` did
  `if (s.HasTownHall(WillGrigs)) t.ApplyAccusation(7, s)` unconditionally, so Grigs could NEVER use
  Alibi's normal effect — and it could backfire (targeting an ally to clear them instead dumped 7 on
  them). Now a **real mode prompt**: `NetworkInput.PlayCardRoutine` asks via the existing
  **`confirm_request`** (`prompt = "grigs_alibi_mode"`; yes = Witness +7, no = normal Alibi).
  - **Target-first** (not mode-first): the eligible target set is IDENTICAL for both modes — roles are
    hidden, so the game cannot filter ally vs enemy — meaning mode-first would gain nothing
    mechanically while costing an extra round-trip. He picks the target normally, then the mode.
  - **No answer → CANCEL** (Alibi stays in hand, nothing applied). Witness is an opt-in per the card,
    and neither mode is a safe auto-default on an already-picked target. This required a
    `RequestConfirmation` contract change (below).
  - **AI** takes the Witness conversion (headline use); **local** leaves the flag false → normal Alibi
    (safe no-op, same posture as Tituba/Parris local).
- **🐛 BUG 2 FIXED — "worth 7" was TRANSIENT.** `ApplyAccusation(7)` added 7 on top of the recomputed
  status total, but the Alibi is green (discarded) and no card was placed — so the next
  `RecomputeStatusFromStatusCards` **wiped the +7**. It only "worked" when it immediately crossed the
  threshold; against **piety (14) / George Burroughs (8)** it evaporated instead of accumulating, and
  it never participated in Scapegoat/Curse/Sarah. Now `CardEffectManager.PlaceWitnessProxy` places a
  **persistent Witness**: `RecalculateAccusations` counts it as **7 across recomputes**, Scapegoat
  transfers it, Curse correctly ignores it (red, not blue) — because it genuinely IS a Witness card.
- **Mechanism (why a proxy):** only ONE `Alibi`/`Witness` SO asset exists, referenced many times in the
  deck, so per-card instance state is impossible (the same shared-asset constraint behind the Robbery
  `ac.target` bug). So the conversion **clones a runtime Witness** (`Instantiate(witnessTemplate)`,
  a new `[SerializeField]` on CardEffectManager — **must be wired in the inspector**, else it logs an
  error and falls back to the old transient +7 rather than silently no-op'ing).
  - **Deck integrity:** the clone is flagged `Card.IsRuntimeInstance`, and `DeckManager.AddToDiscardPile`
    **destroys** runtime instances instead of adding them to the discard pile — otherwise
    `ReshuffleDiscardPile` (`Deck.AddRange(DiscardPile)`) would inflate the deck with phantom Witness
    cards (the Arson-bug lesson). The played Alibi discards normally and returns to circulation.
- **`RequestConfirmation` contract change (shared, behaviour-preserving):** it now fires `onConfirm`
  **only on a real answer** — never on timeout/no-channel — so each caller owns its own no-answer
  default by pre-initializing. Abigail pre-inits `true` (unchanged: no answer → clears); Grigs uses a
  `bool?` that stays null → cancel.
- **Scoping ✅:** the guard tests `s` (the player *playing* the Alibi), so it never fires when an Alibi
  is played *against* Grigs or by someone else. **Martha ✅** via `GetEffectiveTownHallName`.

---

## Card-rule edge cases that affect character math (rulebook p12–14)

- **Card-`Op` data fix ✅ (Phase 5):** every blue/persistent SO had `Op:0` (= `ActionOp.Accusation`,
  the enum default), so playing them routed to the Accusation handler and never attached
  (`CardEffectManager.ExecuteActionOp` keys on `Op`). Fixed the `Op` on **6 SOs** — Piety (13),
  Asylum (11), Matchmaker (12), Stocks (1), Scapegoat (10), Black Cat (5). This unblocked
  Piety/Danforth-piety, and made Asylum (night immunity) and Matchmaker (link + cascade)
  actually work — all three re-verified in playtest. (Code was correct; data was wrong.)
- **Alibi is a POINT budget, not a card count ✅ (FIXED).** Card text: *"DISCARD UP TO THREE
  ACCUSATIONS CURRENTLY IN FRONT OF ANOTHER PLAYER."* "Accusations" is the game's point UNIT — the red
  cards say so themselves (Evidence *"WORTH THREE ACCUSATIONS"*, Witness *"WORTH SEVEN ACCUSATIONS"*,
  Accusation = the base 1). So ONE Alibi discards **either up to three Accusation cards OR a single
  Evidence card**, and can **never** remove a Witness (7 > 3).
  - **🐛 Was:** `ApplyAlibi` removed only `Op == Accusation` cards, so an **Evidence** card (exactly 3
    points) could never be removed — a pre-existing general bug, unrelated to Will Grigs. Found while
    investigating why a Grigs Witness-proxy survived an Alibi (that part was CORRECT — 7 > 3, and the
    proxy is deliberately indistinguishable from a real Witness).
  - **Now:** `ApplyAlibi(accusationBudget = 3)` removes highest-value-first among cards that fit the
    remaining budget — point-optimal for a budget of 3, so a player "which cards" prompt could not
    improve the result and is deliberately NOT built. (Contrast **Curse**, where which blue card
    matters — that choice stays deferred.)
  - **Cotton Mather interaction (confirmed intended):** he devalues Evidence to 1, so an Alibi played
    on him can strip **three** Evidence cards (3 × 1 = 3). Falls out of the shared value function.
  - **Single source of truth:** `Player.AccusationValueOf(Card)` — used by BOTH
    `RecomputeStatusFromStatusCards` (the running total) and `ApplyAlibi` (the removal budget), so the
    value table can't drift. It is player-relative (the Cotton Mather rule lives inside it).
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
