
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
        public Card Card => card;
        
        private Card card;
        private bool faceUp;
        #endregion

        #region Accessor Functions
        public void SetCard(Card c, bool isFaceUp)
        {
            card = c;
            faceUp = isFaceUp;
            Refresh();
        }

        public void SetFaceUp(bool isFaceUp)
        {
            faceUp = isFaceUp;
            Refresh();
        }
        #endregion

        private void Refresh()
        {
            if (card == null || CardImage == null) return;

            // Front for face-up, back for face-down
            var sprite = faceUp ? card.RevealedCardImage : card.HiddenCardImage;
            CardImage.sprite = sprite;

            // Optional: fallback if a sprite is missing
            if (CardImage.sprite == null)
                Debug.LogWarning($"[GameCardUI] {card?.Name} missing {(faceUp ? "Revealed" : "Hidden")} sprite.");
        }
    }
}