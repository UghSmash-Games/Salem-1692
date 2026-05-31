/*
* AUTHOR: Cris
* REFERENCES:
* NOTES:
*   Primary Purpose: 
*   Responsibilities:
*        • 
*   Access Requirements:
*        • 
* FIXME:
*    • 
*/
using System;
using UnityEngine;
using UnityEngine.UI;
using Salem.Cards;
using TMPro;
using System.Collections.Generic;

namespace Salem.UI
{
    public class TownHallChoiceUI : MonoBehaviour
    {
        [SerializeField] private GameObject holder;
        [SerializeField] private Transform listParent;
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TextMeshProUGUI headerLabel;
        [SerializeField] private GameObject townHallCardPrefab;

        private TownHallCard selected;
        private TownHallCard optionA;
        private TownHallCard optionB;
        private Action<TownHallCard, TownHallCard> onChosen;
        private readonly List<TownHallCardUI> spawnedCards = new();

        void Awake()
        {
            holder.SetActive(false);
        }

        public void Open(TownHallCard a, TownHallCard b, Action<TownHallCard, TownHallCard> onChosenCallback)
        {
            //Debug.Log($"[TownHallChoiceUI] Running Open");
            optionA = a;
            optionB = b;
            onChosen = onChosenCallback;
            selected = null;

            holder.SetActive(true);
            foreach (Transform c in listParent) Destroy(c.gameObject);
            
            spawnedCards.Clear();

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
            GameObject go = Instantiate(townHallCardPrefab, listParent);

            TownHallCardUI cardUI = go.GetComponent<TownHallCardUI>();

            if (cardUI == null)
            {
                Debug.LogError("[TownHallChoiceUI] TownHallCard prefab is missing TownHallCardUI.");
                return;
            }

            cardUI.Bind(card);
            cardUI.OnClicked += HandleCardSelected;

            spawnedCards.Add(cardUI);
        }

        private void Confirm()
        {
            if (selected == null) return;

            var discarded = (selected == optionA) ? optionB : optionA;
            holder.SetActive(false);
            onChosen?.Invoke(selected, discarded);
        }

        private void HandleCardSelected(TownHallCardUI clickedUI, TownHallCard card)
        {
            selected = card;

            foreach (TownHallCardUI cardUI in spawnedCards)
            {
                cardUI.SetSelected(cardUI == clickedUI);
            }

            if (confirmButton != null)
                confirmButton.interactable = true;
        }
    }
}
