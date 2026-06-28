/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Manages global game phases (e.g., Setup, Dawn, Night).
*   Responsibilities:
*        • Trigger phase transitions
*        • Notify systems of current phase
*        • Block/enable actions based on phase
*   Access Requirements:
*    • GameStateManager
*    • GameTurnManager

* TODO: []
 * FIXME: [Known bugs or issues]
*/
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Salem.Cards;
using Salem.Data;
using Salem.Deck;
using Salem.Gameplay.Setup;
using Salem.Networking;
using Salem.Players;
using Salem.UI;
using Unity.VisualScripting;
using UnityEngine;

namespace Salem.GameFlow
{
    public enum GamePhase
    {
        Setup,
        Dawn,
        Day,
        Conspiracy,
        Night,
        EndGame
    }

    public class GamePhaseManager : MonoBehaviour
    {
        #region Vars
        [SerializeField] private float PhaseChangeDelay = 0.5f;
        [SerializeField] private DeckManager DeckManager;
        [SerializeField] private float aiDecisionDelay = 0.25f;
        [SerializeField] private bool witchesCanTargetWitches = false;
        [SerializeField] private bool constableCanSelfProtect = false;
        [SerializeField] private string constablePrompt = "Choose a player to protect";
        [SerializeField] private string witchPrompt = "Choose a player to eliminate";
        [SerializeField] private string dawnBlackCatPrompt = "Witches choose who receives the Black Cat";
        [SerializeField] private float confessionAiChance = 0.15f;
        [SerializeField] private string confessionPrompt = "Confess? Reveal a Tryal to protect yourself.";

        // Per-phase secret-phase timeouts (seconds). Backstop only — a phase normally
        // resolves the instant every connected human Confirms (Item 1). On timeout the
        // phase resolves with whatever was recorded; existing safety nets cover missing
        // input (random-fill un-voted witches; no gavel if the constable didn't act).
        // One deadline per phase, identical for everyone → tardiness leaks nothing.
        [Header("Secret-phase timeouts (s)")]
        [SerializeField] private float dawnTimeout = 30f;
        [SerializeField] private float witchVoteTimeout = 45f;
        [SerializeField] private float constableTimeout = 30f;
        [SerializeField] private float confessTimeout = 20f;   // used by the Item 4 confess rework

        // Lead time for synchronized reveals: the host emits phase_resolve with a
        // revealAt this many seconds in the future, then host + mirrors animate the
        // reveal at that shared wall-clock moment (per the /reveal-tryal skill).
        [SerializeField] private float revealLeadSeconds = 3f;
        [SerializeField] private TableLayoutController tableLayoutController;
        // ORPHANED (4c): unused since RunConfessWindow replaced the local ExecuteConfessionRound.
        // Left in place for the deferred serialization-safe orphan-field sweep (see CLAUDE.md).
        [SerializeField] private ConfessionChoiceUI confessionChoiceUI;
        public static GamePhaseManager Instance { get; private set; }
        public GamePhase CurrentPhase { get; private set; }
        /// <summary>True once witches have been revealed to each other at dawn.
        /// The broadcaster includes fellow-witch names in private_state only after this.</summary>
        public bool WitchesRevealed { get; private set; }

        // Live tentative tally for the current witch round (acting player → tentative
        // target). Relayed to fellow witches via private_state. Empty outside a round.
        private readonly Dictionary<Player, Player> currentSecretTally = new();
        public bool IsWitchVoteRoundActive { get; private set; }

        /// <summary>The other witches' tentative picks for `recipient` (excludes self),
        /// for the witch-only live tally. Empty outside a witch round.</summary>
        public WitchVoteMsg[] BuildWitchTallyFor(Player recipient)
        {
            if (!IsWitchVoteRoundActive) return new WitchVoteMsg[0];
            return currentSecretTally
                .Where(kv => kv.Key != null && kv.Key != recipient)
                .Select(kv => new WitchVoteMsg
                {
                    witch = kv.Key.PlayerNameText,
                    target = kv.Value != null ? kv.Value.PlayerNameText : "",
                })
                .ToArray();
        }
        public delegate void PhaseChangeHandler(GamePhase newPhase);
        public event PhaseChangeHandler OnPhaseChange;
        public KeyCode DebugAdvancePhaseKey = KeyCode.P;

        private GameSetup GameSetup;
        private GameTurnManager GameTurnManager;
        private Coroutine activeNightSequence;
        private Card heldNightCard;
        #endregion

        #region Standard Functions
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            GameSetup = GetComponent<GameSetup>();
            GameTurnManager = GetComponent<GameTurnManager>();
            if (!DeckManager) DeckManager = FindFirstObjectByType<DeckManager>();
            if (!tableLayoutController) tableLayoutController = FindFirstObjectByType<TableLayoutController>();

            OnPhaseChange += HandlePhaseChange;
        }

        void Start()
        {
            // Local mode auto-starts. Networked mode waits for the host to press
            // Start (NetworkGameCoordinator calls BeginGame() after the lobby).
            if (PlayerService.Mode == GameMode.Networked)
            {
                Debug.Log("[GamePhaseManager] Networked mode — waiting for host to start the game.");
                return;
            }
            StartGameInternal();
        }

        /// <summary>Begin the game (run Setup → Dawn → Day). Called by the host
        /// in networked mode after players have joined; safe to call once.</summary>
        public void BeginGame()
        {
            StartGameInternal();
        }

        private bool gameStarted;
        private void StartGameInternal()
        {
            if (gameStarted) return;
            gameStarted = true;
            StartCoroutine(ChangePhase(GamePhase.Setup, PhaseChangeDelay));
        }
        #endregion

        #region Accessor Functions
        public IEnumerator ChangePhase(GamePhase newPhase, float delay)
        {
            //Debug.Log($"[GamePhaseManager] Changing phase to {newPhase} in {delay} seconds...");
            yield return new WaitForSeconds(delay);

            CurrentPhase = newPhase;
            OnPhaseChange?.Invoke(newPhase);
            Debug.Log($"Game Phase Changed to: {newPhase}");
        }

        public void HandleNightCardDrawn(Card nightCard)
        {
            heldNightCard = nightCard;
            if (!BeginNightSequence())
            {
                ResolveNightImmediately();
            }
        }

        public void HandleConspiracyCardDrawn(Player drawer)
        {
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning("[GamePhaseManager] Conspiracy triggered while manager disabled.");
                return;
            }
            StartCoroutine(ConspiracyRoutine(drawer));
        }

        public void DebugChangePhase()
        {
            switch (CurrentPhase)
            {
                case GamePhase.Setup:
                    StartCoroutine(ChangePhase(GamePhase.Dawn, PhaseChangeDelay));
                    break;
                case GamePhase.Dawn:
                    StartCoroutine(ChangePhase(GamePhase.Day, PhaseChangeDelay));
                    break;
                case GamePhase.Day:
                    StartCoroutine(ChangePhase(GamePhase.Conspiracy, PhaseChangeDelay));
                    break;
                case GamePhase.Conspiracy:
                    StartCoroutine(ChangePhase(GamePhase.Night, PhaseChangeDelay));
                    break;
                case GamePhase.Night:
                    StartCoroutine(ChangePhase(GamePhase.Day, PhaseChangeDelay));
                    break;
            }
        }
        #endregion

        #region Helper Fucntions
        private void HandlePhaseChange(GamePhase newPhase)
        {
            switch (newPhase)
            {
                case GamePhase.Setup:
                    StartSetupPhase();
                    break;

                case GamePhase.Dawn:
                    StartDawnPhase();
                    break;
            }
        }

        private void StartSetupPhase()
        {
            StartCoroutine(SetupRoutine());
        }

         private IEnumerator SetupRoutine()
        {
            yield return GameSetup.SetupNewGame(PlayerService.All);
            GameTurnManager.Initialize();
            yield return ChangePhase(GamePhase.Dawn, PhaseChangeDelay);
        }

        private void StartDawnPhase()
        {
            StartCoroutine(DawnPhaseRoutine());
        }

        private IEnumerator DawnPhaseRoutine()
        {
            //Debug.Log("Dawn Phase Started: reveal witches, then witches vote on the Black Cat.");

            var witches = PlayerService.GetAliveWitches();
            var allPlayers = PlayerService.GetAlivePlayers();
            var rng = GameManager.Instance?.Rng ?? new XorShiftRng(1UL);

            // Reveal witches to each other: flag on, then push private_state so each
            // witch's phone receives their fellow-witch list (the //TODO at dawn).
            WitchesRevealed = true;
            FindFirstObjectByType<NetworkStateBroadcaster>()?.SendPrivateStates();

            // Retrieve the Black Cat card held during setup
            var blackCatCard = DeckManager != null ? DeckManager.GetHeldBlackCat() : null;

            if (witches.Count == 0 || allPlayers.Count == 0)
            {
                // No witches or no players — assign randomly if we have a card
                if (blackCatCard != null && allPlayers.Count > 0)
                {
                    var fallback = allPlayers[rng.NextInt(0, allPlayers.Count)];
                    ResolveBlackCatAssignment(fallback, blackCatCard);
                }
                yield return ChangePhase(GamePhase.Day, 2.0f);
                yield break;
            }

            // Masked black-cat placement: every player is prompted "Place the black
            // cat"; only witches are acting. Collect all witch votes over the network.
            var votes = new Dictionary<Player, Player>();
            yield return RunNetworkedSecretPhase(
                "black_cat",
                allPlayers,
                p => p.IsWitch,
                (witch, name) =>
                {
                    var target = ResolveByName(name);
                    if (target != null) votes[witch] = target;
                },
                shareTally: true, timeoutSeconds: dawnTimeout);

            if (votes.Count == 0)
            {
                // No usable votes — fall back to a random placement so dawn still resolves.
                votes[witches[0]] = allPlayers[rng.NextInt(0, allPlayers.Count)];
            }

            // Tally votes — majority wins, random tiebreak
            var tally = votes.Values
                .GroupBy(p => p)
                .OrderByDescending(g => g.Count())
                .ToList();

            Player winner;
            int topCount = tally[0].Count();
            var tied = tally.Where(g => g.Count() == topCount).Select(g => g.Key).ToList();
            winner = tied.Count > 1 ? tied[rng.NextInt(0, tied.Count)] : tied[0];

            //Debug.Log($"Witch vote result: {winner.PlayerNameText} receives the Black Cat ({votes.Count} votes cast).");

            // Assign the Black Cat
            if (blackCatCard != null)
            {
                ResolveBlackCatAssignment(winner, blackCatCard);
            }
            else
            {
                Debug.LogWarning("[GamePhaseManager] No Black Cat card available for Dawn assignment.");
                // Still set turn order based on the voted player
                SetTurnOrderFromPlayer(winner);
            }

            // Transition to Day
            yield return ChangePhase(GamePhase.Day, 2.0f);
        }

         private void ResolveBlackCatAssignment(Player target, Card blackCatCard)
        {
            if (target == null || blackCatCard == null) return;

            target.AssignBlackCat(blackCatCard);
            //Debug.Log($"The Black Cat has been assigned to {target.PlayerNameText}.");
            SetTurnOrderFromPlayer(target);
        }

        private void SetTurnOrderFromPlayer(Player target)
        {
            if (GameTurnManager.Instance != null && target != null)
            {
                var alivePlayers = PlayerService.GetAlivePlayers();
                int targetIndex = alivePlayers.IndexOf(target);
                GameTurnManager.Instance.SetStartingPlayerIndex(targetIndex);
            }
        }

         private IEnumerator ConspiracyRoutine(Player drawer)
        {
            Debug.Log("[Conspiracy] Conspiracy card drawn. Resolving...");
            var alivePlayers = PlayerService.GetAlivePlayers();
            var rng = GameManager.Instance?.Rng ?? new XorShiftRng(1UL);

            // Step 1: Drawer chooses a Tryal to reveal on the Black Cat holder
            var blackCatHolder = alivePlayers.Find(p => p.IsBlackCatHolder);
            if (blackCatHolder != null)
            {
                var unrevealed = new List<int>();
                for (int i = 0; i < blackCatHolder.TryalCards.Count; i++)
                    if (!blackCatHolder.TryalCards[i].IsRevealed) unrevealed.Add(i);

                if (unrevealed.Count > 0)
                {
                    if (drawer != null && drawer.IsHuman && drawer.IsLocalPlayer && tableLayoutController != null)
                    {
                        bool done = false;
                        tableLayoutController.BeginTryalSelection(blackCatHolder, idx =>
                        {
                            blackCatHolder.RevealTryalCard(idx);
                            done = true;
                        });
                        yield return new WaitUntil(() => done);
                    }
                    else
                    {
                        yield return new WaitForSeconds(aiDecisionDelay);
                        int pick = unrevealed[rng.NextInt(0, unrevealed.Count)];
                        blackCatHolder.RevealTryalCard(pick);
                    }
                }
            }
            else
            {
                Debug.Log("[Conspiracy] No Black Cat in play — skipping Tryal reveal step.");
            }

            // Step 2: Clockwise Tryal pass — each player takes one unrevealed Tryal from the player to their left
            if (alivePlayers.Count >= 2)
            {
                // Build the pass: each player takes from the player to their left (previous index, wrapping)
                var passedCards = new TryalCard[alivePlayers.Count];
                for (int i = 0; i < alivePlayers.Count; i++)
                {
                    int leftIdx = (i - 1 + alivePlayers.Count) % alivePlayers.Count;
                    var leftPlayer = alivePlayers[leftIdx];
                    int? tryalIdx = leftPlayer.GetRandomUnrevealedTryalIndex(rng);
                    if (tryalIdx.HasValue)
                        passedCards[i] = leftPlayer.RemoveTryalAt(tryalIdx.Value);
                }

                // Distribute received cards
                for (int i = 0; i < alivePlayers.Count; i++)
                {
                    if (passedCards[i] != null)
                    {
                        Debug.Log($"[Conspiracy] {alivePlayers[i].PlayerNameText} received a {passedCards[i].TryalCardType} from their left (private).");
                        alivePlayers[i].AddTryalCardAndNotify(passedCards[i]);
                    }
                }
            }

            // Step 3: Rearrange face-down Tryals (auto-shuffle for now)
            foreach (var player in alivePlayers)
            {
                // Shuffle unrevealed Tryal positions
                var unrevealed = new List<int>();
                for (int i = 0; i < player.TryalCards.Count; i++)
                    if (!player.TryalCards[i].IsRevealed) unrevealed.Add(i);

                // Fisher-Yates on unrevealed positions
                for (int i = unrevealed.Count - 1; i > 0; i--)
                {
                    int j = rng.NextInt(0, i + 1);
                    var temp = player.TryalCards[unrevealed[i]];
                    player.TryalCards[unrevealed[i]] = player.TryalCards[unrevealed[j]];
                    player.TryalCards[unrevealed[j]] = temp;
                }
                player.InvokeOnTryalCardsChanged();
            }

            Debug.Log("[Conspiracy] Conspiracy resolved. Resuming Day phase.");
        }

        private bool BeginNightSequence()
        {
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning("[GamePhaseManager] Night sequence requested while manager disabled.");
                return false;
            }

            if (activeNightSequence != null)
            {
                Debug.LogWarning("[GamePhaseManager] Night sequence already running. Ignoring duplicate request.");
                return true; // already in progress – treat as handled
            }

            activeNightSequence = StartCoroutine(NightSequenceRoutine());
            return true;
        }

        private void ResolveNightImmediately()
        {
            var rng = GameManager.Instance?.Rng;
            if (rng == null)
            {
                Debug.LogWarning("[GamePhaseManager] Unable to resolve night immediately: missing RNG instance.");
                return;
            }

            // Degenerate fallback (manager disabled / sequence already running): resolve
            // with an empty plan and eliminate immediately — no synchronized reveal here.
            var outcome = NightResolver.Resolve(rng, null, witchesCanTargetWitches);
            if (outcome.Victim != null && outcome.Eliminated)
                ApplyNightKill(outcome.Victim);

            if (heldNightCard != null && DeckManager != null)
            {
                DeckManager.ReshuffleDeckWithDiscard(heldNightCard);
                heldNightCard = null;
            }

            GameManager.Instance.EvaluateEndGame();
        }

        private IEnumerator NightSequenceRoutine()
        {
            yield return ChangePhase(GamePhase.Night, PhaseChangeDelay);
            yield return NightPhaseRoutine();

            // Post-night deck reshuffle: merge discard into deck, shuffle, place Night card in bottom half
            if (heldNightCard != null && DeckManager != null)
            {
                DeckManager.ReshuffleDeckWithDiscard(heldNightCard);
                Debug.Log("[GamePhaseManager] Post-night deck reshuffle complete. Night card placed in bottom half.");
                heldNightCard = null;
            }
            
            yield return ChangePhase(GamePhase.Day, PhaseChangeDelay);

            activeNightSequence = null;
        }

        private IEnumerator NightPhaseRoutine()
        {
            var rng = GameManager.Instance?.Rng;
            if (rng == null)
            {
                Debug.LogWarning("[GamePhaseManager] Night phase invoked without a valid RNG instance.");
                yield break;
            }

            var plan = new NightResolver.NightPlan();
            var alivePlayers = PlayerService.GetAlivePlayers();

            // Two masked rounds into ONE plan. Every player sees and taps through both;
            // only the acting role is recorded each round (host-side discard). No
            // intermediate broadcast → witches never learn the constable's pick and the
            // constable never sees the kill tally. A dual-role evil constable is acting
            // in BOTH rounds and gets both actions.

            // Round 1 — witch kill vote. Collects ALL witch votes over the network.
            yield return RunNetworkedSecretPhase(
                "night_vote",
                alivePlayers,
                p => p.IsWitch,
                (witch, name) =>
                {
                    var target = ResolveByName(name);
                    if (target != null) plan.SetWitchVote(witch, target);
                },
                shareTally: true, timeoutSeconds: witchVoteTimeout);

            // Round 2 — constable save.
            yield return RunNetworkedSecretPhase(
                "constable_save",
                alivePlayers,
                p => p.IsConstable,
                (constable, name) =>
                {
                    var target = ResolveByName(name);
                    // The constable may not give the gavel to themselves (rulebook p7).
                    // The full target list is kept identical for all (masking); a
                    // self-pick is simply void here as the authoritative backstop.
                    // NOTE: the 2-3 player ghost variant DOES allow self-protect
                    // (rulebook p17) — re-enable for that mode in Phase 6.
                    if (target != null && target != constable) plan.ConstableTarget = target;
                },
                shareTally: false, timeoutSeconds: constableTimeout);

            // Masked, timed confess window. Every phone shows the same confess prompt; a
            // confession reveals one of the player's OWN tryals for immunity, but the reveal
            // is DEFERRED to revealAt (timing masked during the window). Records immunity into
            // plan.Confessors and the chosen tryal index into pendingConfessions.
            var pendingConfessions = new Dictionary<Player, int>();
            yield return RunConfessWindow(alivePlayers, plan, pendingConfessions, rng);

            // Resolve who the night targets, then reveal the outcome — confessed tryals and
            // the elimination flip together, in sync across host + mirrors (phase_resolve).
            // Win conditions are checked BEFORE the animation inside EmitSynchronizedReveal.
            var outcome = NightResolver.Resolve(rng, plan, witchesCanTargetWitches);
            yield return RevealNightOutcome(outcome, pendingConfessions);
        }

        /// <summary>
        /// Turn a NightResolver outcome into a synchronized reveal. Confessed tryals (from the
        /// confess window, deferred) flip PUBLICLY alongside the night outcome at revealAt —
        /// confession is a public act; only its TIMING was masked while the window was open.
        /// An elimination reveals all of the victim's tryals (routed through TrialService so
        /// the multiple-witch-card rule applies) and eliminates; a save emits a "no elimination"
        /// result. With no victim and no confessions, nothing reveals.
        /// </summary>
        private IEnumerator RevealNightOutcome(NightResolver.NightOutcome outcome,
                                               Dictionary<Player, int> pendingConfessions)
        {
            bool hasConfessions = pendingConfessions != null && pendingConfessions.Count > 0;

            if (outcome.Victim == null && !hasConfessions)
            {
                GameManager.Instance?.EvaluateEndGame();
                yield break;
            }

            // Flip every confessed tryal face-up (public). Routed through RevealTryalCard so
            // the normal reveal side effects apply (a witch who confessed their last witch
            // card is handled by TrialService, like any other reveal).
            void RevealConfessions()
            {
                if (pendingConfessions == null) return;
                foreach (var kv in pendingConfessions)
                {
                    var cp = kv.Key; int idx = kv.Value;
                    if (cp != null && idx >= 0 && idx < cp.TryalCards.Count &&
                        cp.TryalCards[idx] != null && !cp.TryalCards[idx].IsRevealed)
                        cp.RevealTryalCard(idx);
                }
            }

            if (outcome.Victim != null && outcome.Eliminated)
            {
                var victim = outcome.Victim;
                yield return EmitSynchronizedReveal(
                    applyReveal: () => { RevealConfessions(); ApplyNightKill(victim); },
                    elimination: new EliminationResultMsg { playerId = PublicIdOf(victim), eliminated = true, savedBy = "" });
            }
            else if (outcome.Victim != null)
            {
                // Targeted but saved (constable / confession). Still flip confessed tryals
                // and announce the outcome in sync.
                yield return EmitSynchronizedReveal(
                    applyReveal: RevealConfessions,
                    elimination: new EliminationResultMsg
                    {
                        playerId = PublicIdOf(outcome.Victim),
                        eliminated = false,
                        savedBy = outcome.SavedByLabel ?? "",
                    });
            }
            else
            {
                // No victim, but confessions happened — reveal them publicly in sync.
                yield return EmitSynchronizedReveal(applyReveal: RevealConfessions, elimination: null);
            }
        }

        /// <summary>
        /// Synchronized reveal per the /reveal-tryal skill. The reveal is DEFERRED in
        /// whole to a shared future wall-clock moment so the host screen, mirrors, and
        /// phones all reveal together (the host renders from its own model, so the model
        /// mutation itself is held until revealAt — otherwise the host would reveal early).
        ///  1) emit phase_resolve { revealAt = now + revealLeadSeconds } to host + mirrors.
        ///  2) wait until revealAt (REALTIME — a win sets Time.timeScale = 0).
        ///  3) AT revealAt: applyReveal mutates the model (this fires the host's own UI
        ///     and the auto game_state_update broadcast), EvaluateEndGame runs BEFORE the
        ///     result is sent, then elimination_result and — only after the reveal —
        ///     game_over. Reveal-then-game_over ordering is preserved.
        /// The night routine awaits this coroutine, so nothing acts on the game between
        /// (1) and (3). Reusable for confession reveals (Item 4) and accusation/conspiracy.
        /// </summary>
        private IEnumerator EmitSynchronizedReveal(System.Action applyReveal, EliminationResultMsg elimination)
        {
            var nm = NetworkManager.Instance;
            bool networked = nm != null && nm.IsConnected;

            // Offline/local: no screens to synchronize — apply immediately and exit.
            if (!networked)
            {
                applyReveal?.Invoke();
                GameManager.Instance?.EvaluateEndGame();
                yield break;
            }

            // (1) Emit the shared reveal timestamp FIRST. Nothing changes yet — the model
            //     mutation, the host's own UI update, and the client broadcast all happen
            //     together at revealAt.
            long nowMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long revealAt = nowMs + (long)(revealLeadSeconds * 1000f);
            nm.SendPhaseResolve(new PhaseResolveMsg { revealAt = revealAt });

            // (2) Wait for the wall-clock moment using REALTIME — pauseOnGameEnd sets
            //     Time.timeScale = 0 on a win, which would freeze scaled waits forever.
            float delay = (revealAt - System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000f;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            // (3) AT revealAt: mutate the model (fires the host's UI), check win BEFORE
            //     sending the result, push the now-revealed state to all clients, then send
            //     elimination_result and — only after the reveal — game_over.
            applyReveal?.Invoke();
            GameManager.Instance?.EvaluateEndGame();
            bool gameOver = GameManager.Instance != null && GameManager.Instance.IsGameOver;

            // Propagate model changes that don't auto-broadcast (a save / confession-only
            // night reveals tryals but fires no elimination event).
            FindFirstObjectByType<NetworkStateBroadcaster>()?.BroadcastNow();

            if (elimination != null) nm.SendEliminationResult(elimination);
            if (gameOver) nm.SendGameOver(new GameOverMsg { winner = WinnerLabel() });
        }

        // Reveal the night victim's full identity and run elimination bookkeeping.
        // Each tryal is revealed through RevealTryalCard → TrialService so the
        // multiple-witch-card rule applies (a non-last witch card announces "second
        // witch" and does not eliminate; the last witch / all-revealed triggers
        // Eliminate). A night kill reveals EVERY card, so the victim dies regardless
        // of how many witch cards they hold. The explicit Eliminate is an idempotent
        // safety net (cause + matchmaker cascade) for the no-tryals edge.
        // Shared by the synchronized night reveal and the immediate fallback.
        private static void ApplyNightKill(Player victim)
        {
            if (victim == null) return;
            for (int i = 0; i < victim.TryalCards.Count; i++)
                if (victim.TryalCards[i] != null && !victim.TryalCards[i].IsRevealed)
                    victim.RevealTryalCard(i);
            if (!victim.IsEliminated)
                PlayerService.Eliminate(victim, EliminationCause.NightKill);
        }

        // Public display id: NetworkId for human seats, synthetic PublicId for AI.
        private static string PublicIdOf(Player p)
            => p == null ? "" : (!string.IsNullOrEmpty(p.NetworkId) ? p.NetworkId : (p.PublicId ?? ""));

        // game_over winner label per protocol ("witches" | "townspeople").
        private static string WinnerLabel()
        {
            var r = GameManager.Instance?.LastEndResult;
            if (r == null) return "";
            return r.WinningTeam == Salem.Data.Team.Witches ? "witches" : "townspeople";
        }

        /// <summary>
        /// THE masking-timing + timeout predicate, in exactly ONE place (shared by the
        /// witch/constable/black-cat rounds and the confess window). Resolves when EVERY
        /// connected human has Confirmed — never when only the acting players finish, so
        /// resolution timing reveals nothing about who acted. IsConnected is read live so
        /// a mid-phase disconnect drops that seat from the wait set immediately (no stall).
        /// AI have no phone and cannot leak timing, so they are never waited on. The single
        /// per-phase deadline fires on a wall-clock instant identical for everyone, so a
        /// timeout never leaks who was being waited on. Reports timedOut via onResolved.
        /// </summary>
        private IEnumerator AwaitAllConfirmedOrTimeout(string promptType, List<Player> alivePlayers,
                                                       HashSet<Player> allConfirmed, float timeoutSeconds,
                                                       System.Action<bool> onResolved)
        {
            bool StillWaiting() => alivePlayers.Any(p =>
                p != null && p.IsHuman && p.IsConnected && !allConfirmed.Contains(p));

            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            bool timedOut = false;

            float nextLog = Time.realtimeSinceStartup + 5f;
            while (StillWaiting())
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    timedOut = true;
                    break;
                }
                if (Time.realtimeSinceStartup >= nextLog)
                {
                    var pending = alivePlayers
                        .Where(p => p != null && p.IsHuman && p.IsConnected && !allConfirmed.Contains(p))
                        .Select(p => p.PlayerNameText);
                    Debug.Log($"[SecretPhase] {promptType}: still waiting on confirm from [{string.Join(", ", pending)}] " +
                              $"({deadline - Time.realtimeSinceStartup:0.0}s to timeout)");
                    nextLog += 5f;
                }
                yield return null;
            }

            onResolved?.Invoke(timedOut);
        }

        /// <summary>
        /// Run one masked secret-phase round (the host = the skill's "server").
        /// Prompts EVERY non-AI player identically (acting flag per role); records a
        /// submission into the round ONLY if the submitter is acting — non-acting
        /// submissions are received and silently discarded. Acting AI choose directly.
        /// Resolves once EVERY connected human has Confirmed (not just acting players) —
        /// resolution timing must not reveal who acted; a mid-phase disconnect drops that
        /// seat from the wait set (Player.IsConnected). Per-player prompts flow through
        /// IPlayerInput.RequestSecretPhase (NetworkInput for phones).
        /// </summary>
        private IEnumerator RunNetworkedSecretPhase(string promptType, List<Player> alivePlayers,
                                                    System.Func<Player, bool> isActing,
                                                    System.Action<Player, string> recordActing,
                                                    bool shareTally, float timeoutSeconds)
        {
            var rng = GameManager.Instance?.Rng ?? new XorShiftRng(1UL);
            var targetNames = alivePlayers.Where(p => p != null).Select(p => p.PlayerNameText).ToArray();
            var actingPlayers = alivePlayers.Where(p => p != null && isActing(p)).ToList();
            var confirmed = new HashSet<Player>();      // acting players whose vote was RECORDED
            var allConfirmed = new HashSet<Player>();    // ANY player who tapped Confirm (what we WAIT on)
            var broadcaster = FindFirstObjectByType<NetworkStateBroadcaster>();

            // Seed the live tally (witch rounds) so fellows see "Mary → —" up front.
            if (shareTally)
            {
                currentSecretTally.Clear();
                foreach (var a in actingPlayers) currentSecretTally[a] = null;
                IsWitchVoteRoundActive = true;
                broadcaster?.SendPrivateStates();
            }

            void OnSubmit(Player p, string name, bool isConfirm)
            {
                if (p == null) return;
                if (allConfirmed.Contains(p)) return;      // already finalized this player

                bool acting = isActing(p);

                // Witch live tally is acting-only (private witch-coordination channel).
                // Non-acting submits update nothing — silently discarded, as before.
                if (acting && shareTally)
                {
                    currentSecretTally[p] = ResolveByName(name); // tentative or confirm → update
                    broadcaster?.SendPrivateStates();             // relay live to fellow witches
                }

                if (isConfirm)
                {
                    // MASKING-TIMING FIX: every player's Confirm counts toward the wait,
                    // regardless of role — resolution timing must not reveal who acted.
                    allConfirmed.Add(p);
                    if (acting)
                    {
                        confirmed.Add(p);            // only acting selections are RECORDED
                        recordActing?.Invoke(p, name);
                    }
                }
            }

            foreach (var p in alivePlayers)
            {
                if (p == null) continue;
                bool acting = isActing(p);

                if (p is AIPlayer)
                {
                    if (acting)
                    {
                        // AI picks a random other player and confirms immediately;
                        // NightResolver validates eligibility.
                        var candidates = alivePlayers.Where(x => x != null && x != p).ToList();
                        if (candidates.Count > 0)
                            OnSubmit(p, candidates[rng.NextInt(0, candidates.Count)].PlayerNameText, true);
                    }
                    // non-acting AI has no phone — nothing to mask
                }
                else if (p.Input != null)
                {
                    // Human (network or local) — prompted regardless of acting (masking).
                    var who = p;
                    StartCoroutine(who.Input.RequestSecretPhase(who, promptType, targetNames, acting,
                        (submitter, name, isConfirm) => OnSubmit(submitter, name, isConfirm)));
                }
            }

            // Wait for ALL connected humans to Confirm, or the timeout — the single
            // masking-timing + timeout predicate, shared with the confess window.
            bool timedOut = false;
            yield return AwaitAllConfirmedOrTimeout(promptType, alivePlayers, allConfirmed,
                                                    timeoutSeconds, t => timedOut = t);

            if (shareTally)
            {
                IsWitchVoteRoundActive = false;
                currentSecretTally.Clear();
                broadcaster?.SendPrivateStates(); // clear witchVotes on phones
            }

            int humanCount = alivePlayers.Count(p => p != null && p.IsHuman);
            int humansConfirmed = allConfirmed.Count(p => p != null && p.IsHuman);
            string how = timedOut ? "TIMED OUT — resolving with recorded input" : "all connected humans confirmed";
            Debug.Log($"[SecretPhase] {promptType}: {how} " +
                      $"({humansConfirmed}/{humanCount} humans; " +
                      $"{confirmed.Count}/{actingPlayers.Count} acting recorded).");
        }

        /// <summary>Resolve a submitted display name back to an alive Player (humans and AI).</summary>
        private Player ResolveByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return PlayerService.GetAlivePlayers().FirstOrDefault(p => p != null && p.PlayerNameText == name);
        }

        // Confess-window selection sentinels (sent by AI / phone). A tryal index ("0".."n")
        // means "reveal that tryal"; ConfessSkip means "don't confess"; ConfessFake is the
        // William Phipps fake confession (Town Hall, AI-only in 4c — immune, reveals nothing).
        private const string ConfessSkip = "skip";
        private const string ConfessFake = "fake";

        /// <summary>
        /// Masked, timed confess window (replaces the legacy sequential ExecuteConfessionRound).
        /// Every player is prompted identically; anyone may confess by revealing one of their
        /// OWN tryals for immunity. The reveal is DEFERRED to the synchronized revealAt (see
        /// RevealNightOutcome) so timing never exposes who confessed while the window is open —
        /// immunity is applied now via plan.Confessors. Confessing players' chosen tryal index
        /// is collected in pendingConfessions for the public reveal. Shares the wait-for-all +
        /// timeout predicate with the other secret phases (AwaitAllConfirmedOrTimeout).
        /// </summary>
        private IEnumerator RunConfessWindow(List<Player> alivePlayers, NightResolver.NightPlan plan,
                                             Dictionary<Player, int> pendingConfessions, IRng rng)
        {
            var allConfirmed = new HashSet<Player>();

            void OnSubmit(Player p, string sel, bool isConfirm)
            {
                if (p == null) return;
                if (allConfirmed.Contains(p)) return;       // already finalized this player
                if (isConfirm)
                {
                    allConfirmed.Add(p);                    // every Confirm counts toward the wait
                    RecordConfession(p, sel, plan, pendingConfessions);
                }
                // tentative submits carry no confess state — ignored (masking flow only)
            }

            foreach (var p in alivePlayers)
            {
                if (p == null) continue;

                if (p is AIPlayer)
                {
                    // AI confess heuristic (preserves William Phipps fake-confess + confessionAiChance).
                    OnSubmit(p, AiConfessSelection(p, rng), true);
                }
                else if (p.Input != null)
                {
                    // Human (network or local). The phone renders its OWN face-down tryals
                    // (from private_state) + a "don't confess" option; targets is unused for
                    // confess. Selection carries the tryal index or ConfessSkip. acting=true
                    // for everyone — confession is genuinely identical (anyone may confess).
                    var who = p;
                    StartCoroutine(who.Input.RequestSecretPhase(who, "confess", System.Array.Empty<string>(), true,
                        (submitter, sel, isConfirm) => OnSubmit(submitter, sel, isConfirm)));
                }
            }

            bool timedOut = false;
            yield return AwaitAllConfirmedOrTimeout("confess", alivePlayers, allConfirmed,
                                                    confessTimeout, t => timedOut = t);

            Debug.Log($"[Confess] window closed ({(timedOut ? "TIMED OUT" : "all connected humans confirmed")}) — " +
                      $"{plan.Confessors.Count} confessor(s), {pendingConfessions.Count} tryal(s) to reveal at revealAt.");
        }

        // Record one confession submission. Selection = own-tryal-index | ConfessSkip | ConfessFake.
        // The actual tryal reveal is DEFERRED (recorded into pending) — only immunity is applied now.
        private void RecordConfession(Player p, string sel, NightResolver.NightPlan plan,
                                      Dictionary<Player, int> pending)
        {
            if (p == null || string.IsNullOrEmpty(sel) || sel == ConfessSkip) return;  // no confession

            if (sel == ConfessFake)
            {
                // William Phipps fake confession (Town Hall): immune WITHOUT revealing a tryal.
                if (p.townHallAbilityCharges > 0) p.ConsumeTownHallCharge();
                plan.Confessors.Add(p);
                return;
            }

            if (!int.TryParse(sel, out int idx)) return;                 // unrecognized selection
            if (idx < 0 || idx >= p.TryalCards.Count) return;
            var card = p.TryalCards[idx];
            if (card == null || card.IsRevealed) return;                 // can't confess a revealed card

            pending[p] = idx;            // reveal DEFERRED to revealAt (timing masked during window)
            plan.Confessors.Add(p);      // immunity now — NightResolver early-returns for confessors
        }

        // AI confess decision → a confess-window selection string (index | ConfessSkip | ConfessFake).
        private string AiConfessSelection(Player p, IRng rng)
        {
            // William Phipps AI: fake-confess to protect Witch tryals.
            bool canFakeConfess = p.HasTownHall(Salem.Cards.TownhallName.WilliamsPhipps) && p.townHallAbilityCharges > 0;
            if (canFakeConfess && p.IsWitch && rng.NextInt(0, 100) < 50)
                return ConfessFake;

            // Otherwise: small chance to confess a non-Witch tryal (reveal one for immunity).
            int safeIdx = -1;
            for (int i = 0; i < p.TryalCards.Count; i++)
            {
                var c = p.TryalCards[i];
                if (c != null && !c.IsRevealed && c.TryalCardType != TryalCardType.Witch) { safeIdx = i; break; }
            }
            if (safeIdx >= 0 && rng.NextInt(0, 100) < (int)(confessionAiChance * 100))
                return safeIdx.ToString();

            return ConfessSkip;
        }
        #endregion
    }
}