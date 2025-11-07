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

        public void StartDawnPhase()
        {
            /*
            // Reveal Witches to each other
            List<Player> witches = players.Where(p => p.HasRole(TryalCardType.Witch)).ToList();
            witches.ForEach(w => w.RevealWitchGroup(witches));

            // Assign the Black Cat
            Player blackCatHolder = witches[RNGService.Rng.NextInt(0, witches.Count)];
            blackCatHolder.AssignBlackCat();

            */
            //Transition to Day phase
            StartCoroutine(ChangePhase(GamePhase.Day, PhaseChangeDelay));
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

        public void HandleNightCardDrawn()
        {
            if (!BeginNightSequence())
            {
                ResolveNightImmediately();
            }
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
            if (newPhase == GamePhase.Setup)
            {
                StartSetupPhase();
            }
        }

        private void StartSetupPhase()
        {
            GameSetup.SetupNewGame(PlayerService.All, PlayerService.All.Count);
            GameTurnManager.Initialize();
            // Transition to Dawn phase
            StartCoroutine(ChangePhase(GamePhase.Dawn, PhaseChangeDelay));
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
        }

        private IEnumerator NightSequenceRoutine()
        {
            yield return ChangePhase(GamePhase.Night, PhaseChangeDelay);
            yield return NightPhaseRoutine();

            yield return ChangePhase(GamePhase.Dawn, PhaseChangeDelay);
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

            yield return ExecuteConstableChoice(alivePlayers, localPlayer, plan, rng);
            yield return ExecuteLocalWitchChoice(alivePlayers, localPlayer, plan, rng);

            NightResolver.Resolve(rng, plan, witchesCanTargetWitches);
            GameManager.Instance.EvaluateEndGame();
        }

        private IEnumerator ExecuteConstableChoice(List<Player> alivePlayers, Player localPlayer, NightResolver.NightPlan plan, IRng rng)
        {
            var constable = alivePlayers.FirstOrDefault(p => p.TryalCards.Any(card => card.TryalCardType == TryalCardType.Constable));
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