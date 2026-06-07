using System;
using UnityEngine;

namespace Salem.Networking
{
    /// <summary>
    /// Singleton MonoBehaviour that bridges the SocketIOClient to game systems.
    /// Game systems subscribe to C# events; NetworkManager handles serialization,
    /// connection lifecycle, and reconnection.
    ///
    /// This is a relay bridge ΓÇö it does NOT drive GamePhaseManager or GameTurnManager.
    /// Game systems subscribe to events and decide what to do.
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        // ΓöÇΓöÇΓöÇ Singleton ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        public static NetworkManager Instance { get; private set; }

        // ΓöÇΓöÇΓöÇ Inspector Fields ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        [Header("Server")]
        [SerializeField] private string serverUrl = "ws://localhost:3000";
        [SerializeField] private int maxReconnectAttempts = 5;
        [SerializeField] private float baseReconnectDelay = 1.0f;

        // ΓöÇΓöÇΓöÇ Public State ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        public bool IsConnected => socketClient != null && socketClient.IsConnected;
        public string RoomCode { get; private set; }

        // ΓöÇΓöÇΓöÇ Inbound Events (game systems subscribe to these) ΓöÇΓöÇΓöÇΓöÇ

        public event Action<string> OnRoomCreated;                    // room code
        public event Action<string, string> OnPlayerJoined;           // playerId, displayName
        public event Action<string> OnPlayerLeft;                     // playerId
        public event Action<PlayerActionMsg> OnPlayerAction;
        public event Action<SecretPhaseSubmitMsg> OnSecretPhaseSubmit;
        public event Action<ConfessMsg> OnConfess;
        public event Action<PhaseResolveMsg> OnPhaseResolveEcho;
        public event Action OnRoomClosed;
        public event Action OnConnectedToServer;
        public event Action OnDisconnectedFromServer;

        // ΓöÇΓöÇΓöÇ Private State ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        private SocketIOClient socketClient;
        private int reconnectAttempts;
        private float reconnectTimer;
        private bool reconnecting;

        // ΓöÇΓöÇΓöÇ Unity Lifecycle ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (socketClient == null) return;

            // Dispatch queued WebSocket messages on the main thread
#if !UNITY_WEBGL || UNITY_EDITOR
            socketClient.DispatchMessageQueue();
#endif

            // Check for ping timeout ΓåÆ trigger reconnect
            if (socketClient.IsConnected &&
                socketClient.TimeSinceLastPing > socketClient.PingTimeoutThreshold)
            {
                Debug.LogWarning("[NetworkManager] Ping timeout ΓÇö connection may be stale. Reconnecting...");
                HandleDisconnect("ping_timeout");
            }

            // Manage reconnect timer
            if (reconnecting)
            {
                reconnectTimer -= Time.unscaledDeltaTime;
                if (reconnectTimer <= 0f)
                {
                    AttemptReconnect();
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            DisconnectImmediate();
        }

        private void OnApplicationQuit()
        {
            DisconnectImmediate();
        }

        // ΓöÇΓöÇΓöÇ Public API ΓÇö Connection ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        /// <summary>
        /// Connect to the server. Call this before CreateRoom or any other server interaction.
        /// </summary>
        public async void ConnectToServer()
        {
            if (socketClient != null && socketClient.IsConnected)
            {
                Debug.LogWarning("[NetworkManager] Already connected.");
                return;
            }

            socketClient = new SocketIOClient();
            RegisterHandlers();

            socketClient.OnConnected += () =>
            {
                Debug.Log("[NetworkManager] Connected to server.");
                reconnectAttempts = 0;
                reconnecting = false;
                OnConnectedToServer?.Invoke();
            };

            socketClient.OnDisconnected += (reason) =>
            {
                Debug.Log($"[NetworkManager] Disconnected: {reason}");
                HandleDisconnect(reason);
            };

            try
            {
                await socketClient.Connect(serverUrl);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] Connection failed: {e.Message}");
                HandleDisconnect("connection_failed");
            }
        }

        // ΓöÇΓöÇΓöÇ Public API ΓÇö Outbound Messages ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        public void CreateRoom()
        {
            if (!GuardConnected("CreateRoom")) return;
            _ = socketClient.Emit("create_room");
        }

        public void SendGameStateUpdate(GameStateUpdateMsg msg)
        {
            if (!GuardConnected("SendGameStateUpdate")) return;
            _ = socketClient.Emit("game_state_update", JsonUtility.ToJson(msg));
        }

        public void SendPrivateState(PrivateStateMsg msg)
        {
            if (!GuardConnected("SendPrivateState")) return;
            _ = socketClient.Emit("private_state", JsonUtility.ToJson(msg));
        }

        public void SendSecretPhasePrompt(SecretPhasePromptMsg msg)
        {
            if (!GuardConnected("SendSecretPhasePrompt")) return;
            _ = socketClient.Emit("secret_phase_prompt", JsonUtility.ToJson(msg));
        }

        public void SendActionRequest(ActionRequestMsg msg)
        {
            if (!GuardConnected("SendActionRequest")) return;
            _ = socketClient.Emit("action_request", JsonUtility.ToJson(msg));
        }

        public void SendPhaseResolve(PhaseResolveMsg msg)
        {
            if (!GuardConnected("SendPhaseResolve")) return;
            _ = socketClient.Emit("phase_resolve", JsonUtility.ToJson(msg));
        }

        public void SendEliminationResult(EliminationResultMsg msg)
        {
            if (!GuardConnected("SendEliminationResult")) return;
            _ = socketClient.Emit("elimination_result", JsonUtility.ToJson(msg));
        }

        public void SendGameOver(GameOverMsg msg)
        {
            if (!GuardConnected("SendGameOver")) return;
            _ = socketClient.Emit("game_over", JsonUtility.ToJson(msg));
        }

        // ΓöÇΓöÇΓöÇ Handler Registration ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        private void RegisterHandlers()
        {
            socketClient.On("room_created", json =>
            {
                var msg = JsonUtility.FromJson<RoomCreatedMsg>(json);
                RoomCode = msg.code;
                Debug.Log($"[NetworkManager] Room created: {msg.code}");
                OnRoomCreated?.Invoke(msg.code);
            });

            socketClient.On("player_joined", json =>
            {
                var msg = JsonUtility.FromJson<PlayerJoinedMsg>(json);
                Debug.Log($"[NetworkManager] Player joined: {msg.playerId} ({msg.displayName})");
                OnPlayerJoined?.Invoke(msg.playerId, msg.displayName);
            });

            socketClient.On("player_left", json =>
            {
                var msg = JsonUtility.FromJson<PlayerLeftMsg>(json);
                Debug.Log($"[NetworkManager] Player left: {msg.playerId}");
                OnPlayerLeft?.Invoke(msg.playerId);
            });

            socketClient.On("player_action", json =>
            {
                var msg = JsonUtility.FromJson<PlayerActionMsg>(json);
                Debug.Log($"[NetworkManager] Player action from {msg.playerId}: {msg.card} ΓåÆ {msg.targetPlayerId}");
                OnPlayerAction?.Invoke(msg);
            });

            socketClient.On("secret_phase_submit", json =>
            {
                var msg = JsonUtility.FromJson<SecretPhaseSubmitMsg>(json);
                Debug.Log($"[NetworkManager] Secret phase submit from {msg.playerId}: {msg.selection}");
                OnSecretPhaseSubmit?.Invoke(msg);
            });

            socketClient.On("confess", json =>
            {
                var msg = JsonUtility.FromJson<ConfessMsg>(json);
                Debug.Log($"[NetworkManager] Confess from {msg.playerId}: tryal index {msg.tryalIndex}");
                OnConfess?.Invoke(msg);
            });

            socketClient.On("phase_resolve", json =>
            {
                var msg = JsonUtility.FromJson<PhaseResolveMsg>(json);
                Debug.Log($"[NetworkManager] Phase resolve echo: revealAt={msg.revealAt}");
                OnPhaseResolveEcho?.Invoke(msg);
            });

            socketClient.On("room_closed", json =>
            {
                Debug.Log("[NetworkManager] Room closed by server.");
                RoomCode = null;
                OnRoomClosed?.Invoke();
            });

            socketClient.On("mirror_joined", json =>
            {
                Debug.Log("[NetworkManager] Mirror screen connected.");
            });

            socketClient.On("error_msg", json =>
            {
                Debug.LogWarning($"[NetworkManager] Server error: {json}");
            });
        }

        // ΓöÇΓöÇΓöÇ Reconnection ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        private void HandleDisconnect(string reason)
        {
            if (reconnecting) return;

            OnDisconnectedFromServer?.Invoke();

            if (reconnectAttempts >= maxReconnectAttempts)
            {
                Debug.LogError($"[NetworkManager] Max reconnect attempts ({maxReconnectAttempts}) reached. Giving up.");
                reconnecting = false;
                return;
            }

            reconnecting = true;
            float delay = baseReconnectDelay * Mathf.Pow(2, reconnectAttempts);
            reconnectTimer = delay;
            reconnectAttempts++;

            Debug.Log($"[NetworkManager] Will attempt reconnect #{reconnectAttempts} in {delay:F1}s");
        }

        private async void AttemptReconnect()
        {
            reconnecting = false;
            Debug.Log($"[NetworkManager] Reconnect attempt #{reconnectAttempts}...");

            // Tear down old client
            DisconnectImmediate();

            // Create fresh client and connect
            socketClient = new SocketIOClient();
            RegisterHandlers();

            socketClient.OnConnected += () =>
            {
                Debug.Log("[NetworkManager] Reconnected to server.");
                reconnectAttempts = 0;
                reconnecting = false;
                OnConnectedToServer?.Invoke();

                // Note: after reconnect, the room/player state on the server is gone.
                // Game systems listening to OnConnectedToServer should handle re-creation
                // of the room if needed.
            };

            socketClient.OnDisconnected += (r) =>
            {
                Debug.Log($"[NetworkManager] Disconnected after reconnect attempt: {r}");
                HandleDisconnect(r);
            };

            try
            {
                await socketClient.Connect(serverUrl);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] Reconnect failed: {e.Message}");
                HandleDisconnect("reconnect_failed");
            }
        }

        // ΓöÇΓöÇΓöÇ Helpers ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        private bool GuardConnected(string methodName)
        {
            if (IsConnected) return true;
            Debug.LogWarning($"[NetworkManager] {methodName} called but not connected to server.");
            return false;
        }

        private void DisconnectImmediate()
        {
            if (socketClient == null) return;

            try
            {
                _ = socketClient.Disconnect();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NetworkManager] Error during disconnect: {e.Message}");
            }

            socketClient = null;
        }
    }
}
