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
using System.Collections.Generic;
using UnityEngine;
using Salem.Deck;
using Salem.Cards;
using Salem.UI;
using System.Linq;

namespace Salem.Managers.Hands
{
    public class HandManager : MonoBehaviour
    {
        public event Action OnHandChanged;
        #region Vars
        public List<Card> Hand = new List<Card>();

        #endregion

        #region Accessor Functions
        public List<Card> GetCards()
        {
            return new List<Card>(Hand);
        }
        /*
        public void PlayCard(Card card, Player target)
        {
            Hand.Remove(card);
            target.ApplyCardEffect(card);
        }
        */

        public void AddCard(Card card)
        {
            Hand.Add(card);
            OnHandChanged?.Invoke();
        }

        public void RemoveCard(Card card)
        {
            if (Hand.Remove(card)) { OnHandChanged?.Invoke(); }
        }

        public void ClearHand()
        {
            Hand.Clear();
            OnHandChanged?.Invoke();
        }
        #endregion
    }
}