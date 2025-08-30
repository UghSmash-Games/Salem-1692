/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Manages active player turns and progression.
*   Responsibilities:**
*        • Control turn order
*        • Track current player
*        • End/start turns
*   Access Requirements:**
*        • GameStateManager
*        • PlayerManager

* TODO: Check for win/loss conditions (e.g., all Witches eliminated).
*       Resolve any "end of turn" effects (e.g., card-based triggers).
*       Update the UI and notify the next player to begin their turn.
* FIXME: Get Players list from GameManager??
*           This Script will work in conjunction with the GamePhaseManager
*/
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Salem.Managers.GameState;
using Salem.Players;
using Salem.UI;
using Salem.Data;
namespace Salem.GameFlow
{
    public class GameTurnManager : MonoBehaviour
    {
        #region Vars
        public static int CurrentPlayerIndex{ get; private set; }
        public static GameTurnManager Instance;
        [SerializeField] private float turnDuration = 30f;

        public GamePhase CurrentPhase;
        public UnityEvent OnTurnStart;
        public UnityEvent OnPhaseTransition;
        public KeyCode debugTurnAdvanceKey = KeyCode.N;

        private GameManager GameManager;
        private UIManager UIManager;
        private float turnTimer;
        private bool isTurnActive = false;
        private Player currentPlayer;
        #endregion

        private void Awake()
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

            GameManager = FindFirstObjectByType<GameManager>();
            UIManager = FindFirstObjectByType<UIManager>();

        }

        private void Update()
        {
            if (!isTurnActive) return;
            turnTimer -= Time.deltaTime;
            if (turnTimer <= 0f)
            {
                Debug.Log("Turn timer expired.");
                EndTurn();
            }
        }


        #region Accessor Functions
        public void Initialize()
        {
            //Debug.Log("Initializing GameTurnManager.");
            StartTurn(0);
        }
        public void StartTurn(int playerIndex)
        {
            var players = PlayerService.GetAlivePlayers();
            if (players.Count == 0)
                return;
            
            turnTimer = turnDuration;

            if (playerIndex >= players.Count)
                playerIndex = 0;
            
            CurrentPlayerIndex = playerIndex;

            currentPlayer = players[CurrentPlayerIndex];
            Debug.Log($"Starting turn for {currentPlayer.PlayerNameText}");

            isTurnActive = true;

            //Add In Later For Advance UI
            UIManager.SetPlayerTurnActive();

            if (currentPlayer is AIPlayer ai)
            {
                ai.StartTurn(() => EndTurn());
            }
        }

        public void EndTurn()
        {
            var players = PlayerService.GetAlivePlayers();
            if (players.Count == 0)
                return;
            
            isTurnActive = false;

            Debug.Log($"Ending turn for {currentPlayer.PlayerNameText}");

            //Add In later for Advance UI
            //Players[currentPlayerIndex].EndTurnEffects();

            int nextIndex = (CurrentPlayerIndex + 1) % players.Count;

            StartTurn(nextIndex); // Move to the next player's turn
        }

        public void OnPlayerEliminated(Player eliminatedPlayer)
        {
            var players = PlayerService.GetAlivePlayers();
            if (players.Count == 0)
            {
                CurrentPlayerIndex = 0;
                currentPlayer = null;
                return;
            }

            int newIndex = players.IndexOf(currentPlayer);
            if (newIndex == -1)
            {
                CurrentPlayerIndex %= players.Count;
                currentPlayer = players[CurrentPlayerIndex];
            }
            else
            {
                CurrentPlayerIndex = newIndex;
            }

            UIManager.SetPlayerTurnActive();
        }

        public void SkipTurnDebug()
        {
            if (!isTurnActive) return;
            EndTurn();
        }

        public void AdvancePhase()
        {
            switch (CurrentPhase)
            {
                case GamePhase.Dawn:
                    HandleDawnPhase();
                    CurrentPhase = GamePhase.Day;
                    break;

                case GamePhase.Day:
                    CurrentPhase = GamePhase.Night;
                    break;

                case GamePhase.Night:
                    HandleNightPhase();
                    CurrentPhase = GamePhase.Dawn;
                    break;
            }
        }
        #endregion

        #region Helper Functions
        private void HandleNightPhase()
        {
            throw new NotImplementedException();
        }

        private void HandleDawnPhase()
        {
            throw new NotImplementedException();
        }


        private void NotifyTurnStart()
        {
            OnTurnStart?.Invoke();
        }

        private void NotifyPhaseTransition()
        {
            OnPhaseTransition?.Invoke();
        }
        #endregion
    }
}