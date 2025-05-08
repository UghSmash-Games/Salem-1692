/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Displays Tryal cards visually.
*   Responsibilities:
*        • Flip, show/hide Tryal card sprites
*   Access Requirements:
*        • Player
*        • TryalCard

* TODO:
*    • Add highlight effects

* FIXME: [Known bugs or issues]
*/
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Salem.UI
{
    public class TryalCardUI : MonoBehaviour
    {

        #region Vars
        //public TextMeshPro cardText;
        [SerializeField] private Image CardImage;
        private TryalCard assignedCard;
        #endregion

        #region Accessor Functions
        public void AssignCard(TryalCard card)
        {
            assignedCard = card;
            UpdateTryalCardVisual(assignedCard);
        }

        public void UpdateTryalCardVisual(TryalCard card)
        {
            if (card.IsRevealed)
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