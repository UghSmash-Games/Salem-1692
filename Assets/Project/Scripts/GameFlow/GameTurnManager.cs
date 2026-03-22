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
using System.Linq;
using Salem.AirConsole;
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
        public static int CurrentPlayerIndex { get; private set; }
        public static GameTurnManager Instance;
        [SerializeField] private GameManager GameManager;
        [SerializeField] private UIManager UIManager;
        [SerializeField] private float turnDuration = 30f;
        public Player CurrentPlayer => currentPlayer;
        public KeyCode debugTurnAdvanceKey = KeyCode.N;
        public UnityEvent OnTurnStart;
        public UnityEvent OnPhaseTransition;
        public event System.Action<Player> TurnStarted;
        public event System.Action<Player> TurnEnded;

        private int forcedStartingIndex = 0;
        private DeckManager deckManager;
        private Player currentPlayer;
        private float turnTimer;
        private bool isTurnActive = false;
        private bool waitingForHuman;
        private bool turnsStarted;

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
            var phase = FindFirstObjectByType<GamePhaseManager>();
            if (phase != null) phase.OnPhaseChange += HandlePhaseChanged;
        }
        public void SetStartingPlayerIndex(int index)
{
    Debug.Log($"Turn Order Override: Day 1 will start with player index {index}");
    forcedStartingIndex = index;
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

            // Notify AirConsole controllers of the current turn
            if (PlayerService.IsAirConsoleMode && AirConsoleManager.Instance != null)
            {
                AirConsoleManager.Instance.SendGamePhaseToAll("Day", currentPlayer.PlayerNameText);
            }

            StartCoroutine(RunTurn(currentPlayer));
        }

        public void OnPlayerEliminated(Player eliminatedPlayer)
        {
            var players = PlayerService.GetAlivePlayers();
            if (players.Count == 0)
            {
                CurrentPlayerIndex = 0;
                currentPlayer = null;
                GameManager?.EvaluateEndGame();
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
            GameManager?.EvaluateEndGame();
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

            // Track hand before draw for Giles Corey check
            int handSizeBefore = requestingPlayer.HandManager.Hand.Count;
            deckManager.DrawMultipleCards(requestingPlayer.HandManager, 2);
            currentTurnAction = TurnActionChoice.DrawTwoCards;

            // Giles Corey: if both drawn cards are Accusation cards, draw a third
            if (requestingPlayer.HasTownHall(Salem.Cards.TownhallName.GilesCorey))
            {
                var hand = requestingPlayer.HandManager.Hand;
                int newCards = hand.Count - handSizeBefore;
                if (newCards >= 2)
                {
                    var lastTwo = hand.Skip(handSizeBefore).Take(2).ToList();
                    bool bothAccusation = lastTwo.All(c => c is Salem.Cards.ActionCardSO ac && ac.Op == Salem.Cards.ActionOp.Accusation);
                    if (bothAccusation)
                    {
                        deckManager.DrawCard(requestingPlayer.HandManager);
                        Debug.Log($"[TownHall] Giles Corey ({requestingPlayer.PlayerNameText}) drew 2 Accusations — bonus 3rd card drawn.");
                    }
                }
            }

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

        /// <summary>
        /// Samuel Parris ability: draw up to 2 cards from the discard pile instead of the deck.
        /// Counts as the player's turn action. Cannot draw Black cards.
        /// </summary>
        public bool TryDrawFromDiscard(Player requestingPlayer)
        {
            if (!IsCurrentPlayersTurn(requestingPlayer))
            {
                Debug.LogWarning("[TurnManager] Draw from discard attempted outside of turn.");
                return false;
            }

            if (currentTurnAction != TurnActionChoice.None)
            {
                Debug.LogWarning("[TurnManager] Turn action already chosen; cannot draw from discard.");
                return false;
            }

            if (!requestingPlayer.HasTownHall(Salem.Cards.TownhallName.SamuelParris) || requestingPlayer.townHallAbilityCharges <= 0)
            {
                Debug.LogWarning("[TurnManager] Player does not have Samuel Parris ability or no charges left.");
                return false;
            }

            EnsureDeckManager();
            if (!deckManager) return false;

            // Draw up to 2, reject Black cards
            deckManager.DrawFromDiscardPile(requestingPlayer.HandManager, 2,
                c => c.Type == Salem.Cards.Card.CardColor.Black);
            requestingPlayer.ConsumeTownHallCharge();
            currentTurnAction = TurnActionChoice.DrawTwoCards;

            Debug.Log($"[TownHall] Samuel Parris ({requestingPlayer.PlayerNameText}) draws from discard pile. Charges remaining: {requestingPlayer.townHallAbilityCharges}");

            if (requestingPlayer.IsHuman)
            {
                waitingForHuman = false;
            }

            EndTurn();
            return true;
        }

        /// <summary>
        /// Tituba ability: once per game, on her turn before drawing, rearrange the deck.
        /// Current implementation: shuffles the deck. TODO: Full 60-second deck rearrangement UI.
        /// </summary>
        public bool TryUseTitubaAbility(Player requestingPlayer)
        {
            if (!IsCurrentPlayersTurn(requestingPlayer))
                return false;

            if (currentTurnAction != TurnActionChoice.None)
            {
                Debug.LogWarning("[TurnManager] Turn action already chosen; cannot use Tituba ability.");
                return false;
            }

            if (!requestingPlayer.HasTownHall(Salem.Cards.TownhallName.Tituba) || requestingPlayer.townHallAbilityCharges <= 0)
            {
                Debug.LogWarning("[TurnManager] Player does not have Tituba ability or no charges left.");
                return false;
            }

            EnsureDeckManager();
            if (!deckManager) return false;

            // TODO: Replace with full 60-second deck rearrangement UI
            deckManager.ShuffleDeck();
            requestingPlayer.ConsumeTownHallCharge();
            Debug.Log($"[TownHall] Tituba ({requestingPlayer.PlayerNameText}) rearranged the deck. Charges remaining: {requestingPlayer.townHallAbilityCharges}");

            // Tituba's ability counts as the turn action — end turn
            currentTurnAction = TurnActionChoice.DrawTwoCards; // Prevents further actions this turn
            if (requestingPlayer.IsHuman)
                waitingForHuman = false;
            EndTurn();
            return true;
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

            bool isAirConsoleHuman = PlayerService.IsAirConsoleMode
                && current.IsHuman
                && !(current is AIPlayer);

            if (isAirConsoleHuman)
            {
                // AirConsole mode: notify the player's phone controller that it's their turn
                waitingForHuman = true;
                if (AirConsoleManager.Instance != null)
                {
                    AirConsoleManager.Instance.SendTurnNotify(current, true);
                    AirConsoleManager.Instance.SendHandUpdate(current);
                }

                yield return new WaitUntil(() => waitingForHuman == false);

                // Notify controller that turn ended
                if (AirConsoleManager.Instance != null)
                {
                    AirConsoleManager.Instance.SendTurnNotify(current, false);
                }
                yield break;
            }
            else if (current.IsHuman && current.IsLocalPlayer)
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

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Day)
            {
                if (!turnsStarted)
                {
                    turnsStarted = true;
                    StartTurn(forcedStartingIndex); // first ever turn, AFTER Setup+Dawn finished
                }
                else
                {
                    // resuming Day after Night – do not auto-advance here.
                    // If you pause turns on Night, the current player/next index is already set.
                    if (!isTurnActive) StartTurn(CurrentPlayerIndex % PlayerService.GetAlivePlayers().Count);
                }
            }
            else
            {
                // Not Day → pause/stop the turn loop
                isTurnActive = false;
                StopAllCoroutines();
            }
        }
    }
}
