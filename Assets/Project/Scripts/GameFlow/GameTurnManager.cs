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
        [SerializeField] private TableLayoutController TableLayoutController;
        [SerializeField] private float turnDuration = 60f;
        // Tituba's deck-rearrange window (rules value from the card: "rearrange for 60
        // seconds"). The host owns this deadline; the phone shows the same as a countdown.
        [SerializeField] private float titubaRearrangeTimeout = 60f;
        [SerializeField] private EndTurnButtonUI endTurnButtonUI;
        [SerializeField] private DrawFromDiscardButtonUI drawFromDiscardButtonUI;
        public Player CurrentPlayer => currentPlayer;
        public KeyCode debugTurnAdvanceKey = KeyCode.N;
        public UnityEvent OnTurnStart;
        public UnityEvent OnPhaseTransition;
        public event System.Action<Player> TurnStarted;
        public event System.Action<Player> TurnEnded;

        private DeckManager deckManager;
        private Player currentPlayer;
        private float turnTimer;
        private int turnId;   // increments each StartTurn; lets async inputs detect their turn ended
        private bool isTurnActive = false;
        private bool waitingForHuman;
        private bool suppressIdleTimer; // true during a Tituba rearrange (it has its own 60s deadline)

        /// <summary>
        /// True while a local-UI human turn is blocked waiting for input. The UI
        /// button handlers (TryDrawTwoCards, RequestEndTurn, etc.) set this false
        /// to unblock the turn. Exposed so LocalUIInput can drive the same wait.
        /// </summary>
        public bool WaitingForHuman
        {
            get => waitingForHuman;
            set => waitingForHuman = value;
        }

        /// <summary>
        /// Monotonic id, bumped at each turn start. Async inputs (e.g. NetworkInput)
        /// capture it and bail out if it changes — i.e. the turn was force-ended.
        /// </summary>
        public int TurnId => turnId;

        /// <summary>
        /// Reset the inactivity window. Call on each player action so an
        /// actively-playing player never times out on cumulative turn time.
        /// </summary>
        public void ResetIdleTimer() => turnTimer = turnDuration;
        private bool turnsStarted;
        private int forcedStartingIndex = 0;

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
            // suppressIdleTimer: during a Tituba rearrange the inactivity timer is paused —
            // that window has its own host-owned 60s deadline in RequestDeckRearrange.
            if (!isTurnActive || suppressIdleTimer) return;
            turnTimer -= Time.deltaTime;
            if (turnTimer <= 0f)
            {
                HandleIdleTimeout();
            }
        }

        /// <summary>
        /// Fired only after genuine inactivity (the timer resets on each action).
        /// If the player already committed to the play path, just end their turn —
        /// do NOT force a draw. Otherwise Draw 2 is the correct default.
        /// </summary>
        private void HandleIdleTimeout()
        {
            if (!isTurnActive || currentPlayer == null) return;

            if (currentTurnAction == TurnActionChoice.PlayCards)
            {
                Debug.Log($"[IdleTimer] {currentPlayer.PlayerNameText} inactive {turnDuration}s mid-play — ending turn.");
                waitingForHuman = false;
                EndTurn();
            }
            else
            {
                Debug.Log($"[IdleTimer] {currentPlayer.PlayerNameText} inactive {turnDuration}s — forcing draw two cards.");
                ForceDrawAndEndTurn();
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
            //Debug.Log($"Turn Order Override: Day 1 will start with player index {index}");
            forcedStartingIndex = index;
        }

        public void StartTurn(int playerIndex)
        {
            endTurnButtonUI.Hide();
            var players = PlayerService.GetAlivePlayers();
            if (players.Count == 0) return;

            turnTimer = turnDuration;
            turnId++;

            if (playerIndex >= players.Count) playerIndex = 0;

            CurrentPlayerIndex = playerIndex;

            currentPlayer = players[CurrentPlayerIndex];

            TableLayoutController.SetCurrentTurn(currentPlayer);
            //Debug.Log($"Starting turn for {currentPlayer.PlayerNameText}");

            // Stocks: if this player has a Stocks card, skip their turn and consume one
            if (currentPlayer.skipTurn)
            {
                Debug.Log($"{currentPlayer.PlayerNameText}'s turn is skipped (Stocks).");
                currentPlayer.ConsumeOneStocks();
                int nextIndex = (CurrentPlayerIndex + 1) % players.Count;
                StartTurn(nextIndex);
                return;
            }

            isTurnActive = true;
            waitingForHuman = false;
            currentTurnAction = TurnActionChoice.None;
            TurnStarted?.Invoke(currentPlayer);
            UpdateTownHallActionButtons(CurrentPlayer);
            OnTurnStart?.Invoke();

            /*AIRCONSOLE DISABLED TEMP 4/28/26
            // Notify AirConsole controllers of the current turn
            if (PlayerService.IsAirConsoleMode && AirConsoleManager.Instance != null)
            {
                AirConsoleManager.Instance.SendGamePhaseToAll("Day", currentPlayer.PlayerNameText);
            }
            */

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

            int newIndex = players.IndexOf(eliminatedPlayer);
            if (newIndex == -1)
            {
                CurrentPlayerIndex %= players.Count;
                currentPlayer = players[CurrentPlayerIndex];
            }
            else
            {
                CurrentPlayerIndex = newIndex;
            }

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

            drawFromDiscardButtonUI?.Hide();

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
            drawFromDiscardButtonUI?.Hide();

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

            // Draw up to 2, reject black cards (Night/Conspiracy). Identify by name via
            // Card.IsBlackCard — they are authored as CardColor.White, so the old `Type == Black`
            // check rejected nothing and let Parris draw a Conspiracy from the discard.
            deckManager.DrawFromDiscardPile(requestingPlayer.HandManager, 2,
                Salem.Cards.Card.IsBlackCard);
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
        /// Tituba ability (rulebook p14): once per game, on her turn BEFORE drawing, view and
        /// rearrange the whole deck — then still take her normal turn (rearrange AND draw the
        /// same turn). Driven over the network via IPlayerInput.RequestDeckRearrange. The
        /// idle timer is paused for the rearrange window (it owns its own 60s deadline); on
        /// completion the charge is spent and the idle window is refreshed for her draw/play.
        /// Does NOT consume the turn action and does NOT end the turn — the caller's turn loop
        /// continues. Offered only before any play (gated by the caller) and only with a charge.
        /// </summary>
        public IEnumerator RunTitubaRearrange(Player requestingPlayer)
        {
            if (!IsCurrentPlayersTurn(requestingPlayer)) yield break;
            if (requestingPlayer == null ||
                !requestingPlayer.HasTownHall(Salem.Cards.TownhallName.Tituba) ||
                requestingPlayer.townHallAbilityCharges <= 0)
            {
                Debug.LogWarning("[TurnManager] Tituba rearrange requested without the ability or a charge — ignored.");
                yield break;
            }

            EnsureDeckManager();
            if (!deckManager) yield break;

            suppressIdleTimer = true; // pause inactivity timer; the rearrange has its own deadline
            var deck = deckManager.GetDeckCards();
            yield return requestingPlayer.Input.RequestDeckRearrange(
                requestingPlayer, deck, titubaRearrangeTimeout,
                order => { if (order != null) deckManager.SetDeckOrder(order); });
            suppressIdleTimer = false;

            requestingPlayer.ConsumeTownHallCharge();
            ResetIdleTimer(); // fresh inactivity window for her normal draw/play this same turn
            Debug.Log($"[TownHall] Tituba ({requestingPlayer.PlayerNameText}) rearranged the deck. " +
                      $"Charges remaining: {requestingPlayer.townHallAbilityCharges}");
        }

        /// <summary>
        /// Samuel Parris ability (twice per game): on his turn, draw UP TO 2 cards from the DISCARD PILE
        /// (no black cards) INSTEAD of drawing from the deck. Networked: the holder picks which cards via
        /// IPlayerInput.RequestCardPick (called up to twice, with a Done/decline option). TURN-ENDING —
        /// mirrors TryDrawFromDiscard's tail (consume charge + currentTurnAction=DrawTwoCards + EndTurn).
        /// The caller (NetworkInput) sets turnOver after this; it does NOT loop back like Tituba.
        /// </summary>
        public IEnumerator RunParrisDiscardPick(Player requestingPlayer)
        {
            if (!IsCurrentPlayersTurn(requestingPlayer)) yield break;
            if (currentTurnAction != TurnActionChoice.None) yield break;
            if (requestingPlayer == null ||
                !requestingPlayer.HasTownHall(Salem.Cards.TownhallName.SamuelParris) ||
                requestingPlayer.townHallAbilityCharges <= 0)
            {
                Debug.LogWarning("[TurnManager] Parris discard-pick requested without the ability or a charge — ignored.");
                yield break;
            }

            EnsureDeckManager();
            if (!deckManager) yield break;

            const int maxPicks = 2;
            const float pickTimeout = 45f;

            suppressIdleTimer = true; // pause inactivity; the pick has its own deadline

            // Filtered pool: the discard pile minus black cards (Night/Conspiracy). Mutable snapshot —
            // shrinks as cards are taken so a second pick can't offer an already-taken card.
            var pool = deckManager.GetDiscardPileCards()
                .Where(c => !Salem.Cards.Card.IsBlackCard(c)).ToList();

            int taken = 0;
            while (taken < maxPicks && pool.Count > 0)
            {
                int chosen = -1;
                yield return requestingPlayer.Input.RequestCardPick(
                    requestingPlayer, pool, taken + 1, maxPicks, pickTimeout,
                    allowDone: true, idx => chosen = idx);

                // -1 = explicit Done/decline OR timeout → stop ("up to 2" semantics: 0, 1, or 2).
                if (chosen < 0 || chosen >= pool.Count) break;

                var card = pool[chosen];
                if (!deckManager.TakeSpecificFromDiscard(card, requestingPlayer.HandManager)) break;
                pool.RemoveAt(chosen);
                taken++;
            }

            suppressIdleTimer = false;

            requestingPlayer.ConsumeTownHallCharge();
            currentTurnAction = TurnActionChoice.DrawTwoCards; // counts as the turn action (like Draw 2)
            if (requestingPlayer.IsHuman) waitingForHuman = false;

            Debug.Log($"[TownHall] Samuel Parris ({requestingPlayer.PlayerNameText}) discard-pick done — took " +
                      $"{taken}. Charges remaining: {requestingPlayer.townHallAbilityCharges}");

            EndTurn();
        }

        /// <summary>
        /// DEPRECATED — superseded by the networked <see cref="RunTitubaRearrange"/> flow.
        /// Retained only because a dead `_Archive` UI button references this signature; it no
        /// longer shuffles the deck or ends the turn (the old stub behavior was wrong: Tituba
        /// rearranges AND still takes her turn).
        /// </summary>
        public bool TryUseTitubaAbility(Player requestingPlayer)
        {
            Debug.LogWarning("[TurnManager] TryUseTitubaAbility is deprecated — Tituba now uses " +
                             "the networked turn action (RunTitubaRearrange).");
            return false;
        }

        public void NotifyCardPlayed(Player actingPlayer)
        {
            if (!IsCurrentPlayersTurn(actingPlayer))
            {
                return;
            }

            ResetIdleTimer(); // a play is activity — refresh the inactivity window

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
            drawFromDiscardButtonUI?.Hide();

            var players = PlayerService.GetAlivePlayers();
            if (players.Count == 0) return;

            Debug.Log($"Ending turn for {currentPlayer.PlayerNameText}");

            int nextIndex = (CurrentPlayerIndex + 1) % players.Count;

            StartTurn(nextIndex); // Move to the next player's turn
        }
        #endregion

        private IEnumerator RunTurn(Player current)
        {
             /*AIRCONSOLE DISABLED 4/28/26
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
            else if  then code below*/

            // AI runs its own sequencer; every other seat (local-UI or network)
            // routes through its IPlayerInput. Both drive the same turn API.
            if (current is AIPlayer ai)
            {
                yield return StartCoroutine(ai.TakeTurnOnce());
            }
            else if (current.Input != null)
            {
                yield return StartCoroutine(current.Input.RunTurn(current));
            }
            else
            {
                // No input source (degenerate config) — don't hang the loop.
                GameTurnManager.Instance.EndTurn();
            }
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

         /// <summary>
        /// Called when the idle timer expires. Forces the current player to draw
        /// two cards (applying Giles Corey if applicable) and ends their turn.
        /// </summary>
        private void ForceDrawAndEndTurn()
        {
            if (!isTurnActive || currentPlayer == null) return;

            EnsureDeckManager();
            if (deckManager != null)
            {
                int handSizeBefore = currentPlayer.HandManager.Hand.Count;
                deckManager.DrawMultipleCards(currentPlayer.HandManager, 2);
                currentTurnAction = TurnActionChoice.DrawTwoCards;

                // Giles Corey: if both drawn cards are Accusation cards, draw a third
                if (currentPlayer.HasTownHall(Salem.Cards.TownhallName.GilesCorey))
                {
                    var hand = currentPlayer.HandManager.Hand;
                    int newCards = hand.Count - handSizeBefore;
                    if (newCards >= 2)
                    {
                        var lastTwo = hand.Skip(handSizeBefore).Take(2).ToList();
                        bool bothAccusation = lastTwo.All(c => c is Salem.Cards.ActionCardSO ac && ac.Op == Salem.Cards.ActionOp.Accusation);
                        if (bothAccusation)
                        {
                            deckManager.DrawCard(currentPlayer.HandManager);
                            Debug.Log($"[TownHall] Giles Corey ({currentPlayer.PlayerNameText}) drew 2 Accusations — bonus 3rd card drawn.");
                        }
                    }
                }
            }

            waitingForHuman = false;
            EndTurn();
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

        private void UpdateTownHallActionButtons(Player player)
        {
            if (drawFromDiscardButtonUI == null)
                return;

            bool canUseSamuelParris =
                player != null &&
                player.IsHuman &&
                player.HasTownHall(Salem.Cards.TownhallName.SamuelParris) &&
                player.townHallAbilityCharges > 0 &&
                currentTurnAction == TurnActionChoice.None;

            if (canUseSamuelParris)
            {
                drawFromDiscardButtonUI.Show();
            }
            else
            {
                drawFromDiscardButtonUI.Hide();
            }
        }
    }
}