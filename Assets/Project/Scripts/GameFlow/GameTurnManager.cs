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
using System.Collections;
namespace Salem.GameFlow
{
    public class GameTurnManager : MonoBehaviour
    {
        #region Vars
        public static int CurrentPlayerIndex{ get; private set; }
        public static GameTurnManager Instance;
        [SerializeField] private GameManager GameManager;
        [SerializeField] private UIManager UIManager;
        [SerializeField] private float turnDuration = 30f;
        public Player CurrentPlayer => currentPlayer;
        public UnityEvent OnTurnStart;
        public UnityEvent OnPhaseTransition;
        public KeyCode debugTurnAdvanceKey = KeyCode.N;

        private float turnTimer;
        private bool isTurnActive = false;
        private Player currentPlayer;
        private bool waitingForHuman;
        #endregion

        private void OnValidate()
        {
            if (!UIManager) UIManager = FindFirstObjectByType<UIManager>();
            if (!GameManager) GameManager = FindFirstObjectByType<GameManager>();
        }
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
            if (players.Count == 0) return;
            
            turnTimer = turnDuration;

            if (playerIndex >= players.Count) playerIndex = 0;
            
            CurrentPlayerIndex = playerIndex;

            currentPlayer = players[CurrentPlayerIndex];
            Debug.Log($"Starting turn for {currentPlayer.PlayerNameText}");

            isTurnActive = true;

            //Add In Later For Advance UI
            StartCoroutine(RunTurn(currentPlayer));
        }
        
        private IEnumerator RunTurn(Player current)
        {
            UIManager.SetPlayerTurnActive(); // your existing UI cue

            if (current.IsHuman && current.IsLocalPlayer)
            {
                waitingForHuman = true;
                // Enable local input – e.g., show hand interactivity
                //PlayerInputUI.EnableInputFor(current);

                // Wait until a card is played or End Turn is pressed
                yield return new WaitUntil(() => waitingForHuman == false);
                yield break;
            }
            else
            {
                // AI path
                if (current.TryGetComponent<AIPlayer>(out var ai))
                {
                    yield return StartCoroutine(ai.TakeTurnOnce());
                }
                else GameTurnManager.Instance.EndTurn();
            }

            // advance to next player (your existing logic)
        }

        public void OnHumanActionResolved()
        {
            waitingForHuman = false;
        }

        public void EndTurn()
        {
            if (!isTurnActive) return;
            isTurnActive = false;

            var players = PlayerService.GetAlivePlayers();
            if (players.Count == 0) return;

            Debug.Log($"Ending turn for {currentPlayer.PlayerNameText}");

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
        #endregion
    }
}