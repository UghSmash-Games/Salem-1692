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
        [SerializeField] private EndGameUI EndGameUI;

        //Tracks GameManager
        public static GameManager Instance { get; private set; }
        [SerializeField] private UIManager UIManager;

        private bool isGameActive;
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
        public void CheckEndgameConditions()
        {
            int activeVillagers = PlayerService.GetAliveVillagers().Count;
            int activeWitches = PlayerService.GetAliveWitches().Count;

            if (activeWitches == 0)
            {
                EndGame("Villagers Win!");
            }
            else if (activeVillagers == 0 || activeWitches >= activeVillagers)
            {
                EndGame("Witches Win!");
            }
        }

        public void EndGame(string result)
        {
            Debug.Log(result);
            isGameActive = false;

            // Display the endgame UI
            EndGameUI.Show(result);

            // Provide options to restart or quit
            EndGameUI.OnRestart += RestartGame;
            EndGameUI.OnQuit += QuitGame;
        }

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



        private void RestartGame()
        {
            Debug.Log("Restarting Game...");
            EndGameUI.OnRestart -= RestartGame; // Unsubscribe to prevent memory leaks
            EndGameUI.OnQuit -= QuitGame;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // Reload scene
        }

        private void QuitGame()
        {
            Debug.Log("Quitting Game...");
            EndGameUI.OnRestart -= RestartGame;
            EndGameUI.OnQuit -= QuitGame;
            Application.Quit();
        }
        #endregion
    }
}