/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Central controller that links gameplay systems, tracks players, and checks endgame conditions.
*   Responsibilities:
*        • Initialize game
*        • Track players
*        • Delegate to phase/turn/state managers
*        • Trigger endgame when conditions met
*   Access Requirements:
*        • GamePhaseManager
*        • GameTurnManager
*        • PlayerManager
*        • DeckManager
*        • UIManager

* TODO: [Planned improvements]
 * FIXME: [Known bugs or issues]
*/
using System;
using System.Linq;
using Salem.Cards;
using Salem.Data;
using Salem.Players;
using Salem.UI;
using UnityEngine;

namespace Salem.GameFlow
{
    [DefaultExecutionOrder(-100)] // ensure this Awake Runs before other managers
    public class GameManager : MonoBehaviour
    {
        #region Vars
        [Header("RNG")]
        [SerializeField] private bool useFixedSeed = false;
        [SerializeField] private ulong fixedSeed = 123456789UL;
        public IRng Rng { get; private set; }
        public ulong Seed{ get; private set; }

        //Tracks GameManager
        public static GameManager Instance { get; private set; }
        [SerializeField] private UIManager UIManager;

        public event Action<EndGameResult> OnGameEnded;

        //for central control of input/time when game ends
        [SerializeField] private bool pauseOnGameEnd = true;


        private bool isGameActive;
        private bool gameAlreadyEnded = false;
        #endregion

        #region Standard Functions
        private void OnValidate()
        {
            if (!UIManager) UIManager = FindFirstObjectByType<UIManager>();
        }
        void Awake()
        {
            HandleInstance();
            PopulatePlayers();
            isGameActive = true;
        }

        void Start()
        {
            //Debug.Log($"[GameManager] Total players registered: {PlayerService.All.Count}");
            /*foreach (var p in PlayerService.All)
            {
                Debug.Log($" - Player: {p.PlayerNameText}, IsLocal: {p.IsLocalPlayer}");
            }
            */
            UIManager.SetupLocalPlayerUI(PlayerService.GetLocalPlayer());

            /*AIR CONSOLE DISABLED 4/28/26
            // In AirConsole mode, there is no single local player — input comes from phones
            if (!PlayerService.IsAirConsoleMode)
            {
                UIManager.SetupLocalPlayerUI(PlayerService.GetLocalPlayer());
            }
            */
        }
        #endregion

        #region Accessor Functions
        public void EvaluateEndGame()
        {
            var alive = PlayerService.GetAlivePlayers();
            if (alive == null || alive.Count == 0) return;

             // Townspeople win when ALL Witch Tryal cards in the game have been revealed.
            // Check across all players (alive and eliminated) for any unrevealed Witch cards.
            bool anyUnrevealedWitch = PlayerService.All.Any(p =>
                p.TryalCards != null && p.TryalCards.Any(c =>
                    c.TryalCardType == TryalCardType.Witch && !c.IsRevealed));

            if (!anyUnrevealedWitch)
            {
                var winners = alive.Where(p => !p.IsWitch).ToList();
                RaiseGameEnded(new EndGameResult(Team.Villagers, winners, "All Witch Tryal cards revealed"));
                return;
            }

            // Witches win when all remaining alive players are witches
            // (covers both: all townspeople eliminated, OR final townsperson became a witch)
            int witches = alive.Count(p => p.IsWitch && !p.IsEliminated);
            int nonWitches = alive.Count - witches;

            // villagers win if all witches dead
            if (witches == 0)
            {
                var winners = alive.Where(p => !p.IsWitch).ToList();
                RaiseGameEnded(new EndGameResult(Team.Villagers, winners, "All witches eliminated"));
                return;
            }

            // Witches also win at parity (witches >= townspeople)
            if (witches >= nonWitches)
            {
                var winners = alive.Where(p => p.IsWitch).ToList();
                RaiseGameEnded(new EndGameResult(Team.Witches, winners, "Witches reached parity"));
                return;
            }
        }

        // Call EvaluateEndGame() at key points:
        public void OnDayLynchResolved() => EvaluateEndGame();
        public void OnNightResolved() => EvaluateEndGame();
        public void OnPlayerLeftGame() => EvaluateEndGame();

        public void InitRng(ulong? seed = null)
        {
            Seed = seed ?? (useFixedSeed ? fixedSeed : (ulong)System.DateTime.UtcNow.Ticks);
            Rng  = new XorShiftRng(Seed);
            //Debug.Log($"[GameManager] RNG initialized Seed={Seed}");
        }

        // Optional for replays/debug:
        public void Reseed(ulong newSeed) => InitRng(newSeed);
        #endregion

        #region Helper Functions
        //Ensures only 1 GameManager
        private void HandleInstance()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            InitRng(); // set Rng + Seed
        }

        //Finds ALL Players and stores them in an accessible list
        private void PopulatePlayers()
        {
            // Ensure the list is empty before populating
            PlayerService.Clear();

            GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
            GameObject[] aiObjects = GameObject.FindGameObjectsWithTag("AI");

            foreach (var obj in playerObjects.Concat(aiObjects))
            {
                Player playerComponent = obj.GetComponent<Player>();
                if (playerComponent != null)
                {
                    PlayerService.Register(playerComponent);
                }
                else
                {
                    Debug.LogWarning($"GameObject {obj.name} is tagged as Player/AI but has no Player component.");
                }
            }
        }

        private void RaiseGameEnded(EndGameResult result)
        {
            if (gameAlreadyEnded) return;
            gameAlreadyEnded = true;
            if (pauseOnGameEnd) Time.timeScale = 0f;
            OnGameEnded?.Invoke(result);
        }

        private void OnEnable()
        {
            PlayerService.OnPlayerEliminated += HandlePlayerEliminated;
        }
        private void OnDisable()
        {
            PlayerService.OnPlayerEliminated -= HandlePlayerEliminated;
        }
        private void HandlePlayerEliminated(Player p, EliminationCause cause)
        {
            EvaluateEndGame();
        }
        #endregion

        //TEMP FOR TESTING 5/16/26
        [ContextMenu("TEST End Game - Villagers Win")]
        private void TestVillagersWin()
        {
            var winners = PlayerService.GetAlivePlayers()
                .Where(p => !p.IsWitch)
                .ToList();

            RaiseGameEnded(new EndGameResult(
                Team.Villagers,
                winners,
                "Test villagers win"
            ));
        }
    }
}