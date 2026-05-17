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
*        • Draw initial cards for each player using DeckManager.
*   Access Requirements:
*        • Access to `DeckManager` for drawing Game Cards.
*        • Access to list of `Player` instances.
*        • Read-only references to `TryalCard` ScriptableObjects.

* FIXME:
*    • Fix the in-place shuffling bug with Fisher-Yates shuffle logic.
*    • Optionally offload Tryal card creation to a TryalCardFactory class.
*    • Split SetupNewGame logic into smaller methods for clarity.
*    • Ensure null safety on DeckManager reference in Awake.
*    • Make Witch ratio configurable through game settings.
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
        [SerializeField] private TableLayoutController tableLayoutController;
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
        private static readonly HashSet<string> InitialHandRestrictedCards = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "Night",
            "Conspiracy"
        };
        #endregion

        #region Standard Functions
        void OnValidate()
        {
            if (!GameManager) GameManager = FindFirstObjectByType<GameManager>();
        }

        void Awake()
        {
            DeckManager = GameObject.FindAnyObjectByType<DeckManager>();
            if (!GameManager) Debug.LogError("[CardEffectManager] Missing GameManager reference for RNG.");

        }
        #endregion

        #region Accessor Functions
        //Called in GamePhaseManager during Setup (now a coroutine to support Town Hall UI choice)
        public IEnumerator SetupNewGame(IReadOnlyList<Player> players)
        {
            //Debug.Log($"[GameSetup] Running SetupNewGame");
            SetupTryalCards(players);
            tableLayoutController.BuildTable(players);
            
            yield return SetupTownhallCards(players);
            SetupPlayDeck(players);
        }
        #endregion

        #region Helper Functions
        private void SetupTryalCards(IReadOnlyList<Player> players)
        {
            //Debug.Log($"[GameSetup] Running SetupTryalCards");
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
                //give each player a reference to the RNG to be able to randomly decide tryal card. This will likely be replaced later, but I want to just get the systems connected for now
                player.setRng(GameManager.Rng);
            }
        }

         private IEnumerator SetupTownhallCards(IReadOnlyList<Player> players)
        {
            //Debug.Log($"[GameSetup] Running SetupTownhallCards");
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

                    if (player.IsHuman && player.IsLocalPlayer) //&& !PlayerService.IsAirConsoleMode) //AIRCONSOLE TEMP DISABLE 4/28/26
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

            // Martha Corey: apply copied passive abilities after all town hall cards are assigned
            foreach (var player in players)
            {
                if (player.townhallCard != null && player.townhallCard.CardName == TownhallName.MarthaCorey)
                    player.ApplyMarthaCoreyCopy();
            }
        }

        private void SetupPlayDeck(IReadOnlyList<Player> players)
        {
            //Debug.Log($"[GameSetup] Running SetupPlayDeck");
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

        //Have players draw their Tryal Cards
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
                int randomIndex = RNGService.Rng.NextInt(i, deck.Count);
                (deck[i], deck[randomIndex]) = (deck[randomIndex], deck[i]);
            }
        }
        #endregion
    }
}