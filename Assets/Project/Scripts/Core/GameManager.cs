/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
 * FIXME: [Known bugs or issues]
*/
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
   #region Vars
     //Tracks GameManager
     public static object Instance { get; internal set; }
     public List<Player> players;
 
     private PlayerHandUI PlayerHandUI;
     private EndGameUI EndGameUI;
     private bool isGameActive;
   #endregion

    #region Standard Functions
    void Awake()
    {
        HandleInstance();
        SetReferences();
        PopulatePlayers();
        isGameActive = true;
    }

    void Start()
    {
        UpdateUI();
        PlayerHandUI.PopulateTryalCards(players[0]);
    }
    #endregion
    
    #region Accessor Functions
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

    //Sets necessary references
    private void SetReferences()
    {
        EndGameUI = (EndGameUI)FindFirstObjectByType(typeof(EndGameUI));
        PlayerHandUI = (PlayerHandUI)FindFirstObjectByType(typeof(PlayerHandUI));
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

    //Updates CardsUI
    private void UpdateUI()
    {
        //Update local player's hand
        PlayerHandUI.UpdateHand(players[0].HandManager.Hand);
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
