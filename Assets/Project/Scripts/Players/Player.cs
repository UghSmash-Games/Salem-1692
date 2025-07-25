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
using TMPro;

namespace Salem.Players
{
    public class Player : MonoBehaviour
    {
        #region Vars
        public event Action OnStatusCardsChanged;
        public String PlayerNameText;
        public HandManager HandManager;
        public List<TryalCard> TryalCards = new List<TryalCard>();
        public List<Card> StatusCards { get; private set; } = new();
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
                Debug.Log($"{PlayerNameText} revealed a {card.Type} card!");
            }
            CheckElimination();
        }

        public void AddStatusCard(Card card)
        {
            StatusCards.Add(card);
            OnStatusCardsChanged?.Invoke();
        }

        public void RemoveStatusCard(Card card)
        {
            StatusCards.Remove(card);
            OnStatusCardsChanged?.Invoke();
        }

        public void ClearStatusCards()
        {
            StatusCards.Clear();
            OnStatusCardsChanged?.Invoke();
        }
        
        public virtual void ApplyCardEffect(Card card)
        {
            switch (card.Type)
            {
                case "Green":
                    //played then discarded
                    switch (card.name)
                    {
                        case "Arson":
                            if(PlayerNameText == "Sarah Good") { return; } //sarah good's ability makes her immune to this
                            HandManager.GetCards().Clear();
                            break;
                        case "Robbery":
                            if (PlayerNameText == "Sarah Good") { return; } //sarah good's ability makes her immune to this
                            card.target.HandManager.AddCard(HandManager.GetCards());
                            HandManager.GetCards().Clear();
                            break;
                        case "Alibi":
                            currentAccusationCount -= 3;
                            if(currentAccusationCount < 0) { currentAccusationCount = 0; }
                            break;
                        case "Stocks":
                            skipTurn = true;
                            break;
                        case "Scapegoat":
                            card.target.StatusCards.AddRange(StatusCards);
                            StatusCards.Clear();
                            break;
                    }
                    break;
                case "Blue":
                    // Remain in play
                    break;
                case Card.CardType.Red:
                    //played, then check for tryal reveal
                    switch (card.name)
                    {
                        case "Accusations":
                            currentAccusationCount++;
                            CheckAccusations();
                            break;
                        case "Evidence":
                            currentAccusationCount += 3;
                            if(PlayerNameText == "Cotton Mather") { currentAccusationCount -= 2; } //Cotton mather's ability has evidence only count as 1, so fix the number to reflect that
                            CheckAccusations();
                            break;
                        case "Witness":
                            currentAccusationCount += 7;
                            CheckAccusations();
                            break;
                    }
                    break;
            }
        }
        #endregion

        #region Helper Functions

        private void CheckElimination()
        {
            if (IsEliminated)
            {
                Debug.Log($"{PlayerNameText} is ELIMINATED!");
                //UI Update needs to happen here.
            }
        }
        #endregion
    }
}