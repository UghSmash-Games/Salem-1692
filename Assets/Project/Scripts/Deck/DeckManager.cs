/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Manages card deck, draw, discard, and reshuffling.
*   Responsibilities:
*        • Shuffle
*        • Draw
*        • Discard
*   Access Requirements:
*        • Card
*        • HandManager

* TODO: [Planned improvements]
* FIXME: Rework Draw Cards to work through HandManager and Not Player
*/
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Salem.Cards;
using Salem.Managers.Hands;

namespace Salem.Deck
{
    public class DeckManager : MonoBehaviour
    {
        #region Vars
        [Tooltip("Populate with cards in Inspector")]
        [SerializeField] private List<Card> Deck = new List<Card>();
        private List<Card> DiscardPile = new List<Card>();
        #endregion

        #region Standard Functions
        private void Start()
        {
            InitializeDeck();
        }
        #endregion

        #region Accessor Functions
        public void DrawCard(HandManager handManager)
        {
            if (Deck.Count == 0)
            {
                ReshuffleDiscardPile();
            }

            Card drawnCard = Deck[0];
            Deck.RemoveAt(0);
            handManager.AddCard(drawnCard);

            if (drawnCard.Type == Card.CardType.Black)
            {
                ResolveBlackCardEffect(drawnCard);
            }
        }

        public void DrawMultipleCards(HandManager handManager, int count)
        {
            for (int i = 0; i < count; i++)
            {
                DrawCard(handManager);
            }
        }
        #endregion

        #region Helper Functions
        private void ResolveBlackCardEffect(Card card)
        {
            Debug.Log("Function Not Implemented");
            throw new System.NotImplementedException();
        }

        private void ShuffleDeck()
        {
            for (int i = 0; i < Deck.Count; i++)
            {
                Card temp = Deck[i];
                int randomIndex = Random.Range(0, Deck.Count);
                Deck[i] = Deck[randomIndex];
                Deck[randomIndex] = temp;
            }
        }

        private void ReshuffleDiscardPile()
        {
            Deck.AddRange(DiscardPile);
            DiscardPile.Clear();
            ShuffleDeck();
        }

        private void InitializeDeck()
        {
            ShuffleDeck();
        }
        #endregion
    }
}