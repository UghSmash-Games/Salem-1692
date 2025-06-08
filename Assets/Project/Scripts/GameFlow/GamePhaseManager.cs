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
        public GamePhase CurrentPhase { get; private set; }
        public delegate void PhaseChangeHandler(GamePhase newPhase);
        public event PhaseChangeHandler OnPhaseChange;

        private GameManager GameManager;
        private GameSetup GameSetup;
        #endregion

        #region Standard Functions
        void Awake()
        {
            GameManager = GetComponent<GameManager>();
            GameSetup = GetComponent<GameSetup>();
            OnPhaseChange += HandlePhaseChange;
        }

        void Start()
        {
            ChangePhase(GamePhase.Setup);
        }
        #endregion

        #region Accessor Functions
        public void ChangePhase(GamePhase newPhase)
        {
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
            Player blackCatHolder = witches[Random.Range(0, witches.Count)];
            blackCatHolder.AssignBlackCat();

            // Transition to Day phase
            GamePhaseManager.ChangePhase(GamePhase.Day);
            */
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
            /*
            // Witches vote for elimination
            Player target = WitchesVote();

            // Constable assigns protection
            Player constable = FindConstable();
            Player protectedPlayer = constable?.AssignGavel();

            // Resolve elimination
            if (protectedPlayer != target)
            {
                EliminatePlayer(target);
            }

            // Shuffle discard pile back into deck and reset Night card
            ResetDeck();

            // Transition to Day phase
            GamePhaseManager.ChangePhase(GamePhase.Day);
            */
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
            GameSetup.SetupNewGame(GameManager.players, GameManager.players.Count);
            // Transition to Dawn phase
            ChangePhase(GamePhase.Dawn);
        }
        #endregion
    }
}