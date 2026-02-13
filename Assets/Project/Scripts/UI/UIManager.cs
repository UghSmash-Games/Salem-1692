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
using Salem.GameFlow;
using Salem.Data;
using System.Collections.Generic;
using UnityEngine.UI;

namespace Salem.UI
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private List<PlayerStatusUI> statusPanels;
        [SerializeField] private PlayerInputUI localPlayerInputUI;

        [Header("Phase Indicator")]
        [SerializeField] private Image phaseIconImage;
        [SerializeField] private Sprite placeholderPhaseSprite;
        [SerializeField] private Sprite dawnPhaseSprite;
        [SerializeField] private Sprite dayPhaseSprite;
        [SerializeField] private Sprite nightPhaseSprite;

        private GamePhaseManager GamePhaseManager;
        private Dictionary<Player, PlayerStatusUI> playerToStatusUI = new();

        private void Awake()
        {
            if (phaseIconImage == null)
            {
                var indicatorObject = GameObject.Find("Day_NightIndicator");
                if (indicatorObject != null)
                {
                    phaseIconImage = indicatorObject.GetComponent<Image>();
                }
            }
        }

        private void OnEnable()
        {
            GamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
            if (GamePhaseManager != null)
            {
                GamePhaseManager.OnPhaseChange += HandlePhaseIconChanged;
                HandlePhaseIconChanged(GamePhaseManager.CurrentPhase);
            }
            else
            {
                ApplyPhaseSprite(placeholderPhaseSprite);
            }
        }

        private void OnDisable()
        {
            if (GamePhaseManager != null)
            {
                GamePhaseManager.OnPhaseChange -= HandlePhaseIconChanged;
            }
        }

        public void BindAllPlayerStatusUI()
        {
            var players = PlayerService.All;

            for (int i = 0; i < statusPanels.Count; i++)
            {
                if (i < players.Count)
                {
                    statusPanels[i].gameObject.SetActive(true);
                    BindStatusUI(players[i], statusPanels[i]);
                }
                else
                {
                    statusPanels[i].gameObject.SetActive(false);
                }
            }
        }
        private void BindStatusUI(Player player, PlayerStatusUI statusUI)
        {
            // Link the UI to this player's updates
            player.OnStatusCardsChanged += () =>
            {
                statusUI.UpdateStatusCards(player.StatusCards);
            };
            player.OnTryalCardsChanged += statusUI.UpdateTryalCards;
            statusUI.Initialize(player);

            // Optional: initialize immediately
            statusUI.UpdateStatusCards(player.StatusCards);
            statusUI.UpdateTryalCards();

            playerToStatusUI[player] = statusUI;
            SetPlayerName(player, statusUI);
        }

        public void SetupLocalPlayerUI(Player localPlayer)
        {
            if (localPlayer == null)
            {
                Debug.LogWarning("UIManager: No local player assigned.");
                return;
            }

            localPlayerInputUI.Initialize(localPlayer);

        }

        public void SetPlayerTurnActive()
        {
            foreach (var ui in playerToStatusUI.Values)
            {
                ui.SetTurnActive(false);
            }

            var players = PlayerService.GetAlivePlayers();
            if (GameTurnManager.CurrentPlayerIndex >= players.Count)
            {
                Debug.LogWarning("UIManager: CurrentPlayerIndex out of range.");
                return;
            }

            var currentPlayer = players[GameTurnManager.CurrentPlayerIndex];

            if (playerToStatusUI.TryGetValue(currentPlayer, out var statusUI))
            {
                statusUI.SetTurnActive(true);
            }
            else
            {
                Debug.LogWarning("UIManager: No StatusUI found for current player.");
            }
        }

        private void SetPlayerName(Player player, PlayerStatusUI statusUI)
        {
            statusUI.PlayerName.text = player.PlayerNameText;
        }

        private void HandlePhaseIconChanged(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Dawn:
                    ApplyPhaseSprite(dawnPhaseSprite);
                    break;

                case GamePhase.Day:
                    ApplyPhaseSprite(dayPhaseSprite);
                    break;

                case GamePhase.Night:
                    ApplyPhaseSprite(nightPhaseSprite);
                    break;

                default:
                    ApplyPhaseSprite(placeholderPhaseSprite);
                    break;
            }
        }

        private void ApplyPhaseSprite(Sprite sprite)
        {
            if (phaseIconImage == null)
            {
                return;
            }

            var resolvedSprite = sprite != null ? sprite : placeholderPhaseSprite;
            if (resolvedSprite == null)
            {
                return;
            }

            phaseIconImage.enabled = true;
            phaseIconImage.sprite = resolvedSprite;
        }
    }
}