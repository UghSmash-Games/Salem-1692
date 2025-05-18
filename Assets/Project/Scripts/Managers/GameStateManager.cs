/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Tracks global game state (Running, Paused, Ended).
*   Responsibilities:
*        • Gate input/actions by game state
*        • Broadcast OnStateChanged
*        • Manage victory/defeat triggers
*   Access Requirements:
*        • UIManager
*        • GameManager

* TODO: [Planned improvements]
 * FIXME: [Known bugs or issues]
*/
using UnityEngine;
using Salem.UI;
using Salem.GameFlow;

namespace Salem.Managers.GameState
{
    public class GameStateManager : MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            
        }

        // Update is called once per frame
        void Update()
        {
            
        }
    }
}