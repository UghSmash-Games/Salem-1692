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
using System.Collections;
using System.Collections.Generic;
using Salem.Data;
using Salem.Deck;
using Salem.Managers.GameState;
using Salem.Players;
using Salem.UI;
using UnityEngine;
using UnityEngine.Events;

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

        public event System.Action<Player> TurnStarted;
        public event System.Action<Player> TurnEnded;

        private float turnTimer;
        private bool isTurnActive = false;
        private Player currentPlayer;
        private bool waitingForHuman;
        private DeckManager deckManager;

        private enum TurnActionChoice
        {
            None,
            DrawTwoCards,
            PlayCards
        }
        private TurnActionChoice currentTurnAction = TurnActionChoice.None;
        #endregion

        private void OnValidate()
        {
            if (!UIManager) UIManager = FindFirstObjectByType<UIManager>();
            if (!GameManager) GameManager = FindFirstObjectByType<GameManager>();
            if (!deckManager) deckManager = FindFirstObjectByType<DeckManager>();
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

            if (!deckManager) deckManager = FindFirstObjectByType<DeckManager>();
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
            waitingForHuman = false;
            currentTurnAction = TurnActionChoice.None;
            TurnStarted?.Invoke(currentPlayer);
            OnTurnStart?.Invoke();

            //Add In Later For Advance UI
            StartCoroutine(RunTurn(currentPlayer));
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

        public bool TryBeginPlayPhase(Player requestingPlayer)
        {
            if (!IsCurrentPlayersTurn(requestingPlayer))
            {
                Debug.LogWarning("[TurnManager] Attempted to play cards when it is not this player's turn.");
                return false;
            }

            if (currentTurnAction == TurnActionChoice.DrawTwoCards)
            {
                Debug.LogWarning("[TurnManager] Cannot play cards after choosing to draw this turn.");
                return false;
            }

            if (currentTurnAction == TurnActionChoice.None)
            {
                currentTurnAction = TurnActionChoice.PlayCards;
            }

            return true;
        }

        public bool TryDrawTwoCards(Player requestingPlayer)
        {
            if (!IsCurrentPlayersTurn(requestingPlayer))
            {
                Debug.LogWarning("[TurnManager] Attempted to draw outside of the current player's turn.");
                return false;
            }

            if (currentTurnAction != TurnActionChoice.None)
            {
                Debug.LogWarning("[TurnManager] Turn action already chosen; cannot draw cards now.");
                return false;
            }

            EnsureDeckManager();
            if (!deckManager)
            {
                return false;
            }

            deckManager.DrawMultipleCards(requestingPlayer.HandManager, 2);
            currentTurnAction = TurnActionChoice.DrawTwoCards;

            if (requestingPlayer.IsHuman)
            {
                waitingForHuman = false;
            }

            EndTurn();
            return true;
        }

        public void NotifyCardPlayed(Player actingPlayer)
        {
            if (!IsCurrentPlayersTurn(actingPlayer))
            {
                return;
            }

            if (currentTurnAction == TurnActionChoice.None)
            {
                currentTurnAction = TurnActionChoice.PlayCards;
            }

            if (!actingPlayer.IsHuman)
            {
                EndTurn();
            }
        }

        public void RequestEndTurn(Player requestingPlayer)
        {
            if (!IsCurrentPlayersTurn(requestingPlayer))
            {
                return;
            }

            if (requestingPlayer.IsHuman)
            {
                waitingForHuman = false;
            }

            EndTurn();
        }

        public void EndTurn()
        {
            if (!isTurnActive) return;
            isTurnActive = false;

            TurnEnded?.Invoke(currentPlayer);

            var players = PlayerService.GetAlivePlayers();
            if (players.Count == 0) return;

            Debug.Log($"Ending turn for {currentPlayer.PlayerNameText}");

            int nextIndex = (CurrentPlayerIndex + 1) % players.Count;

            StartTurn(nextIndex); // Move to the next player's turn
        }
        #endregion

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

        private bool IsCurrentPlayersTurn(Player player)
        {
            return isTurnActive && player != null && player == currentPlayer;
        }

        private void EnsureDeckManager()
        {
            if (!deckManager)
            {
                deckManager = FindFirstObjectByType<DeckManager>();
                if (!deckManager)
                {
                    Debug.LogError("[TurnManager] DeckManager reference missing; cannot resolve draw actions.");
                }
            }
        }

        
    }
}