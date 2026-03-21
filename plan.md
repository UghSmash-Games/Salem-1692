# Game Setup Implementation Plan — Salem 1692

Align the setup flow in `GameSetup.cs`, `DeckManager.cs`, and `GamePhaseManager.cs` with the official Salem 1692 rules.

---

## Overview of Changes

The new setup order in `GameSetup.SetupNewGame()` becomes:

```
1. SetupTryalCards(players)          — unchanged logic, same witch ratio + 1 constable
2. SetupTownhallCards(players)       — NEW: ≤7 players get 2, pick 1; >7 get 1
3. SetupPlayDeck(players)            — NEW: extract special cards, deal 3, reinsert Night/Conspiracy
```

The Black Cat is NO LONGER assigned during setup. It is assigned during the Dawn phase by witch vote.

---

## Step 1: Tryal Cards — Minor cleanup only

**File:** `GameSetup.cs` → `SetupTryalCards()`

No functional changes needed. The current implementation (witch ratio + 1 constable + fill with NotAWitch, 5 per player, shuffle and distribute) matches the rules. Just verify the witch count table is reasonable:

| Players | Witches (1/3 ratio) | Constables | NotAWitch | Total |
|---------|---------------------|------------|-----------|-------|
| 4       | 1                   | 1          | 18        | 20    |
| 6       | 2                   | 1          | 27        | 30    |
| 8       | 3                   | 1          | 36        | 40    |
| 12      | 4                   | 1          | 55        | 60    |

This matches the rules. Keep as-is.

---

## Step 2: Town Hall Cards — Major change

**Files:** `GameSetup.cs`, `DeckManager.cs`, `Player.cs`

### 2a. DeckManager: Add method to draw multiple Town Hall cards

Add `DrawTownhallCards(int count)` that returns a `List<TownHallCard>` (draws N from top of shuffled TownhallDeck).

### 2b. GameSetup: Rewrite `SetupTownhallCard()`

```csharp
private void SetupTownhallCards(IReadOnlyList<Player> players)
{
    if (players.Count <= 7)
    {
        // Each player gets 2 cards, chooses 1, discards the other
        foreach (var player in players)
        {
            var options = DeckManager.DrawTownhallCards(2);
            if (options.Count < 2)
            {
                // Fallback: just assign whatever we got
                player.setTownhall(options.FirstOrDefault());
                continue;
            }

            if (player.IsHuman && !PlayerService.IsAirConsoleMode)
            {
                // Queue UI choice (handled via callback/coroutine)
                PendingTownHallChoices.Enqueue((player, options));
            }
            else
            {
                // AI: pick randomly
                int pick = Rng.NextInt(0, options.Count);
                player.setTownhall(options[pick]);
                DeckManager.DiscardTownhallCard(options[1 - pick]);
            }
        }
    }
    else
    {
        // >7 players: 1 card each, no choice
        foreach (var player in players)
            DeckManager.drawTownhallCard(player);
    }
}
```

### 2c. Town Hall Choice UI

Since the game already has `TargetPickerUI` for player selection, we need a similar picker for Town Hall card choice. Options:

- **Option A (Simpler):** Add a new method to an existing UI script that shows 2 Town Hall cards and lets the player tap one. A simple panel with 2 card buttons and a confirm button.
- **Option B:** Reuse TargetPickerUI with card names instead of player names.

**Recommendation:** Create a small `TownHallChoiceUI` MonoBehaviour with:
- `Open(TownHallCard option1, TownHallCard option2, Action<TownHallCard, TownHallCard> onChosen)` — callback receives (chosen, discarded)
- Two card display slots, click to select, confirm button

### 2d. Town Hall visibility

After all players have their Town Hall card, broadcast/log each player's assignment so all players can see. Add to `Player.setTownhall()`:
- Fire an event `OnTownhallAssigned` so UI can display it
- The cards should be viewable at any time (already accessible via `player.townhallCard`)

### 2e. Town Hall ability application

Currently `TownHallCard.applyEffect()` is empty and abilities are hardcoded in `Player.Awake()` by checking `PlayerNameText`. Move the logic:

- In `Player.setTownhall()`, after assigning the card, call `ApplyTownHallAbility()` which sets `baseAccusationLimit`, `townHallAbilityCharges`, etc. based on `card.CardName`.
- Remove the hardcoded ability checks from `Player.Awake()` (they run before Town Hall is assigned anyway).

---

## Step 3: Play Card Deck — Significant changes

**Files:** `GameSetup.cs`, `DeckManager.cs`

### 3a. Replace `AssignBlackCatAtStart()` + `SetupInitalHand()` with `SetupPlayDeck()`

New method flow:

```csharp
private void SetupPlayDeck(IReadOnlyList<Player> players)
{
    // 1. Extract Night, Conspiracy, and Black Cat from the deck
    Card nightCard = DeckManager.ExtractCardFromDeck("Night");
    Card conspiracyCard = DeckManager.ExtractCardFromDeck("Conspiracy");
    Card blackCatCard = DeckManager.ExtractCardFromDeck("Black Cat");

    // Store Black Cat for Dawn phase assignment
    if (blackCatCard != null)
        DeckManager.HoldBlackCatForDawn(blackCatCard);

    // 2. Shuffle the remaining deck
    DeckManager.ShuffleDeckPublic();

    // 3. Deal 3 cards to each player (no rejection needed — special cards already removed)
    foreach (var player in players)
    {
        if (player.HandManager == null) continue;
        DeckManager.DrawMultipleCards(player.HandManager, 3);
    }

    // 4. Add Conspiracy card back at a random position
    if (conspiracyCard != null)
        DeckManager.InsertCardAtRandom(conspiracyCard);

    // 5. Add Night card randomly into the bottom half
    if (nightCard != null)
        DeckManager.ReshuffleAndPlaceNightCard(nightCard);
}
```

### 3b. DeckManager additions

- `ShuffleDeckPublic()` — expose shuffle (or just make `ShuffleDeck()` public)
- `InsertCardAtRandom(Card card)` — insert at random index in the deck
- `HoldBlackCatForDawn(Card card)` / `GetHeldBlackCat()` — temporary storage for Dawn phase
- Remove the `ShouldRejectInitialHandCard` predicate and `InitialHandRestrictedCards` set (no longer needed)

---

## Step 4: Dawn Phase — Witch vote for Black Cat

**File:** `GamePhaseManager.cs`

### 4a. All witches vote, majority wins, random tiebreak

Replace current logic where only local witch picks. New flow:

```
1. Identify all alive witches
2. For each witch:
   - If local human witch: show TargetPickerUI to pick Black Cat recipient
   - If AI witch: pick randomly from all alive players
3. Tally votes — player with most votes gets the Black Cat
4. Tie: break randomly via RNG
5. Assign Black Cat via player.AssignBlackCat(card)
6. Set turn order based on Black Cat holder
7. Transition to Day phase
```

### 4b. Implementation

The Dawn phase becomes a coroutine that:
1. Gets the held Black Cat card from DeckManager
2. Collects votes from all witches (UI for human, random for AI)
3. Tallies and resolves
4. Assigns the card and sets turn order

```csharp
private IEnumerator DawnPhaseRoutine()
{
    var witches = PlayerService.GetAliveWitches();
    var allPlayers = PlayerService.GetAlivePlayers();
    var blackCatCard = DeckManager.GetHeldBlackCat();

    if (witches.Count == 0 || blackCatCard == null)
    {
        // Fallback: random assignment
        var randomTarget = allPlayers[Rng.NextInt(0, allPlayers.Count)];
        ResolveBlackCatAssignment(randomTarget, blackCatCard);
        yield break;
    }

    // Collect votes from each witch
    var votes = new Dictionary<Player, Player>(); // witch → target

    foreach (var witch in witches)
    {
        if (witch.IsLocalPlayer && witch.IsHuman)
        {
            // Show UI, wait for pick
            bool done = false;
            nightTargetPicker.Open(witch, false, (target, _) =>
            {
                votes[witch] = target;
                done = true;
            }, allPlayers, true, "Vote: Who receives the Black Cat?");

            yield return new WaitUntil(() => done);
        }
        else
        {
            // AI: pick randomly
            votes[witch] = allPlayers[Rng.NextInt(0, allPlayers.Count)];
        }
    }

    // Tally votes
    var tally = votes.Values.GroupBy(p => p)
        .OrderByDescending(g => g.Count())
        .ToList();

    Player winner;
    if (tally.Count > 1 && tally[0].Count() == tally[1].Count())
    {
        // Tie — break randomly among tied players
        var tied = tally.Where(g => g.Count() == tally[0].Count()).Select(g => g.Key).ToList();
        winner = tied[Rng.NextInt(0, tied.Count)];
    }
    else
    {
        winner = tally[0].Key;
    }

    ResolveBlackCatAssignment(winner, blackCatCard);
}
```

### 4c. Update `ResolveBlackCatAssignment()` to accept the card

```csharp
private void ResolveBlackCatAssignment(Player target, Card blackCatCard)
{
    if (target == null || blackCatCard == null) return;
    target.AssignBlackCat(blackCatCard);
    // Set turn order...
    // Transition to Day...
}
```

---

## Step 5: Remove obsolete code

1. **`GameSetup.AssignBlackCatAtStart()`** — Delete entirely (Black Cat now assigned in Dawn)
2. **`GameSetup.SetupInitalHand()`** — Replaced by `SetupPlayDeck()`
3. **`GameSetup.InitialHandRestrictedCards`** — Remove (no longer needed)
4. **`GameSetup.ShouldRejectInitialHandCard()`** — Remove
5. **`Player.Awake()` hardcoded ability checks** — Move to `setTownhall()` / `ApplyTownHallAbility()`

---

## Step 6: Setup flow becomes async (coroutine)

Because Town Hall card choice for ≤7 players requires waiting for human UI input, `SetupNewGame` needs to become a coroutine:

```csharp
public IEnumerator SetupNewGame(IReadOnlyList<Player> players)
{
    SetupTryalCards(players);
    yield return SetupTownhallCards(players);  // waits for human choices
    SetupPlayDeck(players);
}
```

Update `GamePhaseManager.StartSetupPhase()` to:
```csharp
private void StartSetupPhase()
{
    StartCoroutine(SetupRoutine());
}

private IEnumerator SetupRoutine()
{
    yield return GameSetup.SetupNewGame(PlayerService.All);
    GameTurnManager.Initialize();
    yield return ChangePhase(GamePhase.Dawn, PhaseChangeDelay);
}
```

---

## File Change Summary

| File | Changes |
|------|---------|
| `GameSetup.cs` | Rewrite `SetupNewGame` as coroutine; replace hand/blackcat methods with `SetupPlayDeck`; rewrite Town Hall distribution with choice mechanic |
| `DeckManager.cs` | Add `DrawTownhallCards(int)`, `InsertCardAtRandom()`, `HoldBlackCatForDawn()`/`GetHeldBlackCat()`, make `ShuffleDeck()` public |
| `GamePhaseManager.cs` | Rewrite Dawn phase as coroutine with witch voting; update setup phase to use coroutine; update `ResolveBlackCatAssignment` signature |
| `Player.cs` | Move Town Hall ability logic from `Awake()` to `setTownhall()`; add `ApplyTownHallAbility()` |
| `TownHallCard.cs` | No changes needed (abilities applied via Player, not the card) |
| **NEW:** `TownHallChoiceUI.cs` | Simple UI for picking 1 of 2 Town Hall cards (≤7 player mode) |
