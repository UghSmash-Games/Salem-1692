/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
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

public class GameTurnManager : MonoBehaviour
{
    #region Vars
    public enum GamePhase { Dawn, Day, Night }
    public GamePhase CurrentPhase;
    public List<Player> Players;
    public UnityEvent OnTurnStart;
    public UnityEvent OnPhaseTransition;

    private int currentPlayerIndex = 0;
    #endregion


    #region Accessor Functions
    public void StartTurn()
    {
        Player currentPlayer = Players[currentPlayerIndex];
        Debug.Log($"Starting turn for {currentPlayer.PlayerName}");
        //currentPlayer.TakeTurn(); // Player performs their actions
    }

    public void EndTurn()
    {
        Debug.Log($"Ending turn for {Players[currentPlayerIndex].PlayerName}");
        currentPlayerIndex = (currentPlayerIndex + 1) % Players.Count;
        StartTurn(); // Move to the next player's turn
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

