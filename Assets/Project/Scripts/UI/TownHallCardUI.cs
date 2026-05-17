using System;
using Salem.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Salem.UI
{
    public class TownHallCardUI : MonoBehaviour
    {
        [SerializeField] private Image townHallCardImage;
        [SerializeField] private TMP_Text cardNameText;
        [SerializeField] private TMP_Text rulesText;
        [SerializeField] private Button cardButton;
        [SerializeField] private GameObject selectedHighlight;

        private TownHallCard boundCard;

        public event Action<TownHallCardUI, TownHallCard> OnClicked;

        private void Awake()
        {
            if (cardButton == null)
                cardButton = GetComponent<Button>();

            if (cardButton != null)
                cardButton.onClick.AddListener(HandleClicked);

            SetSelected(false);
        }

        private void OnDestroy()
        {
            if (cardButton != null)
                cardButton.onClick.RemoveListener(HandleClicked);
        }

        public void Bind(TownHallCard card)
        {
            boundCard = card;

            if (boundCard == null)
                return;

            if (townHallCardImage != null)
                townHallCardImage.sprite = boundCard.RevealedCardImage;

            if (cardNameText != null)
                cardNameText.text = boundCard.CardName.ToString();

            if (rulesText != null)
                rulesText.text = boundCard.GetRulesText();

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectedHighlight != null)
                selectedHighlight.SetActive(selected);
        }

        private void HandleClicked()
        {
            if (boundCard == null)
                return;

            Debug.Log($"[TownHallCardUI] Clicked {boundCard.CardName}");

            OnClicked?.Invoke(this, boundCard);
        }
    }
}