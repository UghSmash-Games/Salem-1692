/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Testing Table Layout Controller
*   Responsibilities:
*   Access Requirements:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/
using UnityEngine;
using System.Collections.Generic;

namespace Salem.UI
{
    public class TableLayoutTestHarness : MonoBehaviour
    {
        [SerializeField] private TableLayoutController layoutController;
        [SerializeField] private RectTransform playerBoardPrefab;
        [SerializeField] private RectTransform playerContainer;

        [Header("Automation")]
        [SerializeField] private bool spawnOnEnable = true;

        [Range(4, 12)]
        [SerializeField] private int playerCount = 6;

        [SerializeField] private string localPlayerId = "Player_0";

        [ContextMenu("Spawn Test Players")]
        public void SpawnTestPlayers()
        {
            if (layoutController == null || playerBoardPrefab == null || playerContainer == null)
            {
                Debug.LogWarning("TableLayoutTestHarness: Missing references. Assign layoutController, playerBoardPrefab, and playerContainer.");
                return;
            }

            // Clear old
            for (int i = playerContainer.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                {
                    Destroy(playerContainer.GetChild(i).gameObject);
                }
                else
                {
                    DestroyImmediate(playerContainer.GetChild(i).gameObject);
                }
            }

            var seats = new List<TableLayoutController.PlayerSeat>();

            for (int i = 0; i < playerCount; i++)
            {
                var board = Instantiate(playerBoardPrefab, playerContainer);
                board.name = $"Player_{i}";

                seats.Add(new TableLayoutController.PlayerSeat
                {
                    playerId = $"Player_{i}",
                    board = board
                });
            }

            layoutController.SetPlayers(seats, localPlayerId);
        }

        private void Reset()
        {
            if (layoutController == null)
            {
                layoutController = GetComponent<TableLayoutController>();
            }

            if (playerContainer == null)
            {
                playerContainer = GetComponent<RectTransform>();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || !spawnOnEnable)
            {
                return;
            }

            SpawnTestPlayers();
        }
    }
}