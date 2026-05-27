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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Salem.GameFlow;
using Salem.Players;
using Salem.Deck;
using Salem.UI;
using Salem.Data;

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
        //[SerializeField] private EndGameUI EndGameUI;

        //Tracks GameManager
        public static GameManager Instance { get; private set; }
        [SerializeField] private UIManager UIManager;

        public event Action<EndGameResult> OnGameEnded;

        //for central control of input/time when game ends
        [SerializeField] private bool pauseOnGameEnd = true;


        private bool isGameActive;
        private bool endGameHandlersRegistered;
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

            UIManager.BindAllPlayerStatusUI();
            UIManager.SetupLocalPlayerUI(PlayerService.GetLocalPlayer());
        }
        #endregion

        #region Accessor Functions
        public void EvaluateEndGame()
        {
            var alive = PlayerService.GetAlivePlayers();
            if (alive == null || alive.Count == 0) return;

            int witches = alive.Count(p => p.IsWitch && !p.IsEliminated);
            int nonWitches = alive.Count - witches;

            // villagers win if all witches dead
            if (witches == 0)
            {
                var winners = alive.Where(p => !p.IsWitch).ToList();
                RaiseGameEnded(new EndGameResult(Team.Villagers, winners, "All witches eliminated"));
                return;
            }

            if (witches >= nonWitches)
            {
                var winners = alive.Where(p => p.IsWitch).ToList();
                RaiseGameEnded(new EndGameResult(Team.Witches, winners, "Witches reached parity"));
                return;
            }
        }
        /*public void CheckEndgameConditions()
        {
            if (!isGameActive)
            {
                return;
            }

            var aliveVillagers = PlayerService.GetAliveVillagers();
            var aliveWitches = PlayerService.GetAliveWitches();

            if (aliveWitches.Count == 0)
            {
                EndGame(FormatVictoryMessage("Villagers", aliveVillagers));
            }
            else if (aliveVillagers.Count == 0 || aliveWitches.Count >= aliveVillagers.Count)
            {
                EndGame(FormatVictoryMessage("Witches", aliveWitches));
            }
        }*/

        // Call EvaluateEndGame() at key points:
        public void OnDayLynchResolved() => EvaluateEndGame();
        public void OnNightResolved() => EvaluateEndGame();
        public void OnPlayerLeftGame() => EvaluateEndGame();

        /*public void EndGame(string result)
        {
            if (!isGameActive)
            {
                return;
            }

            Debug.Log(result);
            isGameActive = false;

            // Display the endgame UI
            if (EndGameUI != null)
            {
                EndGameUI.Show(result);
            }
            else
            {
                Debug.LogWarning("[GameManager] EndGameUI reference missing. Unable to display endgame screen.");
            }

            // Provide options to restart or quit
            if (!endGameHandlersRegistered)
            {
                EndGameUI.OnRestart += RestartGame;
                EndGameUI.OnQuit += QuitGame;
                endGameHandlersRegistered = true;
            }
        }*/

        public void InitRng(ulong? seed = null)
        {
            Seed = seed ?? (useFixedSeed ? fixedSeed : (ulong)System.DateTime.UtcNow.Ticks);
            Rng  = new XorShiftRng(Seed);
            Debug.Log($"[GameManager] RNG initialized Seed={Seed}");
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

        /*private void RestartGame()
        {
            Debug.Log("Restarting Game...");
            if (endGameHandlersRegistered)
            {
                EndGameUI.OnRestart -= RestartGame; // Unsubscribe to prevent memory leaks
                EndGameUI.OnQuit -= QuitGame;
                endGameHandlersRegistered = false;
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // Reload scene
        }

        private void QuitGame()
        {
            Debug.Log("Quitting Game...");
            if (endGameHandlersRegistered)
            {
                EndGameUI.OnRestart -= RestartGame;
                EndGameUI.OnQuit -= QuitGame;
                endGameHandlersRegistered = false;
            }
            Application.Quit();
        }

        private static string FormatVictoryMessage(string faction, IReadOnlyCollection<Player> winners)
        {
            if (winners == null || winners.Count == 0)
            {
                return $"{faction} Win!";
            }

            string survivorList = string.Join(", ", winners.Select(p => p.PlayerNameText));
            return $"{faction} Win!\nSurvivors: {survivorList}";
        }*/

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
    }
}