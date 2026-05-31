using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;

namespace Salem.Networking
{
    /// <summary>
    /// Socket.io v4 (Engine.io v4) wire protocol handler over NativeWebSocket.
    /// NOT a MonoBehaviour — owned and driven by NetworkManager.
    ///
    /// Handles the Engine.io handshake, ping/pong keepalive, namespace connect,
    /// and 42["event",{payload}] message framing that Socket.io layers on top
    /// of raw WebSockets.
    /// </summary>
    public class SocketIOClient
    {
        // ─── State ────────────────────────────────────────────────

        private WebSocket ws;
        private readonly Dictionary<string, Action<string>> handlers = new Dictionary<string, Action<string>>();

        private string sid;
        private int pingIntervalMs = 25000;
        private int pingTimeoutMs = 20000;
        private float lastPingTime;
        private bool namespaceConnected;

        // ─── Public API — State ───────────────────────────────────

        public bool IsConnected => namespaceConnected && ws != null && ws.State == WebSocketState.Open;

        /// <summary>Seconds since the last ping was received from the server.</summary>
        public float TimeSinceLastPing => Time.realtimeSinceStartup - lastPingTime;

        /// <summary>
        /// Total seconds the server allows before considering the client dead.
        /// If TimeSinceLastPing exceeds this, the connection is likely stale.
        /// </summary>
        public float PingTimeoutThreshold => (pingIntervalMs + pingTimeoutMs) / 1000f;

        // ─── Public API — Events ─────────────────────────────────

        public event Action OnConnected;
        public event Action<string> OnDisconnected;

        // ─── Public API — Lifecycle ──────────────────────────────

        /// <summary>
        /// Connect to a Socket.io server. The url should be the base server URL
        /// (e.g. "ws://localhost:3000"). The Socket.io path is appended automatically.
        /// </summary>
        public async Task Connect(string url)
        {
            if (ws != null)
            {
                Debug.LogWarning("[SocketIO] Already connected or connecting. Call Disconnect() first.");
                return;
            }

            // Build the Engine.io WebSocket URL
            string wsUrl = url.TrimEnd('/') + "/socket.io/?EIO=4&transport=websocket";
            Debug.Log($"[SocketIO] Connecting to {wsUrl}");

            namespaceConnected = false;
            lastPingTime = Time.realtimeSinceStartup;

            ws = new WebSocket(wsUrl);

            ws.OnOpen += () =>
            {
                Debug.Log("[SocketIO] WebSocket connection opened, waiting for Engine.io handshake...");
            };

            ws.OnMessage += (bytes) =>
            {
                string message = Encoding.UTF8.GetString(bytes);
                HandleMessage(message);
            };

            ws.OnClose += (closeCode) =>
            {
                Debug.Log($"[SocketIO] WebSocket closed with code: {closeCode}");
                namespaceConnected = false;
                OnDisconnected?.Invoke(closeCode.ToString());
            };

            ws.OnError += (error) =>
            {
                Debug.LogError($"[SocketIO] WebSocket error: {error}");
            };

            await ws.Connect();
        }

        /// <summary>Cleanly disconnect from the server.</summary>
        public async Task Disconnect()
        {
            if (ws == null) return;

            namespaceConnected = false;

            try
            {
                await ws.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SocketIO] Error during disconnect: {e.Message}");
            }

            ws = null;
        }

        /// <summary>
        /// Must be called every frame from NetworkManager.Update().
        /// NativeWebSocket requires this to dispatch queued messages on the main thread.
        /// </summary>
        public void DispatchMessageQueue()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            ws?.DispatchMessageQueue();
#endif
        }

        // ─── Public API — Event Registration ─────────────────────

        /// <summary>
        /// Register a handler for a Socket.io event.
        /// The handler receives the raw JSON payload string (or null if no payload).
        /// </summary>
        public void On(string eventName, Action<string> handler)
        {
            handlers[eventName] = handler;
        }

        /// <summary>Remove a previously registered handler.</summary>
        public void Off(string eventName)
        {
            handlers.Remove(eventName);
        }

        // ─── Public API — Sending ────────────────────────────────

        /// <summary>Emit an event with no payload.</summary>
        public async Task Emit(string eventName)
        {
            if (!IsConnected)
            {
                Debug.LogWarning($"[SocketIO] Cannot emit '{eventName}' — not connected.");
                return;
            }

            string message = $"42[\"{eventName}\"]";
            await SendRaw(message);
        }

        /// <summary>Emit an event with a JSON payload string.</summary>
        public async Task Emit(string eventName, string jsonPayload)
        {
            if (!IsConnected)
            {
                Debug.LogWarning($"[SocketIO] Cannot emit '{eventName}' — not connected.");
                return;
            }

            string message = $"42[\"{eventName}\",{jsonPayload}]";
            await SendRaw(message);
        }

        // ─── Wire Protocol Handling ──────────────────────────────

        private void HandleMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            char packetType = message[0];

            switch (packetType)
            {
                case '0': // Engine.io OPEN — handshake
                    HandleEngineOpen(message.Substring(1));
                    break;

                case '2': // Engine.io PING
                    HandlePing();
                    break;

                case '3': // Engine.io PONG (server responding to our ping — unlikely but handle)
                    lastPingTime = Time.realtimeSinceStartup;
                    break;

                case '4': // Socket.io packet
                    if (message.Length > 1)
                    {
                        HandleSocketIOPacket(message[1], message.Length > 2 ? message.Substring(2) : null);
                    }
                    break;

                default:
                    Debug.Log($"[SocketIO] Unhandled packet type '{packetType}': {message}");
                    break;
            }
        }

        private void HandleEngineOpen(string json)
        {
            try
            {
                var handshake = JsonUtility.FromJson<EngineIOHandshake>(json);
                sid = handshake.sid;
                pingIntervalMs = handshake.pingInterval;
                pingTimeoutMs = handshake.pingTimeout;
                lastPingTime = Time.realtimeSinceStartup;

                Debug.Log($"[SocketIO] Engine.io handshake received, sid={sid}, " +
                          $"pingInterval={pingIntervalMs}ms, pingTimeout={pingTimeoutMs}ms");

                // Request Socket.io namespace connection
                _ = SendRaw("40");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SocketIO] Failed to parse Engine.io handshake: {e.Message}");
            }
        }

        private async void HandlePing()
        {
            lastPingTime = Time.realtimeSinceStartup;
            await SendRaw("3"); // Pong
        }

        private void HandleSocketIOPacket(char socketType, string data)
        {
            switch (socketType)
            {
                case '0': // CONNECT — namespace connected
                    namespaceConnected = true;
                    Debug.Log("[SocketIO] Connected to namespace /");
                    OnConnected?.Invoke();
                    break;

                case '1': // DISCONNECT
                    Debug.Log("[SocketIO] Server requested disconnect");
                    namespaceConnected = false;
                    OnDisconnected?.Invoke("server_disconnect");
                    break;

                case '2': // EVENT — 42["eventName",{payload}]
                    if (data != null)
                    {
                        ParseAndRouteEvent(data);
                    }
                    break;

                default:
                    Debug.Log($"[SocketIO] Unhandled socket.io packet type '4{socketType}'");
                    break;
            }
        }

        /// <summary>
        /// Parse a Socket.io event from the data portion of a 42 packet.
        /// Input is the string after "42", e.g.: ["eventName",{"key":"value"}]
        ///
        /// Manual string parsing — no JSON parser for the outer array because
        /// JsonUtility cannot handle heterogeneous arrays.
        /// </summary>
        private void ParseAndRouteEvent(string data)
        {
            // Expected format: ["eventName"] or ["eventName",{...}]
            // Find event name between first pair of quotes after [
            int firstQuote = data.IndexOf('"');
            if (firstQuote < 0) return;

            int secondQuote = data.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0) return;

            string eventName = data.Substring(firstQuote + 1, secondQuote - firstQuote - 1);

            // Find payload: everything after the comma up to the closing ]
            string payload = null;
            int commaIndex = data.IndexOf(',', secondQuote);
            if (commaIndex >= 0)
            {
                // Payload exists — extract from after comma to before closing ]
                int lastBracket = data.LastIndexOf(']');
                if (lastBracket > commaIndex)
                {
                    payload = data.Substring(commaIndex + 1, lastBracket - commaIndex - 1).Trim();
                }
            }

            // Route to registered handler
            if (handlers.TryGetValue(eventName, out var handler))
            {
                try
                {
                    handler.Invoke(payload);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SocketIO] Error in handler for '{eventName}': {e.Message}\n{e.StackTrace}");
                }
            }
            else
            {
                Debug.Log($"[SocketIO] No handler for event '{eventName}'");
            }
        }

        // ─── Raw Send ────────────────────────────────────────────

        private async Task SendRaw(string message)
        {
            if (ws == null || ws.State != WebSocketState.Open)
            {
                Debug.LogWarning($"[SocketIO] Cannot send — WebSocket not open. Message: {message}");
                return;
            }

            try
            {
                await ws.SendText(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SocketIO] Send failed: {e.Message}");
            }
        }
    }
}
