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
        [SerializeField] private TableLayoutController tableLayoutController;
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
                shareTally: true);

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

            NightResolver.Resolve(rng, null, witchesCanTargetWitches);

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
            var localPlayer = PlayerService.GetLocalPlayer();

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
                shareTally: true);

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
                shareTally: false);

            // Confession round — unchanged for 4b (masked/timed rework is 4c).
            yield return ExecuteConfessionRound(alivePlayers, localPlayer, plan, rng);

            NightResolver.Resolve(rng, plan, witchesCanTargetWitches);
            GameManager.Instance.EvaluateEndGame();
        }

        /// <summary>
        /// Run one masked secret-phase round (the host = the skill's "server").
        /// Prompts EVERY non-AI player identically (acting flag per role); records a
        /// submission into the round ONLY if the submitter is acting — non-acting
        /// submissions are received and silently discarded. Acting AI choose directly.
        /// Resolves once every acting player has submitted. Per-player prompts flow
        /// through IPlayerInput.RequestSecretPhase (NetworkInput for phones).
        /// </summary>
        private IEnumerator RunNetworkedSecretPhase(string promptType, List<Player> alivePlayers,
                                                    System.Func<Player, bool> isActing,
                                                    System.Action<Player, string> recordActing,
                                                    bool shareTally)
        {
            var rng = GameManager.Instance?.Rng ?? new XorShiftRng(1UL);
            var targetNames = alivePlayers.Where(p => p != null).Select(p => p.PlayerNameText).ToArray();
            var actingPlayers = alivePlayers.Where(p => p != null && isActing(p)).ToList();
            var confirmed = new HashSet<Player>();
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
                if (p == null || !isActing(p)) return;   // silent discard for non-acting
                if (confirmed.Contains(p)) return;         // already finalized this player

                if (shareTally)
                {
                    currentSecretTally[p] = ResolveByName(name); // tentative or confirm → update
                    broadcaster?.SendPrivateStates();             // relay live to fellow witches
                }

                if (isConfirm)
                {
                    confirmed.Add(p);
                    recordActing?.Invoke(p, name);
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

            // Resolve when all acting players have CONFIRMED (tentatives don't count).
            // Periodic "still waiting on […]" diagnostic (4b has no timeout — that's 4c).
            float nextLog = Time.realtimeSinceStartup + 5f;
            while (confirmed.Count < actingPlayers.Count)
            {
                if (Time.realtimeSinceStartup >= nextLog)
                {
                    var pending = actingPlayers.Where(a => !confirmed.Contains(a)).Select(a => a.PlayerNameText);
                    Debug.Log($"[SecretPhase] {promptType}: still waiting on confirm from [{string.Join(", ", pending)}]");
                    nextLog += 5f;
                }
                yield return null;
            }

            if (shareTally)
            {
                IsWitchVoteRoundActive = false;
                currentSecretTally.Clear();
                broadcaster?.SendPrivateStates(); // clear witchVotes on phones
            }

            Debug.Log($"[SecretPhase] {promptType}: all {actingPlayers.Count} acting player(s) confirmed.");
        }

        /// <summary>Resolve a submitted display name back to an alive Player (humans and AI).</summary>
        private Player ResolveByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return PlayerService.GetAlivePlayers().FirstOrDefault(p => p != null && p.PlayerNameText == name);
        }

        private IEnumerator ExecuteConfessionRound(List<Player> alivePlayers, Player localPlayer, NightResolver.NightPlan plan, IRng rng)
        {
            Debug.Log("[GamePhaseManager] Confession round begins.");

            foreach (var player in alivePlayers)
            {
                if (player.IsEliminated) continue;

                // Check if this player has any unrevealed Tryals to confess
                bool hasUnrevealed = player.TryalCards.Any(c => !c.IsRevealed);
                if (!hasUnrevealed) continue;

                if (player == localPlayer && player.IsHuman && tableLayoutController != null)
                {
                    bool choiceMade = false;
                    ConfessionChoiceUI.ConfessionChoice choice = ConfessionChoiceUI.ConfessionChoice.Skip;

                    confessionChoiceUI.Open(player, selectedChoice =>
                    {
                        choice = selectedChoice;
                        choiceMade = true;
                    });

                    yield return new WaitUntil(() => choiceMade);

                    if (choice == ConfessionChoiceUI.ConfessionChoice.FakeConfess)
                    {
                        player.ConsumeTownHallCharge();
                        plan.Confessors.Add(player);

                        Debug.Log($"[TownHall] William Phipps ({player.PlayerNameText}) used fake confession ability.");
                    }
                    else if (choice == ConfessionChoiceUI.ConfessionChoice.Confess)
                    {
                        bool tryalChosen = false;

                        tableLayoutController.BeginTryalSelection(player, idx =>
                        {
                            player.RevealTryalCard(idx);
                            plan.Confessors.Add(player);
                            tryalChosen = true;
                        });

                        yield return new WaitUntil(() => tryalChosen);

                        Debug.Log($"[GamePhaseManager] {player.PlayerNameText} confessed.");
                    }
                    else
                    {
                        Debug.Log($"[GamePhaseManager] {player.PlayerNameText} skipped confession.");
                    }
                }
                else
                {
                    // AI or non-local: small chance to confess if they have a safe Tryal to reveal
                    yield return new WaitForSeconds(aiDecisionDelay);

                    // William Phipps AI: use fake confession to protect Witch tryals
                    bool canFakeConfess = player.HasTownHall(Salem.Cards.TownhallName.WilliamsPhipps) && player.townHallAbilityCharges > 0;
                    if (canFakeConfess && player.IsWitch && rng.NextInt(0, 100) < 50)
                    {
                        player.ConsumeTownHallCharge();
                        plan.Confessors.Add(player);
                        Debug.Log($"[TownHall] William Phipps ({player.PlayerNameText}) (AI) used fake confession.");
                        continue;
                    }

                    bool hasNonWitchToReveal = player.TryalCards.Any(c =>
                        !c.IsRevealed && c.TryalCardType != TryalCardType.Witch);

                    if (hasNonWitchToReveal && rng.NextInt(0, 100) < (int)(confessionAiChance * 100))
                    {
                        if (player.TryConfessToSurvive())
                        {
                            plan.Confessors.Add(player);
                            Debug.Log($"[GamePhaseManager] {player.PlayerNameText} (AI) confessed.");
                        }
                    }
                }
            }

            Debug.Log("[GamePhaseManager] Confession round ends.");
        }
        #endregion
    }
}