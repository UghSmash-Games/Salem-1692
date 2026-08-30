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
using Salem.Cards;
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
            if (assignedCard != null)
            {
                assignedCard.OnRevealed -= UpdateTryalCardVisual;
            }
            assignedCard = card;
            assignedCard.OnRevealed += UpdateTryalCardVisual;

            UpdateTryalCardVisual(assignedCard);
        }
        
        private void OnDestroy()
        {
            if (assignedCard != null)
            {
                assignedCard.OnRevealed -= UpdateTryalCardVisual;
            }
        }

        public void UpdateTryalCardVisual(TryalCard card)
        {
            // SELF-HEALING GUARD. TryalCard is a runtime-Instantiated ScriptableObject, so it
            // OUTLIVES this UI and keeps our OnRevealed subscription alive after the GameObject is
            // gone — OnDestroy does not always get to unsubscribe first (destruction is deferred,
            // and the legacy PlayerBoardUI boards are retired in Networked_Game while their spawned
            // card UIs had already subscribed). Unity's overloaded == reports a destroyed Image as
            // null, so detect it, detach, and bail rather than throwing on every reveal.
            if (CardImage == null)
            {
                if (card != null) card.OnRevealed -= UpdateTryalCardVisual;
                return;
            }

            if (card == null) return;

            CardImage.sprite = card.IsRevealed ? card.RevealedCardImage : card.HiddenCardImage;
        }
        #endregion
    }
}