/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Salem.GameFlow;


namespace Salem.UI
{
    public class CardLogManager : MonoBehaviour
    {
        public static CardLogManager Instance;


        [Header("UI Components")]
        [SerializeField] private GameObject logEntryPrefab;
        [SerializeField] private Transform logContainer;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private int maxEntries = 25;


        private Queue<GameObject> logEntries = new();


        private void OnEnable()
        {
            CardEffectManager.OnCardPlayed += HandleCardPlayed;
        }


        private void OnDisable()
        {
            CardEffectManager.OnCardPlayed -= HandleCardPlayed;
        }


        private void HandleCardPlayed(string message)
        {
            AddLogEntry(message);
        }


        private void AddLogEntry(string message)
        {
            GameObject newEntry = Instantiate(logEntryPrefab, logContainer);
            TMP_Text entryText = newEntry.GetComponentInChildren<TMP_Text>();


            if (entryText != null)
                entryText.text = message;


            logEntries.Enqueue(newEntry);


            if (logEntries.Count > maxEntries)
            {
                GameObject oldest = logEntries.Dequeue();
                Destroy(oldest);
            }


            // Auto-scroll to bottom
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}

