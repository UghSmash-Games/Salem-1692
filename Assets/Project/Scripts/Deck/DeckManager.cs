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
using Salem.Data;
using Salem.GameFlow;

namespace Salem.Deck
{
    public class DeckManager : MonoBehaviour
    {
        #region Vars
        [SerializeField] private GameManager GameManager;
        [Tooltip("Populate with cards in Inspector")]
        [SerializeField] private List<Card> Deck = new List<Card>();
        [Tooltip("Populate with cards in Inspector")]
        [SerializeField] private List<TownHallCard> TownhallDeck = new List<TownHallCard>();
        private List<Card> DiscardPile = new List<Card>();
        private IRng Rng => GameManager != null ? GameManager.Rng : _fallbackRng;
        private readonly IRng _fallbackRng = new XorShiftRng(1UL); // only if GM missing
        #endregion

        #region Standard Functions
        private void OnValidate()
        {
            if (Deck != null) Deck.RemoveAll(c => c == null);
            if (!GameManager) GameManager = FindFirstObjectByType<GameManager>();
        }
        void Awake()
        {
            if (Deck != null) Deck.RemoveAll(c => c == null);
            if (!GameManager) Debug.LogError("[Deck Manager] Missing GameManager reference for RNG.");
        }
        private void Start()
        {
            InitializeDeck();
        }
        #endregion

        #region Accessor Functions
        public void DrawCard(HandManager handManager)
        {
            if (handManager == null)
            {
                Debug.LogError("[DeckManager] DrawCard: HandManager is NULL (did the Player object have a HandManager component on the same GameObject?).");
                return;
            }

            if (Deck == null)
            {
                Debug.LogError("[DeckManager] DrawCard: Deck list is NULL.");
                return;
            }

            while (Deck.Count > 0 && Deck[0] == null)
            {
                Debug.LogWarning("[DeckManager] Null card found in Deck. Removing it.");
                Deck.RemoveAt(0);
            }

            if (Deck.Count == 0)
            {
                ReshuffleDiscardPile();
                // after reshuffle, try once more to skip any nulls
                while (Deck.Count > 0 && Deck[0] == null)
                {
                    Debug.LogWarning("[DeckManager] Null card found in Deck after reshuffle. Removing it.");
                    Deck.RemoveAt(0);
                }
                if (Deck.Count == 0)
                {
                    Debug.LogError("[DeckManager] No cards available to draw.");
                    return;
                }
            }

            var drawnCard = Deck[0];
            Deck.RemoveAt(0);
            handManager.AddCard(drawnCard);

            if (drawnCard.Type == Card.CardColor.Black)
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
                int randomIndex = Rng.NextInt(0, Deck.Count);
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