/*
* AUTHOR: Claude Code
* REFERENCES: AirConsole Unity Plugin API (NDream.AirConsole)
* NOTES:
*   Primary Purpose: Singleton bridge between the AirConsole SDK and Salem game systems.
*   Responsibilities:
*        • Initialize AirConsole and handle onReady/onConnect/onDisconnect/onMessage
*        • Map AirConsole device IDs to Player objects
*        • Send game state updates to phone controllers
*        • Route incoming controller input to AirConsoleInputHandler
*        • Set PlayerService.IsAirConsoleMode on init
*   Access Requirements:
*        • NDream.AirConsole (AirConsole Unity Plugin — must be imported separately)
*        • PlayerService
*        • GameTurnManager
*        • GamePhaseManager
*        • AirConsoleInputHandler

* TODO: Add reconnection handling for dropped connections
* FIXME: [Known bugs or issues]
*/
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Salem.Cards;
using Salem.Data;
using Salem.GameFlow;
using Salem.Players;
using NDream.AirConsole;
using Newtonsoft.Json.Linq;

namespace Salem.AirConsole
{
    public class AirConsoleManager : MonoBehaviour
    {
        #region Vars
        public static AirConsoleManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int maxPlayers = 6;

        private readonly Dictionary<int, Player> deviceToPlayer = new();
        private readonly Dictionary<Player, int> playerToDevice = new();
        private AirConsoleInputHandler inputHandler;
        private int connectedControllerCount;
        #endregion

        #region Standard Functions
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            PlayerService.IsAirConsoleMode = true;
            inputHandler = GetComponent<AirConsoleInputHandler>();
            if (inputHandler == null)
            {
                inputHandler = gameObject.AddComponent<AirConsoleInputHandler>();
            }
        }

        private void OnEnable()
        {
            AirConsole.instance.onReady += OnReady;
            AirConsole.instance.onConnect += OnConnect;
            AirConsole.instance.onDisconnect += OnDisconnect;
            AirConsole.instance.onMessage += OnMessage;
        }

        private void OnDisable()
        {
            if (AirConsole.instance != null)
            {
                AirConsole.instance.onReady -= OnReady;
                AirConsole.instance.onConnect -= OnConnect;
                AirConsole.instance.onDisconnect -= OnDisconnect;
                AirConsole.instance.onMessage -= OnMessage;
            }
        }
        #endregion

        #region AirConsole Callbacks
        private void OnReady(string code)
        {
            Debug.Log($"[AirConsoleManager] AirConsole ready. Game code: {code}");
        }

        private void OnConnect(int deviceId)
        {
            Debug.Log($"[AirConsoleManager] Device {deviceId} connected.");
            connectedControllerCount++;

            // Try to assign this device to an unassigned human player
            AssignDeviceToPlayer(deviceId);
        }

        private void OnDisconnect(int deviceId)
        {
            Debug.Log($"[AirConsoleManager] Device {deviceId} disconnected.");
            connectedControllerCount--;

            if (deviceToPlayer.TryGetValue(deviceId, out Player player))
            {
                Debug.Log($"[AirConsoleManager] Player {player.PlayerNameText} disconnected.");
                deviceToPlayer.Remove(deviceId);
                playerToDevice.Remove(player);

                if (!player.IsEliminated)
                {
                    PlayerService.Eliminate(player, EliminationCause.Disconnect);
                }
            }
        }

        private void OnMessage(int from, JToken data)
        {
            if (!deviceToPlayer.TryGetValue(from, out Player player))
            {
                Debug.LogWarning($"[AirConsoleManager] Message from unassigned device {from}.");
                return;
            }

            string action = data["action"]?.ToString();
            if (string.IsNullOrEmpty(action))
            {
                Debug.LogWarning($"[AirConsoleManager] Message from device {from} has no action field.");
                return;
            }

            inputHandler.ProcessMessage(player, action, data);
        }
        #endregion

        #region Device-Player Mapping
        private void AssignDeviceToPlayer(int deviceId)
        {
            // Find the first human player not yet assigned to a device
            var unassigned = PlayerService.All
                .Where(p => p.IsHuman && !p.IsEliminated && !(p is AIPlayer) && !playerToDevice.ContainsKey(p))
                .FirstOrDefault();

            if (unassigned == null)
            {
                Debug.LogWarning($"[AirConsoleManager] No unassigned human player for device {deviceId}.");
                return;
            }

            deviceToPlayer[deviceId] = unassigned;
            playerToDevice[unassigned] = deviceId;

            Debug.Log($"[AirConsoleManager] Assigned device {deviceId} → {unassigned.PlayerNameText}");

            // Send initial hand to the newly connected controller
            SendHandUpdate(unassigned);
        }

        public Player GetPlayerForDevice(int deviceId)
        {
            deviceToPlayer.TryGetValue(deviceId, out Player player);
            return player;
        }

        public int GetDeviceForPlayer(Player player)
        {
            return playerToDevice.TryGetValue(player, out int deviceId) ? deviceId : -1;
        }

        public bool IsPlayerAssigned(Player player)
        {
            return playerToDevice.ContainsKey(player);
        }
        #endregion

        #region Send Messages to Controllers
        public void SendHandUpdate(Player player)
        {
            if (!playerToDevice.TryGetValue(player, out int deviceId)) return;

            var msg = new HandUpdateMessage();
            msg.cards = new List<CardInfo>();

            var hand = player.HandManager.Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                var ac = card as ActionCardSO;
                msg.cards.Add(new CardInfo
                {
                    index = i,
                    name = card.Name,
                    type = card.Type.ToString(),
                    needsTarget = ac != null && ac.NeedsTarget
                });
            }

            string json = JsonUtility.ToJson(msg);
            AirConsole.instance.Message(deviceId, JToken.Parse(json));
        }

        public void SendTurnNotify(Player player, bool isYourTurn)
        {
            if (!playerToDevice.TryGetValue(player, out int deviceId)) return;

            var msg = new TurnNotifyMessage
            {
                isYourTurn = isYourTurn,
                canDraw = isYourTurn,
                canPlay = isYourTurn
            };

            string json = JsonUtility.ToJson(msg);
            AirConsole.instance.Message(deviceId, JToken.Parse(json));
        }

        public void SendTargetRequest(Player player, List<Player> validTargets, bool needsSecondTarget)
        {
            if (!playerToDevice.TryGetValue(player, out int deviceId)) return;

            var msg = new RequestTargetMessage
            {
                needsSecondTarget = needsSecondTarget,
                validTargets = new List<TargetInfo>()
            };

            for (int i = 0; i < validTargets.Count; i++)
            {
                msg.validTargets.Add(new TargetInfo
                {
                    index = i,
                    playerName = validTargets[i].PlayerNameText
                });
            }

            string json = JsonUtility.ToJson(msg);
            AirConsole.instance.Message(deviceId, JToken.Parse(json));
        }

        public void SendEliminated(Player player, string reason)
        {
            if (!playerToDevice.TryGetValue(player, out int deviceId)) return;

            var msg = new EliminatedMessage { reason = reason };

            string json = JsonUtility.ToJson(msg);
            AirConsole.instance.Message(deviceId, JToken.Parse(json));
        }

        public void SendGamePhaseToAll(string phase, string currentPlayerName)
        {
            var msg = new GamePhaseMessage
            {
                phase = phase,
                currentPlayerName = currentPlayerName
            };

            string json = JsonUtility.ToJson(msg);
            var token = JToken.Parse(json);

            foreach (var deviceId in deviceToPlayer.Keys)
            {
                AirConsole.instance.Message(deviceId, token);
            }
        }

        /// <summary>
        /// Sends hand updates to all connected players. Call after any game state change
        /// that might affect hands (card played, drawn, etc.).
        /// </summary>
        public void BroadcastHandUpdates()
        {
            foreach (var kvp in playerToDevice)
            {
                SendHandUpdate(kvp.Key);
            }
        }
        #endregion
    }
}
