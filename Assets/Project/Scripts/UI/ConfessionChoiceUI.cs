using System;
using Salem.Players;
using Salem.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Salem.UI
{
    public class ConfessionChoiceUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text promptText;

        [Header("Buttons")]
        [SerializeField] private Button confessButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button fakeConfessButton;

        private Action<ConfessionChoice> onChoiceSelected;

        public enum ConfessionChoice
        {
            Skip,
            Confess,
            FakeConfess
        }

        private void Awake()
        {
            Hide();

            confessButton.onClick.AddListener(() => Choose(ConfessionChoice.Confess));
            skipButton.onClick.AddListener(() => Choose(ConfessionChoice.Skip));
            fakeConfessButton.onClick.AddListener(() => Choose(ConfessionChoice.FakeConfess));
        }

        private void OnDestroy()
        {
            confessButton.onClick.RemoveAllListeners();
            skipButton.onClick.RemoveAllListeners();
            fakeConfessButton.onClick.RemoveAllListeners();
        }

        public void Open(Player player, Action<ConfessionChoice> callback)
        {
            onChoiceSelected = callback;

            bool canFakeConfess =
                player.HasTownHall(TownhallName.WilliamsPhipps) &&
                player.townHallAbilityCharges > 0;

            if (promptText != null)
            {
                promptText.text = $"{player.PlayerNameText}, do you want to confess?";
            }

            fakeConfessButton.gameObject.SetActive(canFakeConfess);

            panelRoot.SetActive(true);
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void Choose(ConfessionChoice choice)
        {
            Hide();

            Action<ConfessionChoice> callback = onChoiceSelected;
            onChoiceSelected = null;

            callback?.Invoke(choice);
        }
    }
}