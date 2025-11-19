/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/
using Salem.GameFlow;
using UnityEngine;

public class DebugManager : MonoBehaviour
{
    [SerializeField] private GameTurnManager GameTurnManager;
    [SerializeField] private GamePhaseManager GamePhaseManager;

    // Update is called once per frame
    void Update()
    {
        //Debug Key for Manual Skipping
        if (Input.GetKeyDown(GameTurnManager.debugTurnAdvanceKey))
        {
            Debug.Log("Debug Key Pressed (N). Ending Turn.");
            if (GameTurnManager.Instance.CurrentPlayer.IsHuman)
            {
                // Option A: treat N as 'End Turn' for human, not AI autoplay
                GameTurnManager.Instance.RequestEndTurn(GameTurnManager.Instance.CurrentPlayer);
            }
            else
            {
                // Option B: advance AI turn normally for bots
                GameTurnManager.Instance.EndTurn();
            }
        }

        if (Input.GetKeyDown(GamePhaseManager.DebugAdvancePhaseKey))
        {
            Debug.Log("Debug: Advancing game phase...");
            GamePhaseManager.DebugChangePhase();
        }
    }
}
