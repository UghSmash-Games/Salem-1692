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
using Salem.Cards;
using Salem.Characters;
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

        // ─── TEMP (Phase 5 #6 — Martha re-resolve verification). Deterministic hooks so the
        // four Martha cases can be triggered from the inspector without live gameplay. Set
        // debugSeat, then right-click the component → run a menu item. REMOVE at end of Phase 5.
        [Header("TEMP — Phase 5 Martha re-resolve debug")]
        [SerializeField, Tooltip("Seat index into PlayerService.All the debug actions below target.")]
        private int debugSeat = 0;
        [SerializeField, Tooltip("Drag 'Evidence 1' (Red Cards). Used by 'Add Evidence To Seat'.")]
        private ActionCardSO debugEvidenceCard;
        [SerializeField, Tooltip("Second seat index — the matchmaker partner for 'Link Matchmaker'.")]
        private int debugSeatB = 1;
        [SerializeField, Tooltip("Drag 'Matchmaker 1' (Blue Cards). Used by 'Link Matchmaker Seat<->SeatB'.")]
        private ActionCardSO debugMatchmakerCard;

        private static Player DebugSeat(int seat)
        {
            var all = PlayerService.All;
            if (seat < 0 || seat >= all.Count)
            {
                Debug.LogWarning($"[MarthaDebug] seat {seat} out of range (0..{all.Count - 1}).");
                return null;
            }
            return all[seat];
        }

        [ContextMenu("DEBUG — Dump Seat State")]
        private void DebugDumpSeat() => DebugDump(DebugSeat(debugSeat));

        private static void DebugDump(Player p)
        {
            if (p == null) return;
            Debug.Log($"[MarthaDebug] seat '{p.PlayerNameText}' card={p.townhallCard?.CardName.ToString() ?? "none"} " +
                      $"effective={p.GetEffectiveTownHallName()?.ToString() ?? "none"} " +
                      $"base={p.baseAccusationLimit} current={p.currentAccusationLimit} " +
                      $"count={p.currentAccusationCount} charges={p.townHallAbilityCharges} " +
                      $"eliminated={p.IsEliminated}");
        }

        [ContextMenu("DEBUG — Eliminate Seat")]
        private void DebugEliminateSeat()
        {
            var p = DebugSeat(debugSeat);
            if (p == null) return;
            Debug.Log($"[MarthaDebug] eliminating seat {debugSeat} '{p.PlayerNameText}'.");
            PlayerService.Eliminate(p, EliminationCause.Other);
        }

        [ContextMenu("DEBUG — Consume TownHall Charge Seat")]
        private void DebugConsumeChargeSeat()
        {
            var p = DebugSeat(debugSeat);
            if (p == null) return;
            p.ConsumeTownHallCharge();
            Debug.Log($"[MarthaDebug] consumed a charge on seat {debugSeat} '{p.PlayerNameText}'.");
            DebugDump(p);
        }

        [ContextMenu("DEBUG — Add Evidence To Seat")]
        private void DebugAddEvidenceSeat()
        {
            var p = DebugSeat(debugSeat);
            if (p == null) return;
            if (debugEvidenceCard == null) { Debug.LogWarning("[MarthaDebug] assign debugEvidenceCard first."); return; }
            p.AddStatusCard(debugEvidenceCard);
            p.ApplyAccusation(0); // recompute count from status cards (evidence: 1 if Cotton-copy, else 3)
            Debug.Log($"[MarthaDebug] added Evidence to seat {debugSeat} '{p.PlayerNameText}'.");
            DebugDump(p);
        }

        // Link debugSeat ↔ debugSeatB as matchmaker partners: gives BOTH a Matchmaker status card
        // (so HasStatus("Matchmaker") is true on each) and sets the two-way MatchedPlayer bond, exactly
        // what PlayerService.Eliminate's cascade capture requires. Used for the John-draft cascade edge:
        // link a John Proctor to another player, then Eliminate the PARTNER — the cascade kills John
        // (the only drafter) while the partner's hand was already held for the draft.
        [ContextMenu("DEBUG — Link Matchmaker Seat<->SeatB")]
        private void DebugLinkMatchmaker()
        {
            var a = DebugSeat(debugSeat);
            var b = DebugSeat(debugSeatB);
            if (a == null || b == null) return;
            if (a == b) { Debug.LogWarning("[MarthaDebug] Link needs two DIFFERENT seats."); return; }
            if (debugMatchmakerCard == null) { Debug.LogWarning("[MarthaDebug] assign debugMatchmakerCard first."); return; }

            a.AddStatusCard(debugMatchmakerCard);
            b.AddStatusCard(debugMatchmakerCard);
            a.SetMatchWith(b); // two-way MatchedPlayer bond
            Debug.Log($"[MarthaDebug] matchmaker-linked '{a.PlayerNameText}' (seat {debugSeat}) ↔ " +
                      $"'{b.PlayerNameText}' (seat {debugSeatB}). " +
                      $"HasStatus a={a.HasStatus("Matchmaker")} b={b.HasStatus("Matchmaker")}, " +
                      $"a.match={a.MatchedPlayer?.PlayerNameText}, b.match={b.MatchedPlayer?.PlayerNameText}.");
        }

        // Confirm the draft queue drains cleanly and is never stuck after an edge case (e.g. the
        // orphaned-hand cascade). After the edge fires, IsDraftRunning should be false and Queued 0.
        [ContextMenu("DEBUG — Dump Dispatcher State")]
        private void DebugDumpDispatcher()
        {
            var d = CharacterAbilityDispatcher.Instance;
            if (d == null) { Debug.LogWarning("[MarthaDebug] no CharacterAbilityDispatcher.Instance."); return; }
            Debug.Log($"[MarthaDebug] dispatcher: draftRunning={d.IsDraftRunning}, queued={d.QueuedDraftCount}.");
        }
        // ─── end TEMP ────────────────────────────────────────────────────────────────
    }
}