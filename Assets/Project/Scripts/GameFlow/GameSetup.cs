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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Salem.Players;
using Salem.Deck;
using Salem.Cards;
using Salem.GameFlow;

namespace Salem.Gameplay.Setup
{
    public class GameSetup : MonoBehaviour
    {
        #region Vars
        [Tooltip("Must Be Ordered: Constable, Witch, Not A Witch")]
        [SerializeField] private ScriptableObject[] TryalCards;
        private List<TryalCard> TryalDeck = new List<TryalCard>();
        private DeckManager DeckManager;
        private GameManager GameManager;
        #endregion

        #region Standard Functions
        void Awake()
        {
            DeckManager = GetComponent<DeckManager>();
            GameManager = GetComponent<GameManager>();
        }
        #endregion

        #region Accessor Functions
        //Called In GamePhaseManager durning Setup
        public void SetupNewGame(List<Player> players, int count)
        {
            SetupTryalCards(players);
            SetupInitalHand(players, count);
            GameManager.UpdateLocalPlayerUI();
        }
        #endregion

        #region Helper Functions
        private void SetupTryalCards(List<Player> players)
        {
            int numberOfWitches = players.Count / 3; 
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
                player.DetermineRole();
            }
        }

        //Give the players their starting hand
        private void SetupInitalHand(List<Player> players, int count)
        {
            foreach (var player in players)
            {
                DeckManager.DrawMultipleCards(player.HandManager, count);
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
                int randomIndex = Random.Range(i, deck.Count);
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