/*
* ⚠ TEMP — DEBUG SCAFFOLDING (Phase 5). Not shipped. This harness (ContextMenu launchers, forced
*   setups, dispatcher/seat dump helpers) exists ONLY to exercise game logic until a real Unity
*   play-mode test harness exists. REMOVE this file — together with GameSetup.DEBUG_forcedTownHall —
*   once that harness lands. Kept deliberately for now; see CLAUDE.md "TEMP scaffolding".
*
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
using System.Collections.Generic;
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

        // ─── TEMP (Phase 10 — deterministic seat setup) ──────────────────────────────
        //
        // ⚠ WHY THIS EXISTS. Seven Phase-10 checklist items describe a SPECIFIC end state — the last
        // townsperson turning witch, a player holding two witch cards, an evil constable, a piety
        // holder at the threshold. GameSetup deals tryals at RANDOM, so before these hooks the only
        // way to "test" those items was to play games until the situation happened to arise, which
        // is waiting, not testing. See docs/phase-10-test-plan.md.
        //
        // These write the SAME state the real deal produces (instantiated TryalCard SOs, then
        // DetermineRole) and drive reveals through the normal path, so nothing here is a shortcut
        // around the rules — only around the randomness.

        [Header("TEMP — Phase 10 seat setup")]
        [SerializeField, Tooltip("Tryal SO templates in the SAME order GameSetup uses: 0 Constable, 1 Witch, 2 Not a Witch.")]
        private TryalCard[] debugTryalTemplates = new TryalCard[3];
        [SerializeField, Tooltip("Tryals to deal to debugSeat. W = Witch, C = Constable, N = Not a Witch. e.g. \"W,W,N\"")]
        private string debugTryalSpec = "W,N,N";
        [SerializeField, Tooltip("Index into the seat's tryal row for 'Reveal Tryal On Seat'.")]
        private int debugTryalIndex = 0;
        [SerializeField, Tooltip("Drag 'Accusation 1' (Red Cards). Used by 'Add Accusations To Seat'.")]
        private ActionCardSO debugAccusationCard;
        [SerializeField, Tooltip("How many Accusation cards 'Add Accusations To Seat' places.")]
        private int debugAccusationCount = 7;
        [SerializeField, Tooltip("Any blue/persistent card (Piety, Asylum, Stocks…). Used by 'Add Status Card To Seat'.")]
        private ActionCardSO debugStatusCard;

        /// <summary>
        /// Deal an EXACT tryal row to a seat: "W,N,N" | "W,W,N" | "W,C" …
        ///
        /// Replaces the row wholesale, then re-runs DetermineRole — the same two steps GameSetup
        /// performs after dealing, so the seat is indistinguishable from a dealt one.
        ///
        /// ⚠ IsWitch is STICKY by rulebook ("a player who loses their only witch card remains a
        /// witch"), so clearing a witch row does NOT clear the role. That is correct game behavior,
        /// not a harness bug — restart the game to get a clean seat.
        /// </summary>
        [ContextMenu("DEBUG — Set Tryals On Seat")]
        private void DebugSetTryals()
        {
            var p = DebugSeat(debugSeat);
            if (p == null) return;

            if (debugTryalTemplates == null || debugTryalTemplates.Length < 3 ||
                debugTryalTemplates[0] == null || debugTryalTemplates[1] == null || debugTryalTemplates[2] == null)
            {
                Debug.LogWarning("[Phase10Debug] assign all three debugTryalTemplates (Constable, Witch, Not a Witch).");
                return;
            }

            var row = new List<TryalCard>();
            foreach (var raw in (debugTryalSpec ?? "").Split(','))
            {
                var token = raw.Trim().ToUpperInvariant();
                if (token.Length == 0) continue;

                TryalCardType type;
                int templateIndex;
                switch (token[0])
                {
                    case 'C': type = TryalCardType.Constable; templateIndex = 0; break;
                    case 'W': type = TryalCardType.Witch;     templateIndex = 1; break;
                    case 'N': type = TryalCardType.NotAWitch; templateIndex = 2; break;
                    default:
                        Debug.LogWarning($"[Phase10Debug] '{token}' is not W, C or N — skipped.");
                        continue;
                }

                // Instantiate: the SOs are shared project assets, and writing TryalCardType or
                // IsRevealed on the asset itself would persist into every later game.
                var card = (TryalCard)Instantiate(debugTryalTemplates[templateIndex]);
                card.TryalCardType = type;
                card.IsRevealed = false;
                row.Add(card);
            }

            if (row.Count == 0) { Debug.LogWarning("[Phase10Debug] empty tryal spec — nothing dealt."); return; }

            p.TryalCards = row;
            p.InvokeOnTryalCardsChanged();
            p.DetermineRole();

            Debug.Log($"[Phase10Debug] seat {debugSeat} '{p.PlayerNameText}' dealt [{debugTryalSpec}] — " +
                      $"IsWitch={p.IsWitch} IsConstable={p.IsConstable}.");
        }

        /// <summary>
        /// Flip one tryal through the REAL reveal path (Player.RevealTryalCard), so TrialService, the
        /// double-witch announcement, Rebecca Nurse's draw and the win check all fire exactly as in
        /// play. `fromAccusation` is false — this stands in for a night/conspiracy flip, not an
        /// accusation one.
        /// </summary>
        [ContextMenu("DEBUG — Reveal Tryal On Seat")]
        private void DebugRevealTryal()
        {
            var p = DebugSeat(debugSeat);
            if (p == null) return;
            if (p.TryalCards == null || debugTryalIndex < 0 || debugTryalIndex >= p.TryalCards.Count)
            {
                Debug.LogWarning($"[Phase10Debug] tryal index {debugTryalIndex} out of range for seat {debugSeat}.");
                return;
            }

            Debug.Log($"[Phase10Debug] revealing tryal {debugTryalIndex} " +
                      $"({p.TryalCards[debugTryalIndex].TryalCardType}) on '{p.PlayerNameText}'.");
            p.RevealTryalCard(debugTryalIndex);
        }

        /// <summary>
        /// Place N Accusation cards in front of a seat — real status cards, so the count comes from
        /// RecalculateAccusations and honours Piety doubling, George Burroughs' base 8 and Cotton
        /// Mather's Evidence discount, exactly as a played card would.
        /// </summary>
        [ContextMenu("DEBUG — Add Accusations To Seat")]
        private void DebugAddAccusations()
        {
            var p = DebugSeat(debugSeat);
            if (p == null) return;
            if (debugAccusationCard == null) { Debug.LogWarning("[Phase10Debug] assign debugAccusationCard first."); return; }

            for (int i = 0; i < debugAccusationCount; i++) p.AddStatusCard(debugAccusationCard);
            p.ApplyAccusation(0);   // recompute from status cards

            Debug.Log($"[Phase10Debug] added {debugAccusationCount} accusations to '{p.PlayerNameText}'.");
            DebugDump(p);
        }

        /// <summary>Attach any blue/persistent card (Piety, Asylum, Stocks…) to a seat.</summary>
        [ContextMenu("DEBUG — Add Status Card To Seat")]
        private void DebugAddStatusCard()
        {
            var p = DebugSeat(debugSeat);
            if (p == null) return;
            if (debugStatusCard == null) { Debug.LogWarning("[Phase10Debug] assign debugStatusCard first."); return; }

            p.AddStatusCard(debugStatusCard);
            p.ApplyAccusation(0);   // Piety changes the LIMIT, so recompute
            Debug.Log($"[Phase10Debug] added '{debugStatusCard.name}' to '{p.PlayerNameText}'.");
            DebugDump(p);
        }

        /// <summary>
        /// Print the win-condition inputs for the whole table.
        ///
        /// The two checks are asymmetric and easy to confuse, so both are shown: witches win ONLY
        /// when nonWitches == 0 (there is NO parity rule — see CLAUDE.md), villagers win when no
        /// unrevealed Witch tryal remains anywhere.
        /// </summary>
        [ContextMenu("DEBUG — Dump Win State")]
        private void DebugDumpWinState()
        {
            int alive = 0, aliveWitches = 0, aliveNonWitches = 0, unrevealedWitchCards = 0;

            foreach (var p in PlayerService.All)
            {
                if (p == null) continue;

                foreach (var t in p.TryalCards)
                    if (!t.IsRevealed && t.TryalCardType == TryalCardType.Witch) unrevealedWitchCards++;

                if (p.IsEliminated) continue;
                alive++;
                if (p.IsWitch) aliveWitches++; else aliveNonWitches++;
            }

            Debug.Log($"[Phase10Debug] alive={alive} witches={aliveWitches} nonWitches={aliveNonWitches} " +
                      $"unrevealedWitchTryals={unrevealedWitchCards} → " +
                      $"witchesWin={(aliveWitches > 0 && aliveNonWitches == 0)} " +
                      $"villagersWin={(unrevealedWitchCards == 0)}");
        }

        // ─── end TEMP ────────────────────────────────────────────────────────────────
    }
}