/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Manages global game phases (e.g., Setup, Dawn, Night).
*   Responsibilities:
*        • Trigger phase transitions
*        • Notify systems of current phase
*        • Block/enable actions based on phase
*   Access Requirements:
*    • GameStateManager
*    • GameTurnManager

* TODO: []
 * FIXME: [Known bugs or issues]
*/
using System.Collections.Generic;
using UnityEngine;
using Salem.Managers.GameState;
using Salem.Gameplay.Setup;
using Salem.Players;
using Salem.UI;
using Salem.Data;
using System.Collections;

namespace Salem.GameFlow
{
    public enum GamePhase
    {
        Setup,
        Dawn,
        Day,
        Conspiracy,
        Night,
        EndGame
    }

    public class GamePhaseManager : MonoBehaviour
    {
        #region Vars
        [SerializeField] private float PhaseChangeDelay = 0.5f;
        public GamePhase CurrentPhase { get; private set; }
        public delegate void PhaseChangeHandler(GamePhase newPhase);
        public event PhaseChangeHandler OnPhaseChange;
        public KeyCode DebugAdvancePhaseKey = KeyCode.P;

        private GameSetup GameSetup;
        private GameTurnManager GameTurnManager;
        #endregion

        #region Standard Functions
        void Awake()
        {
            GameSetup = GetComponent<GameSetup>();
            GameTurnManager = GetComponent<GameTurnManager>();
            OnPhaseChange += HandlePhaseChange;
        }

        void Start()
        {
            StartCoroutine(ChangePhase(GamePhase.Setup, PhaseChangeDelay));

        }
        #endregion

        #region Accessor Functions
        public IEnumerator ChangePhase(GamePhase newPhase, float delay)
        {
            //Debug.Log($"[GamePhaseManager] Changing phase to {newPhase} in {delay} seconds...");
            yield return new WaitForSeconds(delay);

            CurrentPhase = newPhase;
            OnPhaseChange?.Invoke(newPhase);
            Debug.Log($"Game Phase Changed to: {newPhase}");
        }

        public void StartDawnPhase()
        {
            /*
            // Reveal Witches to each other
            List<Player> witches = players.Where(p => p.HasRole(TryalCardType.Witch)).ToList();
            witches.ForEach(w => w.RevealWitchGroup(witches));

            // Assign the Black Cat
            Player blackCatHolder = witches[rng.NextInt(0, witches.Count)];
            blackCatHolder.AssignBlackCat();

            */
            //Transition to Day phase
            StartCoroutine(ChangePhase(GamePhase.Day,PhaseChangeDelay));
        }

        public void StartDayPhase()
        {
            /*
            foreach (Player player in players)
            {
                player.TakeTurn();

                // Check for Conspiracy or Night card
                if (player.DrewCard(CardType.Conspiracy))
                {
                    GamePhaseManager.ChangePhase(GamePhase.Conspiracy);
                    break;
                }
                else if (player.DrewCard(CardType.Night))
                {
                    GamePhaseManager.ChangePhase(GamePhase.Night);
                    break;
                }
            }
            */
        }

        public void StartConspiracyPhase()
        {
            /*
            Player blackCatHolder = FindBlackCatHolder();
            blackCatHolder.RevealTryalCard();

            // Pass Tryal cards to the left
            foreach (Player player in players)
            {
                player.PassTryalCardToLeft();
            }

            // Transition back to Day phase
            GamePhaseManager.ChangePhase(GamePhase.Day);
            */
        }

        public void StartNightPhase()
        {
            NightResolver.Resolve(GameManager.Instance.Rng /*, witchesCanTargetWitches:false */);
            GameManager.Instance.CheckEndgameConditions();
        }

        public void StartEndGamePhase()
        {
            /*
            if (AreAllWitchesEliminated())
            {
                DisplayVictoryScreen("Villagers Win!");
            }
            else if (AreAllVillagersEliminated())
            {
                DisplayVictoryScreen("Witches Win!");
            }
            */
        }

        public void DebugChangePhase()
        {
            switch (CurrentPhase)
            {
                case GamePhase.Setup:
                    StartCoroutine(ChangePhase(GamePhase.Dawn,PhaseChangeDelay));
                    break;
                case GamePhase.Dawn:
                    StartCoroutine(ChangePhase(GamePhase.Day,PhaseChangeDelay));
                    break;
                case GamePhase.Day:
                    StartCoroutine(ChangePhase(GamePhase.Conspiracy,PhaseChangeDelay));
                    break;
                case GamePhase.Conspiracy:
                    StartCoroutine(ChangePhase(GamePhase.Night,PhaseChangeDelay));
                    break;
                case GamePhase.Night:
                    StartCoroutine(ChangePhase(GamePhase.Day,PhaseChangeDelay));
                    break;
            }
        }
        #endregion

        #region Helper Fucntions
        private void HandlePhaseChange(GamePhase newPhase)
        {
            if (newPhase == GamePhase.Setup)
            {
                StartSetupPhase();
            }
        }

        private void StartSetupPhase()
        {
            GameSetup.SetupNewGame(PlayerService.All, PlayerService.All.Count);
            GameTurnManager.Initialize();
            // Transition to Dawn phase
            StartCoroutine(ChangePhase(GamePhase.Dawn,PhaseChangeDelay));
        }
        #endregion
    }
}