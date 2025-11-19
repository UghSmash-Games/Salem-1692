/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Handles card lists for each player’s hand.
*   Responsibilities:
*        • Add and remove cards
*        • Check red card total
*   Access Requirements:
*        • DeckManager
*        • Card
*        • PlayerHandUI

* TODO:
*    • Track Cards from DeckManager
*    • Add Cards to Hand
*    • Manage Discarding Cards via HandManager
*    • Use OnHandChanged event

* FIXME: [Known bugs or issues]
*/
using System;
using System.Collections.Generic;
using System.Linq;
using Salem.Cards;
using Salem.Deck;
using Salem.UI;
using UnityEngine;

namespace Salem.Managers.Hands
{
    public class HandManager : MonoBehaviour
    {
        public event Action OnHandChanged;
        #region Vars
        [SerializeField] private DeckManager deckManager;
        public List<Card> Hand = new List<Card>();
        #endregion

        #region Standard Functions
        private void OnValidate()
        {
            if (!deckManager) deckManager = FindFirstObjectByType<DeckManager>();
        }

        private void Awake()
        {
            if (!deckManager)
            {
                deckManager = FindFirstObjectByType<DeckManager>();
                if (!deckManager)
                {
                    Debug.LogWarning("[HandManager] DeckManager reference is missing; discards will be lost.");
                }
            }
        }
        #endregion

        #region Accessor Functions
        public List<Card> GetCards()
        {
            return new List<Card>(Hand);
        }

        public void AddCard(Card card)
        {
            Hand.Add(card);
            OnHandChanged?.Invoke();
        }

        public void RemoveCard(Card card)
        {
            if (Hand.Remove(card))
            {
                deckManager?.AddToDiscardPile(card);
                OnHandChanged?.Invoke();
            }
        }

        public void ClearHand()
        {
            Hand.Clear();
            OnHandChanged?.Invoke();
        }
        #endregion
    }
}