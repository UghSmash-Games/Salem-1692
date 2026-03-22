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
using System;
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
        private readonly List<Card> DiscardPile = new List<Card>();
        private Card heldBlackCat;
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
        public void DrawCard(HandManager handManager, Predicate<Card> rejectPredicate = null)
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

            Card drawnCard = null;
            int inspectedCards = 0;
            int deckSnapshotCount = Deck.Count;

            while (drawnCard == null)
            {
                while (Deck.Count > 0 && Deck[0] == null)
                {
                    Debug.LogWarning("[DeckManager] Null card found in Deck. Removing it.");
                    Deck.RemoveAt(0);
                }

                if (Deck.Count == 0)
                {
                    ReshuffleDiscardPile();
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

                    deckSnapshotCount = Deck.Count;
                    inspectedCards = 0;
                }

                var candidate = Deck[0];
                Deck.RemoveAt(0);

                if (rejectPredicate != null && rejectPredicate(candidate))
                {
                    Deck.Add(candidate);
                    inspectedCards++;

                    if (inspectedCards >= deckSnapshotCount)
                    {
                        Debug.LogWarning("[DeckManager] All remaining cards rejected by predicate; dealing restricted card anyway.");
                        Deck.RemoveAt(Deck.Count - 1);
                        drawnCard = candidate;
                    }

                    continue;
                }

                drawnCard = candidate;
            }

            // Give the CardEffectManager a chance to handle immediate effects (Black Cards)
            var owningPlayer = handManager.GetComponent<Salem.Players.Player>();
            bool handled = false;
            if (CardEffectManager.Instance != null)
            {
                handled = CardEffectManager.Instance.HandleCardDrawn(owningPlayer, drawnCard);
            }

            if (handled)
            {
                return;
            }

            if (drawnCard != null && drawnCard.Name == "Black Cat")
            {
                if (owningPlayer != null)
                {
                    owningPlayer.AssignBlackCat(drawnCard);
                }
                else
                {
                    Debug.LogWarning("[DeckManager] Drew Black Cat but could not determine owning player. Sending to discard.");
                    AddToDiscardPile(drawnCard);
                }
                return;
            }

            handManager.AddCard(drawnCard);
        }

        public void DrawMultipleCards(HandManager handManager, int count, Predicate<Card> rejectPredicate = null)
        {
            for (int i = 0; i < count; i++)
            {
                DrawCard(handManager, rejectPredicate);
            }
        }

        public void AddToDiscardPile(Card card)
        {
            if (card == null)
            {
                Debug.LogWarning("[DeckManager] Tried to discard a null card.");
                return;
            }

            DiscardPile.Add(card);
        }

        /// <summary>
        /// Returns a read-only view of the discard pile for UI display (e.g., Samuel Parris ability).
        /// </summary>
        public IReadOnlyList<Card> GetDiscardPileCards() => DiscardPile.AsReadOnly();

        /// <summary>
        /// Draws up to 'count' cards from the discard pile, skipping cards that match the reject predicate.
        /// Used by Samuel Parris's ability (cannot draw Black cards).
        /// </summary>
        public List<Card> DrawFromDiscardPile(HandManager handManager, int count, System.Predicate<Card> rejectPredicate = null)
        {
            var drawn = new List<Card>();
            for (int i = DiscardPile.Count - 1; i >= 0 && drawn.Count < count; i--)
            {
                var card = DiscardPile[i];
                if (card == null) continue;
                if (rejectPredicate != null && rejectPredicate(card)) continue;
                drawn.Add(card);
                DiscardPile.RemoveAt(i);
            }

            if (handManager != null)
            {
                foreach (var card in drawn)
                    handManager.AddCard(card);
            }

            return drawn;
        }

        public Card ExtractCardFromDeck(string cardName)
        {
            if (Deck == null || Deck.Count == 0)
            {
                return null;
            }

            int index = Deck.FindIndex(c => c != null && c.Name == cardName);
            if (index < 0)
            {
                return null;
            }

            var card = Deck[index];
            Deck.RemoveAt(index);
            return card;
        }
        
        public void ReshuffleAndPlaceNightCard(Card nightCard)
        {
            if (nightCard == null)
            {
                return;
            }

            if (Deck.Count == 0)
            {
                Deck.Add(nightCard);
                return;
            }

            int lowerHalfStart = Mathf.Clamp(Deck.Count / 2, 0, Deck.Count);
            int insertIndex = Rng.NextInt(lowerHalfStart, Deck.Count + 1);
            Deck.Insert(insertIndex, nightCard);
        }

        /// <summary>
        /// Post-night reshuffle: merge discard pile into deck, shuffle, then place Night card in bottom half.
        /// </summary>
        public void ReshuffleDeckWithDiscard(Card nightCard)
        {
            Deck.AddRange(DiscardPile);
            DiscardPile.Clear();
            ShuffleDeck();
            ReshuffleAndPlaceNightCard(nightCard);
        }

        public void InsertCardAtRandom(Card card)
        {
            if (card == null) return;
            int index = Rng.NextInt(0, Deck.Count + 1);
            Deck.Insert(index, card);
        }

        public void HoldBlackCatForDawn(Card card)
        {
            heldBlackCat = card;
        }

        public Card GetHeldBlackCat()
        {
            var card = heldBlackCat;
            heldBlackCat = null;
            return card;
        }

        public List<TownHallCard> DrawTownhallCards(int count)
        {
            var drawn = new List<TownHallCard>();
            for (int i = 0; i < count && TownhallDeck.Count > 0; i++)
            {
                drawn.Add(TownhallDeck[0]);
                TownhallDeck.RemoveAt(0);
            }
            return drawn;
        }

        public void DiscardTownhallCard(TownHallCard card)
        {
            // Town Hall cards are one-time use; just discard (no reshuffle needed)
            if (card != null)
                Debug.Log($"[DeckManager] Town Hall card '{card.CardName}' discarded.");
        }
        #endregion

        #region Helper Functions
        public void ShuffleDeck()
        {
            for (int i = 0; i < Deck.Count; i++)
            {
                Card temp = Deck[i];
                int randomIndex = RNGService.Rng.NextInt(0, Deck.Count);
                Deck[i] = Deck[randomIndex];
                Deck[randomIndex] = temp;
            }
        }

        private void ShuffleTownhallDeck()
        {
            for (int i = 0; i < TownhallDeck.Count; i++)
            {
                TownHallCard temp = TownhallDeck[i];
                int randomIndex = Rng.NextInt(0, TownhallDeck.Count);
                TownhallDeck[i] = TownhallDeck[randomIndex];
                TownhallDeck[randomIndex] = temp;
            }
        }

        public void drawTownhallCard(Salem.Players.Player player)
        {
            if (TownhallDeck == null)
            {
                Debug.LogError("[DeckManager] DrawCard: TownhallDeck list is NULL.");
                return;
            }
            if (player == null)
            {
                Debug.LogError("[DeckManager] DrawCard: Player is NULL.");
                return;
            }

            var drawnCard = TownhallDeck[0];
            TownhallDeck.RemoveAt(0);
            player.setTownhall(drawnCard);
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
            ShuffleTownhallDeck();
        }
        #endregion
    }
}