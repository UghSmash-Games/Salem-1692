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
using Salem.Cards;
using Salem.Data;
using Salem.GameFlow;
using Salem.Players;


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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            CardEffectManager.OnCardPlayed += HandleCardPlayed;
            Player.AccusationCountChanged += HandleAccusationChanged;
            Player.AccusationThresholdReached += HandleAccusationThreshold;
            Player.TryalCardRevealed += HandleTryalRevealed;
            PlayerService.OnPlayerEliminated += HandlePlayerEliminated;
            TrialService.OnDoubleWitchRevealed += HandleDoubleWitchRevealed;
        }


        private void OnDisable()
        {
            CardEffectManager.OnCardPlayed -= HandleCardPlayed;
            Player.AccusationCountChanged -= HandleAccusationChanged;
            Player.AccusationThresholdReached -= HandleAccusationThreshold;
            Player.TryalCardRevealed -= HandleTryalRevealed;
            PlayerService.OnPlayerEliminated -= HandlePlayerEliminated;
            TrialService.OnDoubleWitchRevealed -= HandleDoubleWitchRevealed;
        }

        private void HandleCardPlayed(string message) => AddLogEntry(message);

        private void HandleAccusationChanged(Player player, byte count, byte limit)
        {
            if (player == null)
            {
                return;
            }

            AddLogEntry($"{player.PlayerNameText} accusations: {count}/{limit}.");
        }

        private void HandleAccusationThreshold(Player player, byte count, byte limit)
        {
            if (player == null)
            {
                return;
            }

            AddLogEntry($"{player.PlayerNameText} reached their accusation limit ({limit}). Revealing a Tryal card...");
        }

        private void HandleTryalRevealed(Player player, TryalCard card)
        {
            if (player == null)
            {
                return;
            }

            string result = card != null ? card.TryalCardType.ToString() : "Unknown";
            AddLogEntry($"{player.PlayerNameText} revealed {result}.");
        }

        private void HandlePlayerEliminated(Player player, EliminationCause cause)
        {
            if (player == null)
            {
                return;
            }

            AddLogEntry($"{player.PlayerNameText} was eliminated ({cause}).");
        }

        private void HandleDoubleWitchRevealed(Player player)
        {
            if (player == null) return;
            AddLogEntry($"WARNING: {player.PlayerNameText} revealed a Witch card but has a SECOND Witch card! They survive.");
        }

        public static void Log(string message)
        {
            if (Instance == null)
            {
                Debug.Log($"[CardLog] {message}");
                return;
            }

            Instance.AddLogEntry(message);
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

