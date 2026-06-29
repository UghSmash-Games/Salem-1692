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

- **Tituba** — deck-rearrange UI (drag/reorder the deck for a timed window).
- **Samuel Parris** — choose up to 2 cards from the discard pile.
- **John Proctor / Martha Corey** — when both hold Proctor's ability, take turns picking 3
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
if (hasCurse) threshold = max(1, threshold - 1)  // curse is a card, not a character
reveal when accusationCount >= threshold
```

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

**Current code bug (Player.cs `CheckAccusations`, ~794–796):** Danforth's `−1` is applied to
`currentAccusationLimit`, which has *already* been doubled for piety (set ~541). So every
**piety** row is wrong (piety target reveals at 13 not 12; George+piety at 15 not 14). The
non-piety rows happen to be correct. **Fix:** apply Danforth's −1 to `baseAccusationLimit`
*before* the piety ×2 (as in the pseudocode above). `baseAccusationLimit = 7` default
(Player.cs:92); George's `baseAccusationLimit++` → 8 (Player.cs:149) is already correct.

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

## 3. Thomas Danforth ✗ (bug)

- **Ability:** When **he** is the accuser, the reveal threshold is reduced by **1**.
- **Numbers:** −1 on the **base**, before piety doubling (see locked spec). Normal target
  6th accusation triggers; piety target 12; George 7; George+piety 14.
- **Edge cases (rulebook p13):** Interaction with George Burroughs and with piety holders is
  the whole point — see the verified table.
- **Interactions:** George Burroughs, Piety, Curse.
- **Code status:** ✗ −1 applied after piety doubling (Player.cs:794–796). Non-piety correct,
  piety cases off by the piety delta.
- **Build:** Move the −1 to the base before piety ×2 per the locked spec.

## 4. George Burroughs ◐ (base ✅, Danforth-interaction via #3 fix)

- **Ability:** Harder to accuse — needs **8** accusations to reveal a tryal (vs the normal 7).
- **Numbers (rulebook-locked, D2 confirmed):** base **8**; **16** with piety (8×2); **7** when
  Danforth accuses; **14** with piety when Danforth accuses. (The dev guide's "14 per tryal"
  was WRONG — corrected.)
- **Edge cases:** All four numbers above; piety doubles his base like anyone else.
- **Interactions:** Thomas Danforth (relative math), Piety.
- **Code status:** ✅ base 8 (`baseAccusationLimit++`, Player.cs:149). Danforth+George piety
  numbers depend on the #3 fix.
- **Build:** Nothing for the base; correctness of the piety rows comes from fixing Danforth.

## 5. John Proctor ◐ (needs `IPlayerInput` for the split edge)

- **Ability:** When a player is eliminated, take all blue (status) cards in front of them and
  all cards in their hand.
- **Edge cases (rulebook p12–13):** If **Martha** has inherited John's ability, John and
  Martha **look at the eliminated player's cards and take turns picking 3 each, John first.**
- **Interactions:** Martha Corey (inheritance/split), Cotton Mather (Martha's evidence value),
  Matchmaker cascade ordering.
- **Code status:** ◐ base transfer to the single John holder (Player.OnElimination,
  Player.cs:742). ✗✗ John/Martha split not built.
- **Build:** This is where the **event-dispatcher / `ICharacterAbility`** foundation should
  be introduced. The split needs **networked input** (both players pick from a revealed set,
  alternating, John first).

## 6. Martha Corey ◐ (inheritance — build dispatcher here)

- **Ability:** Has the same ability as the **first living player to her right** (recomputed
  as neighbors die).
- **Edge cases (rulebook p12):** John Proctor split (see #5). Cotton Mather revert on Cotton's
  death (see #2). Inheriting a charge-based ability (Tituba/Parris/Phipps) grants the charge.
- **Interactions:** Every character she can copy; especially John, Cotton Mather.
- **Code status:** ◐ `GetEffectiveTownHallName` recomputes live (Player.cs:178–204) and
  `HasTownHall` honors it (165–172). But charge/limit copies are set **once at setup**
  (`ApplyMarthaCoreyCopy`, 210–230) — they won't update if the copied neighbor changes
  mid-game (e.g. the right neighbor is eliminated and the new neighbor has a different
  ability).
- **Build:** Re-resolve copied charges/limits whenever Martha's effective source changes
  (neighbor eliminated). Implement `onAbilityInherited` / `onAbilityLost` semantics.
  - **Existing hook to relocate:** `PlayerService.Eliminate` already recomputes every alive
    Martha's *accusations* on each elimination (added for the Cotton revert, #2 —
    `m.ApplyAccusation(0)`). The dispatcher should **move this into its `OnPlayerEliminated`
    handler** and **add the charge/limit re-resolve there** (George's `baseAccusationLimit`,
    Tituba/Parris/Phipps charges). Cannot reuse `ApplyMarthaCoreyCopy` as-is —
    `baseAccusationLimit++` is cumulative and charges would reset; needs proper reset +
    consumed-charge handling.

## 7. Mary Warren ◐ → ✗✗ (matchmaker chain) — **rulebook model, D1**

- **Ability:** Immune to the **ill effects** of Matchmaker and Black Cat.
- **Rulebook model (D1, decided):** "Unaffected by matchmaker" = she IS linkable, but is
  **immune to the elimination chain**:
  - If her matchmaker partner is night-killed, the chain would kill Mary — **Mary survives**.
  - If **Mary** is eliminated, her partner **still dies** (chain fires for the partner).
  - If the chain would make **both teams lose simultaneously**, only the intended target dies
    (not the matched partner). *(This is the deferred Phase-4 matchmaker exception, landing
    here.)*
  - Black Cat: she cannot be given the black cat (immune).
- **Code status:**
  - Black Cat immunity ✅ (Player.cs:617; CardEffectManager:166 excludes her as a target).
  - Matchmaker: ✗ currently makes her **un-linkable** (CardEffectManager:120) — **wrong model;
    remove it** per D1.
  - Night-cascade Mary-immunity ✗✗ not built (`PlayerService.Eliminate` cascade has no Mary
    check, ~140–149).
  - Both-teams-lose rule ✗✗ not built.
- **Build:** Remove CardEffectManager:120 (allow the link); in `PlayerService.Eliminate`'s
  matchmaker cascade, skip eliminating the partner **iff the partner is Mary Warren**; add the
  both-teams-lose guard (only the intended target dies). Keep Black Cat immunity.

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

## 12. Samuel Parris ◐ (needs `IPlayerInput`)

- **Ability:** Twice per game, draw up to 2 cards from the **discard pile** instead of the
  deck. **No black cards.**
- **Numbers:** 2 charges (Player.cs:157).
- **Code status:** ◐ GameTurnManager:324; charges exist.
- **Build:** Verify the no-black-card filter; the discard-pick likely needs a **networked
  choice** (which discard cards to take).

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

- **Piety:** doubles the base threshold (Player.cs:541 ✅). **If a player loses piety while at
  ≥7 accusations, they immediately lose a tryal; the player who removed piety chooses which.**
  ⚠️ Not found in the accusation code — **verify / build** alongside Danforth/Burroughs.
- **Matchmaker:** cannot receive a 2nd; if one linked player is night-killed both die even if
  the other confessed or was saved (✅ `PlayerService.Eliminate`). Mary Warren + both-teams-lose
  exceptions → see #7.
- **Black Cat:** witches may self-give at dawn (✅ 4b); the owner who draws conspiracy chooses
  which of their tryals is revealed (verify in the conspiracy path); Mary Warren immune (✅).

## Build priority (per `/add-character` skill)

1. Tituba → 2. Cotton Mather → 3. Thomas Danforth → 4. George Burroughs →
5. John Proctor → 6. Martha Corey → 7. Mary Warren → 8. remaining.
Fix the Danforth piety-ordering bug (#3) before/with Burroughs (#4). Introduce the
event-dispatcher at John/Martha (#5–6). Mary Warren (#7) folds in the deferred Phase-4
matchmaker exceptions.
