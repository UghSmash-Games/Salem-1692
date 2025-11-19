/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: General testing utilities (e.g., auto-setup AI matches).
*   Responsibilities:
*        • Quick launch of configurations
*        • Automated input hooks
*   Access Requirements:
*        • GameSetup
*        • PlayerManager

* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using System.Collections;
using UnityEngine;
using Salem.Data;
using Salem.Deck;
using Salem.GameFlow;
using Salem.Players;
using Salem.UI;

namespace Salem.DebugTools
{
    public class TestManager : MonoBehaviour
    {
        [Header("Auto Test / Autopilot")]
        [SerializeField] private bool autoStartOnEnable = false;
        [SerializeField, Tooltip("Automatically autopilot the local player when their turn begins.")]
        private bool autopilotLocalPlayer = true;
        [SerializeField, Tooltip("If true, every human player (local or remote) will be autopiloted during tests.")]
        private bool autopilotAllHumans = false;
        [SerializeField, Range(0f, 5f)] private float autopilotThinkDelay = 1.25f;
        [SerializeField, Tooltip("Optional delay before the test manager attempts to hook into turn events.")]
        private float bootstrapDelay = 0.25f;
        [SerializeField] private DeckManager deckManager;

        private Coroutine bootstrapRoutine;
        private Coroutine activeTurnRoutine;
        private bool autoSequenceEnabled;

        private void Awake()
        {
            if (!deckManager)
            {
                deckManager = FindFirstObjectByType<DeckManager>();
            }
        }

        private void OnEnable()
        {
            if (autoStartOnEnable)
            {
                StartAutoSequence();
            }
        }

        private void OnDisable()
        {
            StopAutoSequence();
        }

        [ContextMenu("Start Auto Test Sequence")]
        public void StartAutoSequence()
        {
            if (autoSequenceEnabled)
            {
                return;
            }

            if (bootstrapRoutine != null)
            {
                StopCoroutine(bootstrapRoutine);
            }

            bootstrapRoutine = StartCoroutine(BootstrapAndListen());
        }

        [ContextMenu("Stop Auto Test Sequence")]
        public void StopAutoSequence()
        {
            if (bootstrapRoutine != null)
            {
                StopCoroutine(bootstrapRoutine);
                bootstrapRoutine = null;
            }

            if (!autoSequenceEnabled)
            {
                return;
            }

            if (GameTurnManager.Instance != null)
            {
                GameTurnManager.Instance.TurnStarted -= HandleTurnStarted;
            }

            if (activeTurnRoutine != null)
            {
                StopCoroutine(activeTurnRoutine);
                activeTurnRoutine = null;
            }

            autoSequenceEnabled = false;
            CardLogManager.Log("[AutoTest] Auto test sequence disabled.");
        }

        private IEnumerator BootstrapAndListen()
        {
            if (bootstrapDelay > 0f)
            {
                yield return new WaitForSeconds(bootstrapDelay);
            }

            yield return new WaitUntil(() => GameTurnManager.Instance != null);
            yield return new WaitUntil(() => PlayerService.GetAlivePlayers().Count > 0);

            if (GameTurnManager.Instance == null)
            {
                yield break;
            }

            GameTurnManager.Instance.TurnStarted += HandleTurnStarted;
            autoSequenceEnabled = true;
            bootstrapRoutine = null;
            CardLogManager.Log("[AutoTest] Auto test sequence enabled.");
        }

        private void HandleTurnStarted(Player player)
        {
            if (!autoSequenceEnabled || player == null)
            {
                return;
            }

            if (!ShouldAutopilot(player))
            {
                return;
            }

            if (activeTurnRoutine != null)
            {
                StopCoroutine(activeTurnRoutine);
            }

            activeTurnRoutine = StartCoroutine(AITurnSequencer.ExecuteTurn(player, deckManager, autopilotThinkDelay, true));
            CardLogManager.Log($"[AutoTest] Autopiloting {player.PlayerNameText}'s turn.");
        }

        private bool ShouldAutopilot(Player player)
        {
            if (!player.IsHuman)
            {
                return false;
            }

            if (autopilotAllHumans)
            {
                return true;
            }

            return autopilotLocalPlayer && player.IsLocalPlayer;
        }
    }
}