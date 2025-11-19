/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Visual representation of a player's hand.
*   Responsibilities:
*        • Spawn card prefabs
*        • Update card visuals
*   Access Requirements:
*        • HandManager
*        • Card
*        • TryalCardUI

* TODO:
*    • Pool card objects for performance

* FIXME: [Known bugs or issues]
*/
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Salem.Managers.Hands;
using Salem.Cards;
using Salem.Data;

namespace Salem.UI
{
    public class PlayerHandUI : MonoBehaviour
    {
        #region Vars 
        [SerializeField] private Transform handPanel;
        [SerializeField] private Transform statusCardContainer;
        [SerializeField] private Transform tryalCardContainer;
        [SerializeField] private GameObject cardUIPrefab;
        [SerializeField] private GameObject tryalCardPrefab;

        private List<GameCardUI> cardUIElements = new List<GameCardUI>();
        #endregion

        #region Accessor Functions
        public void UpdateFromData(PlayerHandData data)
        {
            UpdateHand(data.HandCards);
            UpdateTryalCards(data.TryalCards);
        }

        private void UpdateHand(List<Card> hand)
        {
            foreach (Transform child in handPanel) Destroy(child.gameObject);
            cardUIElements.Clear();

            foreach (Card card in hand)
            {
                GameObject cardGO = Instantiate(cardUIPrefab, handPanel);
                GameCardUI cardUI = cardGO.GetComponent<GameCardUI>();
                cardUI.SetCard(card, true);
                cardUIElements.Add(cardUI);
            }
        }

        private void UpdateTryalCards(List<TryalCard> tryalCards)
        {
            foreach (Transform child in tryalCardContainer) Destroy(child.gameObject);

            foreach (TryalCard card in tryalCards)
            {
                GameObject tryalCardUI = Instantiate(tryalCardPrefab, tryalCardContainer);
                tryalCardUI.GetComponent<TryalCardUI>().AssignCard(card);
            }
        }
        //Called in GameManger at Start, Will need to be called when Cards are used
        /*public void UpdateHand(List<Card> hand)
        {
            //Reset Hand
            foreach (Transform child in handPanel) Destroy(child.gameObject);
            cardUIElements.Clear();

            //Re-Populate Hand
            foreach (Card card in hand)
            {
                GameObject cardGO = Instantiate(cardUIPrefab, handPanel);
                GameCardUI cardUI = cardGO.GetComponent<GameCardUI>();
                cardUI.SetCard(card);
                cardUIElements.Add(cardUI);
            }
        }
        */

        //Called In Game Manager At Start
        /*
        public void PopulateTryalCards(Player player)
        {
            // Clear existing cards
            foreach (Transform child in tryalCardContainer.transform)
            {
                Destroy(child.gameObject);
            }

            // Instantiate new Tryal Cards
            foreach (TryalCard card in player.TryalCards)
            {
                GameObject tryalCardUI = Instantiate(tryalCardPrefab, tryalCardContainer.transform);
                tryalCardUI.GetComponent<TryalCardUI>().AssignCard(card);
            }
        }
        */
        #endregion
    }
}