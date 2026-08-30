using System;
using System.Collections.Generic;
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
        [SerializeField, Tooltip("Fallback server URL. A -server argument or SALEM_SERVER_URL " +
                                 "environment variable overrides this, so a BUILT host can be " +
                                 "pointed at a different relay without rebuilding.")]
        private string serverUrl = "ws://localhost:3000";
        [SerializeField] private int maxReconnectAttempts = 5;
        [SerializeField] private float baseReconnectDelay = 1.0f;

        // ΓöÇΓöÇΓöÇ Public State ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        /// <summary>
        /// The relay URL actually used, resolved once.
        ///
        /// ⚠ THE SERIALIZED FIELD IS ONLY A FALLBACK. It is baked into the scene at build time, so a
        /// standalone host would otherwise be permanently pointed at whatever URL happened to be in
        /// the Inspector the day it was built — and the fix would be a rebuild. A deployed relay can
        /// move, and dev/prod are the same binary.
        ///
        /// Precedence, most specific first:
        ///   1. `-server wss://host` on the command line (how you launch a built host)
        ///   2. the SALEM_SERVER_URL environment variable
        ///   3. the Inspector value
        /// </summary>
        private string ResolveServerUrl()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-server" && !string.IsNullOrWhiteSpace(args[i + 1]))
                    return args[i + 1].Trim();
            }

            var fromEnv = Environment.GetEnvironmentVariable("SALEM_SERVER_URL");
            if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv.Trim();

            return serverUrl;
        }

        /// <summary>
        /// Resolve the URL and warn about the one misconfiguration that fails confusingly: a plaintext
        /// ws:// pointed at a remote host. Deployed relays terminate TLS (Fly's force_https), so ws://
        /// is refused or redirected and the error surfaces as a bare connection failure with nothing
        /// naming the scheme as the cause. Local development over ws:// is normal and not warned about.
        /// </summary>
        private string ServerUrlForConnect()
        {
            var url = ResolveServerUrl();

            bool isLocal = url.Contains("localhost") || url.Contains("127.0.0.1") ||
                           url.Contains("192.168.") || url.Contains("10.0.");
            if (url.StartsWith("ws://") && !isLocal)
            {
                Debug.LogWarning($"[NetworkManager] {url} is plaintext ws:// but not local. A deployed " +
                                 "relay serves TLS — use wss:// or the connection will simply fail.");
            }

            return url;
        }

        public bool IsConnected => socketClient != null && socketClient.IsConnected;
        public string RoomCode { get; private set; }

        // ΓöÇΓöÇΓöÇ Inbound Events (game systems subscribe to these) ΓöÇΓöÇΓöÇΓöÇ

        public event Action<string> OnRoomCreated;                    // room code
        public event Action<string, string> OnPlayerJoined;           // playerId, displayName
        public event Action<string> OnPlayerLeft;                     // playerId
        public event Action<string, string> OnPlayerRejoined;         // playerId, displayName
        public event Action<PlayerActionMsg> OnPlayerAction;
        public event Action<SecretPhaseSubmitMsg> OnSecretPhaseSubmit;
        public event Action<ConfessMsg> OnConfess;
        public event Action<DeckRearrangeSubmitMsg> OnDeckRearrangeSubmit;
        public event Action<CardPickSubmitMsg> OnCardPickSubmit;
        public event Action<ConfirmSubmitMsg> OnConfirmSubmit;
        public event Action<TargetSubmitMsg> OnTargetSubmit;
        public event Action<TryalPickSubmitMsg> OnTryalPickSubmit;
        public event Action<PhaseResolveMsg> OnPhaseResolveEcho;
        public event Action OnRoomClosed;

        /// <summary>
        /// A mirror screen just connected. The host answers with a full re-broadcast so the mirror
        /// syncs to the CURRENT state instead of waiting for the next incidental event.
        ///
        /// Necessary because broadcasts are event-driven (turn / phase / elimination / card played /
        /// tryal revealed) with no periodic tick — so a mirror that connects during the LOBBY, or
        /// during any quiet stretch, receives nothing at all and renders an empty board. Keeping the
        /// re-broadcast on the HOST rather than caching state server-side preserves the server as a
        /// pure relay: it never stores or originates game state.
        /// </summary>
        public event Action OnMirrorJoined;
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

            // Keep Update() (and thus the socket message pump) running when the
            // Editor/standalone window loses focus, so Engine.io pings are answered
            // and the server doesn't idle-close the connection.
            Application.runInBackground = true;
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
                await socketClient.Connect(ServerUrlForConnect());
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] Connection failed: {e.Message}");
                HandleDisconnect("connection_failed");
            }
        }

        // ─── Pending prompt replay (reconnection) ─────────────────

        /// <summary>
        /// The last per-player prompt still awaiting an answer, kept so a phone that dropped can be
        /// handed it again when it returns.
        ///
        /// 🔴 WHY THIS IS NEEDED. Every per-player request is a one-shot emit to one socket. A phone
        /// that locks its screen mid-prompt comes back with an empty store and no idea it is holding
        /// the game up — and the game genuinely IS held up: the host waits on that answer until the
        /// phase times out. Re-sending is what turns a dropped connection into a pause instead of a
        /// lost turn.
        ///
        /// Entries are written by the Send*Request methods and cleared by NetworkInput when the
        /// matching wait ends, so a replay can only ever re-issue a prompt that is still open. A
        /// stale replay would be worse than none: the phone would show a prompt whose handler is
        /// already gone, and nothing it sent would be heard.
        /// </summary>
        private readonly Dictionary<string, PendingPrompt> pendingPrompts = new();

        private readonly struct PendingPrompt
        {
            public readonly string Event;
            public readonly string Json;
            public PendingPrompt(string evt, string json) { Event = evt; Json = json; }
        }

        private void RememberPrompt(string playerId, string evt, string json)
        {
            if (string.IsNullOrEmpty(playerId)) return;   // local/AI seats have no socket
            pendingPrompts[playerId] = new PendingPrompt(evt, json);
        }

        /// <summary>The prompt is answered (or expired) — stop offering to replay it.</summary>
        public void ClearPendingPrompt(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            pendingPrompts.Remove(playerId);
        }

        /// <summary>
        /// Re-send this player's open prompt, if any, to whatever socket now holds their seat.
        /// Addressed by playerId exactly like the original, so the relay routes it to the new socket
        /// with no idea a reconnection happened.
        /// </summary>
        public void ResendPendingPrompt(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (!pendingPrompts.TryGetValue(playerId, out var prompt)) return;
            if (!GuardConnected("ResendPendingPrompt")) return;

            Debug.Log($"[NetworkManager] Re-sending {prompt.Event} to {playerId} after reconnect.");
            _ = socketClient.Emit(prompt.Event, prompt.Json);
        }

        /// <summary>Drop every remembered prompt — a new game owes nobody an answer.</summary>
        public void ClearAllPendingPrompts() => pendingPrompts.Clear();

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

            // Remembered PER ENTRY, each as its own single-entry batch: a replay must carry only
            // the returning player's own prompt. Replaying the whole batch would hand one phone
            // every other player's `acting` flag — the masking model's one unforgivable leak.
            if (msg?.prompts != null)
            {
                foreach (var entry in msg.prompts)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.playerId)) continue;
                    var single = new SecretPhasePromptMsg { prompts = new[] { entry } };
                    RememberPrompt(entry.playerId, "secret_phase_prompt", JsonUtility.ToJson(single));
                }
            }

            _ = socketClient.Emit("secret_phase_prompt", JsonUtility.ToJson(msg));
        }

        public void SendActionRequest(ActionRequestMsg msg)
        {
            if (!GuardConnected("SendActionRequest")) return;
            var json = JsonUtility.ToJson(msg);
            RememberPrompt(msg.playerId, "action_request", json);
            _ = socketClient.Emit("action_request", json);
        }

        public void SendDeckRearrangeRequest(DeckRearrangeRequestMsg msg)
        {
            if (!GuardConnected("SendDeckRearrangeRequest")) return;
            var json = JsonUtility.ToJson(msg);
            RememberPrompt(msg.playerId, "deck_rearrange_request", json);
            _ = socketClient.Emit("deck_rearrange_request", json);
        }

        public void SendCardPickRequest(CardPickRequestMsg msg)
        {
            if (!GuardConnected("SendCardPickRequest")) return;
            var json = JsonUtility.ToJson(msg);
            RememberPrompt(msg.playerId, "card_pick_request", json);
            _ = socketClient.Emit("card_pick_request", json);
        }

        public void SendConfirmRequest(ConfirmRequestMsg msg)
        {
            if (!GuardConnected("SendConfirmRequest")) return;
            var json = JsonUtility.ToJson(msg);
            RememberPrompt(msg.playerId, "confirm_request", json);
            _ = socketClient.Emit("confirm_request", json);
        }

        public void SendTargetRequest(TargetRequestMsg msg)
        {
            if (!GuardConnected("SendTargetRequest")) return;
            var json = JsonUtility.ToJson(msg);
            RememberPrompt(msg.playerId, "target_request", json);
            _ = socketClient.Emit("target_request", json);
        }

        public void SendTryalPickRequest(TryalPickRequestMsg msg)
        {
            if (!GuardConnected("SendTryalPickRequest")) return;
            var json = JsonUtility.ToJson(msg);
            RememberPrompt(msg.playerId, "tryal_pick_request", json);
            _ = socketClient.Emit("tryal_pick_request", json);
        }

        // Host-facing echoes of the outgoing PUBLIC dramatic-beat messages. The host TV display
        // (Salem.UI.HostDisplay) subscribes to these so it renders the SAME payloads sent to phones/mirrors
        // — in particular public_reveal is NOT echoed to the host over the socket, so this is the host's
        // only signal for it. Carry only public data (a timestamp / already-public DTOs).
        public static event System.Action<long> OnPhaseResolveSent;
        public static event System.Action<EliminationResultMsg> OnEliminationResultSent;
        public static event System.Action<PublicRevealMsg> OnPublicRevealSent;
        public static event System.Action<GameEventMsg> OnGameEventSent;

        public void SendPhaseResolve(PhaseResolveMsg msg)
        {
            if (!GuardConnected("SendPhaseResolve")) return;
            _ = socketClient.Emit("phase_resolve", JsonUtility.ToJson(msg));
            OnPhaseResolveSent?.Invoke(msg.revealAt);
        }

        public void SendPublicReveal(PublicRevealMsg msg)
        {
            if (!GuardConnected("SendPublicReveal")) return;
            _ = socketClient.Emit("public_reveal", JsonUtility.ToJson(msg));
            OnPublicRevealSent?.Invoke(msg);
        }

        // Public event-log entry. Like public_reveal, this is NOT echoed back to the host by the
        // server, so the send-event is the host display's only signal.
        public void SendGameEvent(GameEventMsg msg)
        {
            if (!GuardConnected("SendGameEvent")) return;
            _ = socketClient.Emit("game_event", JsonUtility.ToJson(msg));
            OnGameEventSent?.Invoke(msg);
        }

        public void SendEliminationResult(EliminationResultMsg msg)
        {
            if (!GuardConnected("SendEliminationResult")) return;
            _ = socketClient.Emit("elimination_result", JsonUtility.ToJson(msg));
            OnEliminationResultSent?.Invoke(msg);
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

            socketClient.On("player_rejoined", json =>
            {
                var msg = JsonUtility.FromJson<PlayerRejoinedMsg>(json);
                Debug.Log($"[NetworkManager] Player rejoined: {msg.playerId} ({msg.displayName})");
                OnPlayerRejoined?.Invoke(msg.playerId, msg.displayName);
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

            socketClient.On("deck_rearrange_submit", json =>
            {
                var msg = JsonUtility.FromJson<DeckRearrangeSubmitMsg>(json);
                Debug.Log($"[NetworkManager] Deck rearrange submit from {msg.playerId}: confirmed={msg.confirmed}");
                OnDeckRearrangeSubmit?.Invoke(msg);
            });

            socketClient.On("card_pick_submit", json =>
            {
                var msg = JsonUtility.FromJson<CardPickSubmitMsg>(json);
                Debug.Log($"[NetworkManager] Card pick submit from {msg.playerId}: index {msg.index}");
                OnCardPickSubmit?.Invoke(msg);
            });

            socketClient.On("confirm_submit", json =>
            {
                var msg = JsonUtility.FromJson<ConfirmSubmitMsg>(json);
                Debug.Log($"[NetworkManager] Confirm submit from {msg.playerId}: confirmed={msg.confirmed}");
                OnConfirmSubmit?.Invoke(msg);
            });

            socketClient.On("target_submit", json =>
            {
                var msg = JsonUtility.FromJson<TargetSubmitMsg>(json);
                Debug.Log($"[NetworkManager] Target submit from {msg.playerId}: target={msg.targetPlayerId}");
                OnTargetSubmit?.Invoke(msg);
            });

            socketClient.On("tryal_pick_submit", json =>
            {
                var msg = JsonUtility.FromJson<TryalPickSubmitMsg>(json);
                Debug.Log($"[NetworkManager] Tryal pick submit from {msg.playerId}: ordinal={msg.ordinal}");
                OnTryalPickSubmit?.Invoke(msg);
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
                OnMirrorJoined?.Invoke();
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
                await socketClient.Connect(ServerUrlForConnect());
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
