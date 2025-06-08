/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Represents each player’s state, hand, and Tryal cards.
*   Responsibilities:
*        • Track Tryal cards
*        • Check elimination
*        • Receive cards
*   Access Requirements:
*        • HandManager
*        • TryalCard
*        • GameStateManager
*        • PlayerHandUI

* TODO:
*   • Split state/data if needed
*   • Expose public events for changes
* FIXME: [Known bugs or issues]
*/
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Salem.Managers.GameState;
using Salem.Managers.Hands;
using Salem.Cards;
using Salem.UI;
using TMPro;

namespace Salem.Players
{
    public class Player : MonoBehaviour
    {
        #region Vars
        public string PlayerName;
        public HandManager HandManager;
        public List<TryalCard> TryalCards = new List<TryalCard>();
        public List<Card> StatusCards { get; private set; } = new();
        public PlayerStatusUI StatusUI;
        public PlayerInputUI InputUI;
        public bool IsLocalPlayer = false;
        public bool IsWitch { get; private set; }  // Now determined dynamically
        public bool IsEliminated => TryalCards.TrueForAll(card => card.IsRevealed);
        #endregion

        #region Standard Functions
        void Awake()
        {
            HandManager = GetComponent<HandManager>();
        }
        #endregion

        #region Accessor Functions
        public void DetermineRole()
        {
            // A player is a Witch if they have at least one Witch TryalCard
            IsWitch = TryalCards.Any(card => card.TryalCardType == TryalCardType.Witch);
        }

        public void RevealTryalCard(int index)
        {
            if (index < 0 || index >= TryalCards.Count) return;

            TryalCard card = TryalCards[index];
            if (!card.IsRevealed)
            {
                card.Reveal();
                Debug.Log($"{PlayerName} revealed a {card.Type} card!");
            }
            CheckElimination();
        }
        
        public void AddStatusCard(Card card)
        {
            StatusCards.Add(card);
            StatusUI?.UpdateStatusCards();  // Optional: UI refresh
        }

        public void RemoveStatusCard(Card card)
        {
            if (StatusCards.Remove(card))
            {
                StatusUI?.UpdateStatusCards();
            }
        }

        public void ClearStatusCards()
        {
            StatusCards.Clear();
            StatusUI?.UpdateStatusCards();
        }
        #endregion

        #region Helper Functions
        //Called in Hand Manager.
        internal void ApplyCardEffect(Card card)
        {
            switch (card.Type)
            {
                case "Green":
                    // Discard after use
                    break;
                case "Blue":
                    // Remain in play
                    break;
                case "Red":
                    // Stack effects until limit
                    break;
            }
        }

        private void CheckElimination()
        {
            if(IsEliminated)
            {
                Debug.Log($"{PlayerName} is ELIMINATED!");
                //UI Update needs to happen here.
            }
        }
        #endregion
    }
}