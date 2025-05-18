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
    public class GameManager : MonoBehaviour
    {
        #region Vars
        //[SerializeField] private PlayerHandUI PlayerHandUI;
        [SerializeField] private EndGameUI EndGameUI;
        [SerializeField] private PlayerStatusUI PlayerStatusUI;
        [SerializeField] private List<PlayerStatusUI> AI_PlayerStatusUIPanels;
        [SerializeField] private PlayerInputUI PlayerInputUI;


        //Tracks GameManager
        public static object Instance { get; internal set; }
        public List<Player> players;
    
        private bool isGameActive;
        #endregion

        #region Standard Functions
        void Awake()
        {
            HandleInstance();
            PopulatePlayers();
            isGameActive = true;
        }

        void Start()
        {
            LinkPlayersToUIPanels(players);
        }
        #endregion

        #region Accessor Functions
        public void LinkPlayersToUIPanels(List<Player> players)
        {
            for (int i = 0; i < AI_PlayerStatusUIPanels.Count; i++)
            {
                if (i < players.Count-1)
                {
                    AI_PlayerStatusUIPanels[i].gameObject.SetActive(true);
                    AI_PlayerStatusUIPanels[i].Initialize(players[i]);
                    players[i].StatusUI = AI_PlayerStatusUIPanels[i];
                    print("Assigned AI UI Pannel for player count " + i);
                }
                else
                {
                    AI_PlayerStatusUIPanels[i].gameObject.SetActive(false);
                }
            }
        }
        //Updates CardsUI
        public void UpdateLocalPlayerUI()
        {
            //Update local player's hand
            var localPlayer = players[0];
            var data = new PlayerHandData(localPlayer.HandManager.GetCards(), localPlayer.TryalCards);
            //PlayerHandUI.UpdateFromData(data);
            //PlayerInputUI.UpdateHand();
            PlayerStatusUI.Initialize(localPlayer);
            localPlayer.StatusUI = PlayerStatusUI;
        }
        
        public void CheckEndgameConditions()
        {
            int activeVillagers = players.Count(p => !p.IsWitch && !p.IsEliminated);
            int activeWitches = players.Count(p => p.IsWitch && !p.IsEliminated);

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
        #endregion
    
        #region Helper Functions
        //Ensures only 1 GameManager
        private void HandleInstance()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
  
        //Finds ALL Players and stores them in an accessible list
        private void PopulatePlayers()
        {
            // Ensure the list is empty before populating
            players.Clear(); 

            GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
            GameObject[] aiObjects = GameObject.FindGameObjectsWithTag("AI");

            foreach (var obj in playerObjects.Concat(aiObjects))
            {
                Player playerComponent = obj.GetComponent<Player>();
                if (playerComponent != null)
                {
                    players.Add(playerComponent);
                    Debug.Log($"Added player: {playerComponent.PlayerName}");
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