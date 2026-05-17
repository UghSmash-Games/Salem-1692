/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Resolves logic when cards are played.
*   Responsibilities:
*        • Interpret effect type
*        • Trigger gameplay consequences
*   Access Requirements:
*        • DeckManager
*        • Player
*        • GameStateManager

* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using System;
using System.Collections.Generic;
using Salem.Cards;
using Salem.Players;
using UnityEngine;
using UnityEngine.UI;

namespace Salem.UI
{
    public class TryalPickerUI : MonoBehaviour
    {
        [SerializeField] private GameObject holder;
        [SerializeField] private Transform listParent;
        [SerializeField] private GameObject buttonPrefab;

        void Awake()
        {
            holder.SetActive(false);
        }

        public void Open(Player target, Action<int> onIndexChosen)
        {
             holder.SetActive(true);
            foreach (Transform c in listParent) Destroy(c.gameObject);
            var indices = new List<int>();
            for (int i = 0; i < target.TryalCards.Count; i++)
                if (!target.TryalCards[i].IsRevealed) indices.Add(i);

            foreach (var idx in indices)
            {
                var b = Instantiate(buttonPrefab, listParent).GetComponent<Button>();
                b.GetComponentInChildren<TMPro.TMP_Text>().text = $"Reveal Tryal {idx+1}";
                int captured = idx;
                b.onClick.AddListener(() => { onIndexChosen?.Invoke(captured); holder.SetActive(false); });
            }
        }
    }
}
