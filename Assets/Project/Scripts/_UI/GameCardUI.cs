
/*
* AUTHOR:
* REFERENCES:
* NOTES:
*   Primary Purpose: Displays playable hand cards.
*   Responsibilities:
*        • Render card sprite, tooltip
*   Access Requirements:
*        • Card
*        • PlayerHandUI

* TODO:
*    • Handle hover or play animations

* FIXME: [Known bugs or issues]
*/
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Salem.Cards;

namespace Salem.UI
{
    public class GameCardUI : MonoBehaviour
    {
        #region Vars
        [SerializeField] private Image CardImage;
        private Image revealedCardImage;
        //public TextMeshPro cardNameText;
        
        private Card card;
        #endregion

        #region Accessor Functions
        public void SetCard(Card newCard)
        {
            card = newCard;
            UpdatePlayingCardVisual(card); 
        }

        public void UpdatePlayingCardVisual(Card card)
        {
            if (card.IsPlayed)
            { 
                CardImage.sprite = card.RevealedCardImage;
            }
            else
            {
                CardImage.sprite = card.HiddenCardImage;
            }
        }
        #endregion
    }
}