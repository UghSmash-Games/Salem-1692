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
using System.Linq;
using Salem.Cards;
using Salem.Data;
using Salem.Deck;
using Salem.Gameplay.Setup;
using Salem.Players;
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
        public static GamePhaseManager Instance { get; private set; }
        public GamePhase CurrentPhase { get; private set; }
        public delegate void PhaseChangeHandler(GamePhase newPhase);
        public event PhaseChangeHandler OnPhaseChange;
        public KeyCode DebugAdvancePhaseKey = KeyCode.P;

        private GameSetup GameSetup;
        private GameTurnManager GameTurnManager;
        private bool isResolvingNight;
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
            StartCoroutine(ChangePhase(GamePhase.Day,PhaseChangeDelay));
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

        public void HandleNightCardDrawn(Player drawer, Card nightCard)
        {
            if (isResolvingNight)
            {
                return;
            }

            StartCoroutine(ResolveNightFromCard(drawer, nightCard));
        }

        public void StartNightPhase(Player nightDrawer = null, Card nightCard = null)
        {
            ResolveNightSequence(nightDrawer, nightCard);
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
        

        private IEnumerator ResolveNightFromCard(Player drawer, Card nightCard)
        {
            isResolvingNight = true;
            GameTurnManager?.OnPhaseTransition?.Invoke();

            yield return StartCoroutine(ChangePhase(GamePhase.Night, PhaseChangeDelay));

            StartNightPhase(drawer, nightCard);

            yield return StartCoroutine(ChangePhase(GamePhase.Day, PhaseChangeDelay));

            isResolvingNight = false;
        }

        private void ResolveNightSequence(Player drawer, Card nightCard)
        {
            var gameManager = GameManager.Instance;
            var rng = gameManager != null ? gameManager.Rng : new XorShiftRng(1UL);

            var alive = PlayerService.GetAlivePlayers();
            var witches = alive.Where(p => p.IsWitch && !p.IsEliminated).ToList();
            if (witches.Count == 0)
            {
                Debug.Log("[Night] No witches remain. Night passes quietly.");
                DeckManager?.ReshuffleAndPlaceNightCard(nightCard);
                return;
            }

            var eligible = alive.Where(p => !p.IsEliminated && !p.hasAsylum).ToList();
            if (eligible.Count == 0)
            {
                Debug.Log("[Night] No eligible targets. Night ends without incident.");
                DeckManager?.ReshuffleAndPlaceNightCard(nightCard);
                return;
            }

            var tally = eligible.ToDictionary(p => p, _ => 0);
            foreach (var witch in witches)
            {
                var choice = eligible[RNGService.Rng.NextInt(0, eligible.Count)];
                tally[choice]++;
            }

            int best = tally.Values.Max();
            var topChoices = tally.Where(kv => kv.Value == best).Select(kv => kv.Key).ToList();
            var victim = topChoices[RNGService.Rng.NextInt(0, topChoices.Count)];

            Debug.Log($"[Night] Witches targeted {victim.PlayerNameText}.");

            var constable = alive.FirstOrDefault(p => p.IsConstable && !p.IsEliminated);
            Player protectedPlayer = null;
            if (constable != null)
            {
                var protectable = alive.Where(p => p != constable && !p.IsEliminated).ToList();
                if (protectable.Count > 0)
                {
                    protectedPlayer = protectable[RNGService.Rng.NextInt(0, protectable.Count)];
                    Debug.Log($"[Night] {constable.PlayerNameText} protected {protectedPlayer.PlayerNameText}.");
                }
            }

            bool confessed = victim.TryConfessToSurvive();
            if (confessed)
            {
                Debug.Log($"[Night] {victim.PlayerNameText} confessed and revealed a Tryal card to avoid elimination.");
            }

            bool savedByConstable = protectedPlayer != null && victim == protectedPlayer;
            if (savedByConstable)
            {
                Debug.Log($"[Night] {victim.PlayerNameText} was saved by the Constable.");
            }

            if (!savedByConstable && !confessed)
            {
                Debug.Log($"[Night] {victim.PlayerNameText} was eliminated by the witches.");
                victim.EliminateNow();
            }

            gameManager?.CheckEndgameConditions();

            DeckManager?.ReshuffleAndPlaceNightCard(nightCard);
        }
        #endregion
    }
}