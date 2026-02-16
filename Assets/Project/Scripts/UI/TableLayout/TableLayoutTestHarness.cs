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
        [SerializeField] private Transform playerContainer;

        [Range(4, 12)]
        [SerializeField] private int playerCount = 6;

        [SerializeField] private string localPlayerId = "Player_0";

        [ContextMenu("Spawn Test Players")]
        public void SpawnTestPlayers()
        {
            // Clear old
            for (int i = playerContainer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(playerContainer.GetChild(i).gameObject);
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
    }
}

