/*
* AUTHOR:
* REFERENCES:
* NOTES:
*   Primary Purpose: Central controller for displaying UI events and panels.
*   Responsibilities:
*        • Route UI events
*        • Control HUD and feedback
*   Access Requirements:
*        • All UI scripts
*        • GameStateManager

* TODO:
*    • Broadcast turn/phase changes to listeners

* FIXME: [Known bugs or issues]
*/
using UnityEngine;
using Salem.Managers.GameState;
using Salem.Players;

namespace Salem.UI
{
    public class UIManager : MonoBehaviour
    {
        private PlayerInputUI localPlayerInputUI;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public void SetupLocalPlayerUI(Player localPlayer)
        {
            if (localPlayer == null)
            {
                Debug.LogWarning("UIManager: No local player assigned.");
                return;
            }

            localPlayerInputUI = localPlayer.InputUI;
            localPlayerInputUI.Initialize(localPlayer);

        }
    }
}