using System;
using UnityEngine;
using UnityEngine.UI;
using Salem.Cards;
using TMPro;

namespace Salem.UI
{
    public class TownHallChoiceUI : MonoBehaviour
    {
        [SerializeField] private Transform listParent;
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TextMeshProUGUI headerLabel;

        private TownHallCard selected;
        private TownHallCard optionA;
        private TownHallCard optionB;
        private Action<TownHallCard, TownHallCard> onChosen;

        public void Open(TownHallCard a, TownHallCard b, Action<TownHallCard, TownHallCard> onChosenCallback)
        {
            optionA = a;
            optionB = b;
            onChosen = onChosenCallback;
            selected = null;

            gameObject.SetActive(true);
            foreach (Transform c in listParent) Destroy(c.gameObject);

            if (headerLabel != null)
                headerLabel.text = "Choose a Town Hall Card";

            SpawnOption(a);
            SpawnOption(b);

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(Confirm);
                confirmButton.interactable = false;
            }
        }

        private void SpawnOption(TownHallCard card)
        {
            var go = Instantiate(buttonPrefab, listParent);
            var btn = go.GetComponent<Button>();
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = card.CardName.ToString();

            btn.onClick.AddListener(() =>
            {
                selected = card;
                if (confirmButton != null)
                    confirmButton.interactable = true;
            });
        }

        private void Confirm()
        {
            if (selected == null) return;

            var discarded = (selected == optionA) ? optionB : optionA;
            gameObject.SetActive(false);
            onChosen?.Invoke(selected, discarded);
        }
    }
}
