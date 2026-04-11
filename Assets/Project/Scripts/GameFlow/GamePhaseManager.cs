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
using Salem.Players;
using Salem.UI;
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
        [SerializeField] private TargetPickerUI nightTargetPicker;
        [SerializeField] private float aiDecisionDelay = 0.25f;
        [SerializeField] private bool witchesCanTargetWitches = false;
        [SerializeField] private bool constableCanSelfProtect = false;
        [SerializeField] private string constablePrompt = "Choose a player to protect";
        [SerializeField] private string witchPrompt = "Choose a player to eliminate";
        [SerializeField] private TryalPickerUI tryalPicker;
        public static GamePhaseManager Instance { get; private set; }
        public GamePhase CurrentPhase { get; private set; }
        public delegate void PhaseChangeHandler(GamePhase newPhase);
        public event PhaseChangeHandler OnPhaseChange;
        public KeyCode DebugAdvancePhaseKey = KeyCode.P;

        private GameSetup GameSetup;
        private GameTurnManager GameTurnManager;
        private Coroutine activeNightSequence;
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

            OnPhaseChange += HandlePhaseChange;
        }

        void Start()
        {
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

        public void StartDayPhase()
        {
            /*
            foreach (Player player in players)
            {
                player.TakeTurn();

                // Check for Conspiracy or Night card
                if (player.DrewCard(CardType.Conspiracy))
                {
                    GamePhaseManager.ChangePhase(GamePhase.Conspiracy);
                    break;
                }
                else if (player.DrewCard(CardType.Night))
                {
                    GamePhaseManager.ChangePhase(GamePhase.Night);
                    break;
                }
            }
            */
        }

        public void StartConspiracyPhase()
        {
            /*
            Player blackCatHolder = FindBlackCatHolder();
            blackCatHolder.RevealTryalCard();

            // Pass Tryal cards to the left
            foreach (Player player in players)
            {
                player.PassTryalCardToLeft();
            }

            // Transition back to Day phase
            GamePhaseManager.ChangePhase(GamePhase.Day);
            */
        }

        private Card heldNightCard;

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

        public void StartNightPhase()
        {
            if (!BeginNightSequence())
            {
                // If we could not queue the sequence (e.g., manager disabled), fall back to immediate resolve.
                ResolveNightImmediately();
            }
        }

        public void StartEndGamePhase()
        {
            /*
            if (AreAllWitchesEliminated())
            {
                DisplayVictoryScreen("Villagers Win!");
            }
            else if (AreAllVillagersEliminated())
            {
                DisplayVictoryScreen("Witches Win!");
            }
            */
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
            Debug.Log("Dawn Phase Started: Witches vote on who receives the Black Cat.");

            var witches = PlayerService.GetAliveWitches();
            var allPlayers = PlayerService.GetAlivePlayers();
            var rng = GameManager.Instance?.Rng ?? new XorShiftRng(1UL);

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

            // Collect a vote from each witch
            var votes = new Dictionary<Player, Player>();

            foreach (var witch in witches)
            {
                if (witch.IsLocalPlayer && witch.IsHuman && !PlayerService.IsAirConsoleMode)
                {
                    // Human witch: show UI
                    if (nightTargetPicker != null)
                    {
                        bool done = false;
                        nightTargetPicker.Open(
                            source: witch,
                            isAttack: false,
                            onConfirm: (target, _) =>
                            {
                                votes[witch] = target;
                                done = true;
                            },
                            validTargets: allPlayers,
                            isSingleTarget: true,
                            promptOverride: "Vote: Who receives the Black Cat?"
                        );
                        yield return new WaitUntil(() => done);
                    }
                    else
                    {
                        votes[witch] = allPlayers[rng.NextInt(0, allPlayers.Count)];
                    }
                }
                else
                {
                    // AI witch: pick randomly
                    yield return new WaitForSeconds(aiDecisionDelay);
                    votes[witch] = allPlayers[rng.NextInt(0, allPlayers.Count)];
                }
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

            Debug.Log($"Witch vote result: {winner.PlayerNameText} receives the Black Cat ({votes.Count} votes cast).");

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

            // Close picker UI
            if (nightTargetPicker != null)
                nightTargetPicker.gameObject.SetActive(false);

            // Transition to Day
            yield return ChangePhase(GamePhase.Day, 2.0f);
        }

        private void ResolveBlackCatAssignment(Player target, Card blackCatCard)
        {
            if (target == null || blackCatCard == null) return;

            target.AssignBlackCat(blackCatCard);
            Debug.Log($"The Black Cat has been assigned to {target.PlayerNameText}.");
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
                    if (drawer != null && drawer.IsHuman && drawer.IsLocalPlayer && tryalPicker != null)
                    {
                        bool done = false;
                        tryalPicker.Open(blackCatHolder, idx =>
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

        private IEnumerator EnterNight()
        {
            // pause turns: GameTurnManager already stops on phase change
            NightResolver.Resolve(GameManager.Instance.Rng);
            // (Optional: Constable protect hook here)
            yield return null;
            ChangePhase(GamePhase.Day, PhaseChangeDelay); // turn manager will restart on this
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
            GameManager.Instance.EvaluateEndGame();

            if (heldNightCard != null && DeckManager != null)
            {
                DeckManager.ReshuffleDeckWithDiscard(heldNightCard);
                heldNightCard = null;
            }
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

            yield return ChangePhase(GamePhase.Dawn, PhaseChangeDelay);
            yield return ChangePhase(GamePhase.Day, PhaseChangeDelay);

            activeNightSequence = null;
        }

        [SerializeField] private float confessionAiChance = 0.15f;
        [SerializeField] private string confessionPrompt = "Confess? Reveal a Tryal to protect yourself.";

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

            // Rules order: witches vote, then constable protects, then confession round
            yield return ExecuteLocalWitchChoice(alivePlayers, localPlayer, plan, rng);
            yield return ExecuteConstableChoice(alivePlayers, localPlayer, plan, rng);
            yield return ExecuteConfessionRound(alivePlayers, localPlayer, plan, rng);

            NightResolver.Resolve(rng, plan, witchesCanTargetWitches);
            GameManager.Instance.EvaluateEndGame();
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

                if (player == localPlayer && player.IsHuman && tryalPicker != null)
                {
                    // Human local player: open TryalPickerUI on themselves
                    bool done = false;
                    bool confessed = false;

                    // William Phipps: can fake confess without revealing a Tryal
                    bool canFakeConfess = player.HasTownHall(Salem.Cards.TownhallName.WilliamsPhipps) && player.townHallAbilityCharges > 0;

                    // Show tryal picker — player can choose a Tryal to reveal (confess)
                    // or we need a skip mechanism. Use nightTargetPicker as a Yes/No prompt first.
                    string prompt = canFakeConfess
                        ? confessionPrompt + " (William Phipps: you may fake confess without revealing a Tryal)"
                        : confessionPrompt;

                    if (nightTargetPicker != null)
                    {
                        Player chosen = null;
                        nightTargetPicker.Open(player, false, (primary, _) =>
                        {
                            chosen = primary;
                            done = true;
                        }, new List<Player> { player }, true, prompt);

                        yield return new WaitUntil(() => done || nightTargetPicker == null || !nightTargetPicker.gameObject.activeSelf);

                        if (done && chosen != null)
                        {
                            if (canFakeConfess)
                            {
                                // William Phipps: fake confess — no Tryal reveal, just mark as confessor
                                player.ConsumeTownHallCharge();
                                confessed = true;
                                Debug.Log($"[TownHall] William Phipps ({player.PlayerNameText}) used fake confession ability.");
                            }
                            else
                            {
                                // Normal confession: pick which Tryal to reveal
                                bool tryalChosen = false;
                                tryalPicker.Open(player, idx =>
                                {
                                    player.RevealTryalCard(idx);
                                    confessed = true;
                                    tryalChosen = true;
                                });
                                yield return new WaitUntil(() => tryalChosen);
                            }
                        }
                    }

                    if (confessed)
                    {
                        plan.Confessors.Add(player);
                        Debug.Log($"[GamePhaseManager] {player.PlayerNameText} confessed (revealed a Tryal).");
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

        private IEnumerator ExecuteConstableChoice(List<Player> alivePlayers, Player localPlayer, NightResolver.NightPlan plan, IRng rng)
        {
            var constable = alivePlayers.FirstOrDefault(p => p.IsConstable);
            if (constable == null)
                yield break;

            var candidates = alivePlayers
                .Where(p => constableCanSelfProtect || p != constable)
                .ToList();

            if (candidates.Count == 0)
                yield break;

            if (constable == localPlayer && constable.IsHuman && nightTargetPicker != null)
            {
                bool done = false;
                Player chosen = null;
                var picker = nightTargetPicker;
                picker.Open(constable, false, (primary, _) =>
                {
                    chosen = primary;
                    done = true;
                }, candidates, constableCanSelfProtect, constablePrompt);

                yield return new WaitUntil(() => done || picker == null || !picker.gameObject.activeSelf);

                if (done && chosen != null)
                {
                    plan.ConstableTarget = chosen;
                }
                else if (candidates.Count > 0)
                {
                    Debug.LogWarning("[GamePhaseManager] Constable selection cancelled; defaulting to random.");
                    plan.ConstableTarget = candidates[rng.NextInt(0, candidates.Count)];
                }
            }
            else
            {
                if (constable == localPlayer && nightTargetPicker == null)
                    Debug.LogWarning("[GamePhaseManager] Night target picker not assigned; constable protection defaulting to random.");

                yield return new WaitForSeconds(aiDecisionDelay);
                plan.ConstableTarget = candidates[rng.NextInt(0, candidates.Count)];
            }
        }

        private IEnumerator ExecuteLocalWitchChoice(List<Player> alivePlayers, Player localPlayer, NightResolver.NightPlan plan, IRng rng)
        {
            if (localPlayer == null || !localPlayer.IsWitch || localPlayer.IsEliminated)
                yield break;

            var eligible = alivePlayers
                .Where(p => !p.hasAsylum)
                .ToList();

            if (!witchesCanTargetWitches)
                eligible = eligible.Where(p => !p.IsWitch).ToList();

            eligible = eligible.Distinct().ToList();

            if (eligible.Count == 0)
                yield break;

            if (nightTargetPicker != null)
            {
                bool done = false;
                Player voteTarget = null;
                var picker = nightTargetPicker;
                picker.Open(localPlayer, false, (primary, _) =>
                {
                    voteTarget = primary;
                    done = true;
                }, eligible, false, witchPrompt);

                yield return new WaitUntil(() => done || picker == null || !picker.gameObject.activeSelf);

                if (done && voteTarget != null)
                {
                    plan.SetWitchVote(localPlayer, voteTarget);
                }
                else if (eligible.Count > 0)
                {
                    Debug.LogWarning("[GamePhaseManager] Witch vote selection cancelled; defaulting to random.");
                    plan.SetWitchVote(localPlayer, eligible[rng.NextInt(0, eligible.Count)]);
                }
            }
            else
            {
                Debug.LogWarning("[GamePhaseManager] Night target picker not assigned; witch vote defaulting to random.");
                yield return new WaitForSeconds(aiDecisionDelay);
                plan.SetWitchVote(localPlayer, eligible[rng.NextInt(0, eligible.Count)]);
            }
        }
        #endregion
    }
}
