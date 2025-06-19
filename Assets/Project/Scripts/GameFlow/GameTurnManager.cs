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
        [SerializeField] private float turnDuration = 30f;

        public static int CurrentPlayerIndex{ get; private set; }
        public enum GamePhase { Dawn, Day, Night }
        public GamePhase CurrentPhase;
        public UnityEvent OnTurnStart;
        public UnityEvent OnPhaseTransition;
        public KeyCode debugTurnAdvanceKey = KeyCode.N;

        private UIManager UIManager;
        private float turnTimer;
        private bool isTurnActive = false;
        #endregion

        private void Awake()
        {
            UIManager = GetComponent<UIManager>();

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
            turnTimer = turnDuration;
            CurrentPlayerIndex = playerIndex;
            Player currentPlayer = PlayerService.All[CurrentPlayerIndex];
            //Debug.Log($"Starting turn for {PlayerService.All[playerIndex].PlayerNameText}");

            //Add In Later For Advance UI
            UIManager.SetPlayerTurnActive();

            isTurnActive = true;

            if (currentPlayer is AIPlayer ai)
            {
                ai.StartTurn(() => EndTurn());
            }
        }

        public void EndTurn()
        {
            isTurnActive = false;
            Debug.Log($"Ending turn for {PlayerService.GetAlivePlayers()[CurrentPlayerIndex].PlayerNameText}");

            //Add In later for Advance UI
            //Players[currentPlayerIndex].EndTurnEffects();

            int nextIndex = (CurrentPlayerIndex + 1) % PlayerService.GetAlivePlayers().Count;

            StartTurn(nextIndex); // Move to the next player's turn
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