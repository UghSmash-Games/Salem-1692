
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
using Salem.Players;
using UnityEngine.EventSystems;
using System;

namespace Salem.UI
{
    public class GameCardUI : MonoBehaviour
    {
        #region Vars
        [SerializeField] private Image CardImage;
        [SerializeField] private Button cardButton;
        public Card Card => boundCard;

        public event Action<Card, Player> OnCardClicked;
        
        private Card boundCard;
        private bool faceUp;
        private Player owner;
        #endregion

        private void Awake()
        {
            if (cardButton == null)
                cardButton = GetComponent<Button>();

            cardButton.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            cardButton.onClick.RemoveListener(HandleClicked);
        }

        #region Accessor Functions
        public void SetCard(Card c, bool isFaceUp, Player playerOwner)
        {
            boundCard = c;
            faceUp = isFaceUp;
            owner = playerOwner;

            Debug.Log($"[GameCardUI] SetCard: {boundCard?.Name} | Owner: {owner?.PlayerNameText}");

            Refresh();
        }

        public void SetFaceUp(bool isFaceUp)
        {
            faceUp = isFaceUp;
            Refresh();
        }
        #endregion

        private void HandleClicked()
        {
            Debug.Log($"[GameCardUI] Clicked card: {gameObject.name}");

            if (boundCard == null || owner == null)
                return;

            OnCardClicked?.Invoke(boundCard, owner);
        }

        private void Refresh()
        {
            if (boundCard == null || CardImage == null) return;

            // Front for face-up, back for face-down
            var sprite = faceUp ? boundCard.RevealedCardImage : boundCard.HiddenCardImage;
            CardImage.sprite = sprite;

            // Optional: fallback if a sprite is missing
            if (CardImage.sprite == null)
                Debug.LogWarning($"[GameCardUI] {boundCard?.Name} missing {(faceUp ? "Revealed" : "Hidden")} sprite.");
        }
    }
}