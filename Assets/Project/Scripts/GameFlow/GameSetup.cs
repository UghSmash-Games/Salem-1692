/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Initializes the game by creating the Tryal deck, distributing Tryal cards to players, and drawing initial hands using the DeckManager.
*   Responsibilities:
*        • Calculate number of Witch, Constable, NotAWitch cards based on player count.
*        • Build and shuffle Tryal deck.
*        • Distribute 5 Tryal cards to each player.
*        • Invoke `DetermineRole()` on each player.
*        • Distribute Town Hall cards (2 per player if ≤7, choose 1; 1 per player if >7).
*        • Build Play Card deck: extract specials, deal 3, reinsert Conspiracy and Night.
*   Access Requirements:
*        • Access to `DeckManager` for drawing Game Cards.
*        • Access to list of `Player` instances.
*        • Read-only references to `TryalCard` ScriptableObjects.
*
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Salem.Cards;
using Salem.Data;
using Salem.Deck;
using Salem.GameFlow;
using Salem.Players;
using Salem.UI;
using UnityEngine;

namespace Salem.Gameplay.Setup
{
    public class GameSetup : MonoBehaviour
    {
        #region Vars
        [SerializeField] private GameManager GameManager;
        [Tooltip("Must Be Ordered: Constable, Witch, Not A Witch")]
        [SerializeField] private ScriptableObject[] TryalCards;
        [SerializeField] private TownHallChoiceUI townHallChoiceUI;
        // Exact Tryal card counts per player count: (NotAWitch, Witch, Constable)
        private static readonly Dictionary<int, (int notAWitch, int witch, int constable)> TryalDistribution = new()
        {
            { 4,  (18, 1, 1) },
            { 5,  (23, 1, 1) },
            { 6,  (27, 2, 1) },
            { 7,  (32, 2, 1) },
            { 8,  (29, 2, 1) },
            { 9,  (33, 2, 1) },
            { 10, (27, 2, 1) },
            { 11, (30, 2, 1) },
            { 12, (33, 2, 1) },
        };
        private List<TryalCard> TryalDeck = new List<TryalCard>();
        private DeckManager DeckManager;
        private IRng Rng => GameManager != null ? GameManager.Rng : _fallbackRng;
        private readonly IRng _fallbackRng = new XorShiftRng(1UL); // only if GM missing
        #endregion

        #region Standard Functions
        void OnValidate()
        {
            if (!GameManager) GameManager = FindFirstObjectByType<GameManager>();
        }

        void Awake()
        {
            DeckManager = GameObject.FindAnyObjectByType<DeckManager>();
            if (!GameManager) Debug.LogError("[GameSetup] Missing GameManager reference for RNG.");
        }
        #endregion

        #region Accessor Functions
        // Called in GamePhaseManager during Setup (now a coroutine to support Town Hall UI choice)
        public IEnumerator SetupNewGame(IReadOnlyList<Player> players)
        {
            SetupTryalCards(players);
            yield return SetupTownhallCards(players);
            SetupPlayDeck(players);
        }
        #endregion

        #region Helper Functions

        // ── Step 1: Tryal Cards ──────────────────────────────────────────────
        private void SetupTryalCards(IReadOnlyList<Player> players)
        {
            if (!TryalDistribution.TryGetValue(players.Count, out var dist))
            {
                Debug.LogError($"[GameSetup] No Tryal distribution defined for {players.Count} players.");
                return;
            }

            int totalCards = dist.notAWitch + dist.witch + dist.constable;
            int cardsPerPlayer = totalCards / players.Count;

            // Add Constable card(s)
            for (int i = 0; i < dist.constable; i++)
            {
                TryalCard card = (TryalCard)Instantiate(TryalCards[0]);
                card.TryalCardType = TryalCardType.Constable;
                TryalDeck.Add(card);
            }

            // Create Witch cards
            for (int i = 0; i < dist.witch; i++)
            {
                TryalCard card = (TryalCard)Instantiate(TryalCards[1]);
                card.TryalCardType = TryalCardType.Witch;
                TryalDeck.Add(card);
            }

            // Fill remaining slots with NotAWitch cards
            for (int i = 0; i < dist.notAWitch; i++)
            {
                TryalCard card = (TryalCard)Instantiate(TryalCards[2]);
                card.TryalCardType = TryalCardType.NotAWitch;
                TryalDeck.Add(card);
            }

            // Shuffle and distribute
            ShuffleTryalDeck(TryalDeck);

            foreach (var player in players)
            {
                player.TryalCards = DrawTryalCards(cardsPerPlayer, TryalDeck);
                player.InvokeOnTryalCardsChanged();
                player.DetermineRole();
                player.setRng(GameManager.Rng);
            }
        }

        // ── Step 2: Town Hall Cards ──────────────────────────────────────────
        private IEnumerator SetupTownhallCards(IReadOnlyList<Player> players)
        {
            if (players.Count <= 7)
            {
                // Each player gets 2 cards and chooses 1
                foreach (var player in players)
                {
                    var options = DeckManager.DrawTownhallCards(2);
                    if (options.Count < 2)
                    {
                        // Fallback: assign whatever we got
                        player.setTownhall(options.FirstOrDefault());
                        continue;
                    }

                    if (player.IsHuman && player.IsLocalPlayer && !PlayerService.IsAirConsoleMode)
                    {
                        // Human player: show UI choice
                        bool chosen = false;
                        if (townHallChoiceUI != null)
                        {
                            townHallChoiceUI.Open(options[0], options[1], (selected, discarded) =>
                            {
                                player.setTownhall(selected);
                                DeckManager.DiscardTownhallCard(discarded);
                                chosen = true;
                            });
                            yield return new WaitUntil(() => chosen);
                        }
                        else
                        {
                            Debug.LogWarning("[GameSetup] TownHallChoiceUI not assigned; defaulting to random pick.");
                            int pick = Rng.NextInt(0, 2);
                            player.setTownhall(options[pick]);
                            DeckManager.DiscardTownhallCard(options[1 - pick]);
                        }
                    }
                    else
                    {
                        // AI or remote player: pick randomly
                        int pick = Rng.NextInt(0, 2);
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

        // ── Step 3: Play Card Deck ───────────────────────────────────────────
        private void SetupPlayDeck(IReadOnlyList<Player> players)
        {
            if (DeckManager == null)
            {
                Debug.LogError("[GameSetup] DeckManager is null; cannot set up play deck.");
                return;
            }

            // Remove Night and Black Cat from the deck before dealing
            Card nightCard = DeckManager.ExtractCardFromDeck("Night");
            Card blackCatCard = DeckManager.ExtractCardFromDeck("Black Cat");

            // Hold Black Cat for Dawn phase witch vote
            if (blackCatCard != null)
                DeckManager.HoldBlackCatForDawn(blackCatCard);
            else
                Debug.LogWarning("[GameSetup] No Black Cat card found in the deck.");

            // Shuffle the remaining deck
            DeckManager.ShuffleDeck();

            // Deal 3 cards to each player; if Conspiracy is drawn it stays in the deck
            foreach (var player in players)
            {
                if (player.HandManager == null)
                {
                    Debug.LogError($"[GameSetup] {player.PlayerNameText} has NULL HandManager.");
                    continue;
                }
                DeckManager.DrawMultipleCards(player.HandManager, 3, c => c.Name == "Conspiracy");
            }

            // Cut remaining deck in half and shuffle Night card into the bottom half
            if (nightCard != null)
                DeckManager.ReshuffleAndPlaceNightCard(nightCard);
        }

        private List<TryalCard> DrawTryalCards(int count, List<TryalCard> deck)
        {
            List<TryalCard> cards = deck.Take(count).ToList();
            deck.RemoveRange(0, count);
            return cards;
        }

        private void ShuffleTryalDeck(List<TryalCard> deck)
        {
            for (int i = 0; i < deck.Count; i++)
            {
                int randomIndex = Rng.NextInt(i, deck.Count);
                (deck[i], deck[randomIndex]) = (deck[randomIndex], deck[i]);
            }
        }
        #endregion
    }
}
