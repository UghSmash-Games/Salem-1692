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
using Salem.Cards;
using Salem.Data;
using Salem.Deck;
using Salem.GameFlow;
using Salem.Players;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Salem.Gameplay.Setup
{
    public class GameSetup : MonoBehaviour
    {
        #region Vars
        [SerializeField] private GameManager GameManager;
        [Tooltip("Must Be Ordered: Constable, Witch, Not A Witch")]
        [SerializeField] private ScriptableObject[] TryalCards;
        [SerializeField, Range(0f, 1f), Tooltip("Proportion of players assigned the Witch role")]
        private float witchRatio = 1f / 3f;
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
        //Called In GamePhaseManager durning Setup
        public void SetupNewGame(IReadOnlyList<Player> players, int count)
        {
            SetupTryalCards(players);
            AssignBlackCatAtStart(players);
            SetupInitalHand(players, count);
            SetupTownhallCard(players);
        }
        #endregion

        #region Helper Functions
        private void SetupTryalCards(IReadOnlyList<Player> players)
        {
            int numberOfWitches = Mathf.Max(1, Mathf.RoundToInt(players.Count * witchRatio));
            //Debug.Log($"There are {numberOfWitches} Witches.");

            int numberOfTryalCardsNeeded = players.Count * 5;

            // Add cards to the deck, Start with the Constable
            TryalCard constableCard = (TryalCard)Instantiate(TryalCards[0]);
            constableCard.TryalCardType = TryalCardType.Constable;
            TryalDeck.Add(constableCard);

            //Create our Witch Cards
            for (int i = 0; i < numberOfWitches; i++)
            {
                TryalCard card = (TryalCard)Instantiate(TryalCards[1]);
                card.TryalCardType = TryalCardType.Witch;
                TryalDeck.Add(card);
            }

            //Finish the deck with NotAWitch Cards
            for (int i = TryalDeck.Count; i < numberOfTryalCardsNeeded; i++)
            {
                TryalCard card = (TryalCard)Instantiate(TryalCards[2]);
                card.TryalCardType = TryalCardType.NotAWitch;
                TryalDeck.Add(card);
            }

            //Debug.Log($"There are {TryalDeck.Count} total Tryal Cards.");

            // Shuffle and distribute
            ShuffleTryalDeck(TryalDeck);

            foreach (var player in players)
            {
                player.TryalCards = DrawTryalCards(5, TryalDeck);
                player.InvokeOnTryalCardsChanged();
                player.DetermineRole();
                //give each player a reference to the RNG to be able to randomly decide tryal card. This will likely be replaced later, but I want to just get the systems connected for now
                player.setRng(GameManager.Rng);
            }
        }
        
        private void AssignBlackCatAtStart(IReadOnlyList<Player> players)
        {
            if (DeckManager == null)
            {
                Debug.LogWarning("[GameSetup] Cannot assign Black Cat without a DeckManager reference.");
                return;
            }

            var card = DeckManager.ExtractCardFromDeck("Black Cat");
            if (card == null)
            {
                Debug.LogWarning("[GameSetup] No Black Cat card found in the deck during setup.");
                return;
            }

            if (players == null || players.Count == 0)
            {
                Debug.LogWarning("[GameSetup] No players available to receive the Black Cat. Sending card to discard.");
                DeckManager.AddToDiscardPile(card);
                return;
            }

            int index = Rng.NextInt(0, players.Count);
            var chosenPlayer = players[index];
            chosenPlayer.AssignBlackCat(card);
        }

        //Give the players their starting hand
        private void SetupInitalHand(IReadOnlyList<Player> players, int count)
        {
            foreach (var player in players)
            {
                if (player.HandManager == null)
                {
                    Debug.LogError($"[GameSetup] {player.PlayerNameText} has NULL HandManager. Add HandManager to the SAME GameObject as Player.");
                    continue;
                }
                DeckManager.DrawMultipleCards(player.HandManager, count, ShouldRejectInitialHandCard);
            }
        }

         private bool ShouldRejectInitialHandCard(Card card)
        {
            return card != null && InitialHandRestrictedCards.Contains(card.Name);
        }

        //Give the players their townhall Card
        private void SetupTownhallCard(IReadOnlyList<Player> players)
        {
            foreach (var player in players)
            {
                if (player.HandManager == null)
                {
                    Debug.LogError($"[GameSetup] {player.PlayerNameText} has NULL HandManager. Add HandManager to the SAME GameObject as Player.");
                    continue;
                }
                //draw and set card
                DeckManager.drawTownhallCard(player);
            }
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
            
            //Debug.Log("Shuffled Tryal Deck:");
            /*for (int i = 0; i < TryalDeck.Count; i++)
            {
                Debug.Log($"[{i}] {TryalDeck[i].TryalCardType}");
            }*/
        }
        #endregion
    }
}