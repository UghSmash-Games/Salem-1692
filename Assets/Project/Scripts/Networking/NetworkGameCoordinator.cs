using System.Collections.Generic;
using Salem.Data;
using Salem.GameFlow;
using Salem.Players;
using UnityEngine;

namespace Salem.Networking
{
    /// <summary>
    /// The lobby + glue for networked play. The ONLY place that drives game flow
    /// from the network (keeps NetworkManager a pure relay bridge).
    ///
    /// Runs before GameManager (execution order) so it can switch PlayerService
    /// into Networked mode before GameManager's Awake decides whether to scan the
    /// scene for pre-placed players.
    ///
    /// Lobby: connect → create room → players join (instantiated as Player prefabs
    /// with a NetworkInput) → host presses Start → optional AI fill → BeginGame().
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class NetworkGameCoordinator : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private GamePhaseManager gamePhaseManager;
        [SerializeField] private Player playerPrefab;       // base Player (human), isHuman = true
        [SerializeField] private AIPlayer aiPlayerPrefab;   // AIPlayer, isHuman = false
        [SerializeField] private Transform spawnParent;

        [Header("Lobby Rules")]
        [Tooltip("TryalDistribution supports 4–12 players, so 4 is the floor.")]
        [SerializeField] private int minPlayers = 4;
        [Tooltip("Opt-in: fill empty seats with AI up to targetPlayerCount on Start.")]
        [SerializeField] private bool fillWithAI = false;
        [SerializeField] private int targetPlayerCount = 4;

        // Lobby roster, in join order.
        private readonly List<Player> seats = new();
        private int aiSeatCounter;

        public string RoomCode { get; private set; }
        public IReadOnlyList<Player> Seats => seats;
        public event System.Action<string> OnRoomCodeAssigned;
        public event System.Action OnRosterChanged;

        // Switch to networked mode as early as possible (before GameManager.Awake).
        private void Awake()
        {
            PlayerService.Mode = GameMode.Networked;
        }

        private void Start()
        {
            var nm = NetworkManager.Instance;
            if (nm == null)
            {
                Debug.LogError("[Coordinator] No NetworkManager in scene.");
                return;
            }

            nm.OnConnectedToServer += HandleConnected;
            nm.OnRoomCreated += HandleRoomCreated;
            nm.OnPlayerJoined += HandlePlayerJoined;
            nm.OnPlayerLeft += HandlePlayerLeft;

            nm.ConnectToServer();
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;
            nm.OnConnectedToServer -= HandleConnected;
            nm.OnRoomCreated -= HandleRoomCreated;
            nm.OnPlayerJoined -= HandlePlayerJoined;
            nm.OnPlayerLeft -= HandlePlayerLeft;
        }

        // ─── Lobby ────────────────────────────────────────────────

        private void HandleConnected()
        {
            NetworkManager.Instance.CreateRoom();
        }

        private void HandleRoomCreated(string code)
        {
            RoomCode = code;
            Debug.Log($"[Coordinator] Room created: {code}");
            OnRoomCodeAssigned?.Invoke(code);
        }

        private void HandlePlayerJoined(string playerId, string displayName)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[Coordinator] No playerPrefab assigned.");
                return;
            }

            var p = Instantiate(playerPrefab, spawnParent);
            // playerPrefab is the human Player prefab (isHuman = true in the prefab).
            p.NetworkId = playerId;
            p.PlayerNameText = displayName;
            p.Input = new NetworkInput(p);

            PlayerService.Register(p);
            PlayerService.RegisterNetworkId(playerId, p);
            seats.Add(p);

            Debug.Log($"[Coordinator] Seated {playerId} ({displayName}). Players: {seats.Count}");
            OnRosterChanged?.Invoke();
        }

        private void HandlePlayerLeft(string playerId)
        {
            // Pre-game: free the seat. Mid-game disconnect handling is post-4a.
            var p = PlayerService.GetByNetworkId(playerId);
            if (p == null) return;
            seats.Remove(p);
            Debug.Log($"[Coordinator] {playerId} left the lobby.");
            OnRosterChanged?.Invoke();
            // Note: leaving PlayerService.All cleanup + Destroy to mid-game handling (post-4a).
        }

        // ─── Start ────────────────────────────────────────────────

        // TEMP TEST TRIGGER — remove when the real lobby Start button UI is built.
        // In Play mode: right-click the NetworkGameCoordinator component header in
        // the Inspector and choose "TEST — Start Game".
        [ContextMenu("TEST — Start Game")]
        private void DebugStartGame() => StartGame();

        /// <summary>Host presses Start. Optionally fills AI seats, then begins.</summary>
        public void StartGame()
        {
            if (fillWithAI)
            {
                int needed = targetPlayerCount - PlayerService.All.Count;
                for (int i = 0; i < needed; i++)
                {
                    SpawnAISeat();
                }
            }

            int count = PlayerService.All.Count;
            if (count < minPlayers)
            {
                Debug.LogWarning($"[Coordinator] Need at least {minPlayers} players to start (have {count}). " +
                                 "Enable AI fill or wait for more joins.");
                return;
            }

            if (gamePhaseManager == null)
            {
                Debug.LogError("[Coordinator] No GamePhaseManager assigned.");
                return;
            }

            Debug.Log($"[Coordinator] Starting game with {count} players.");
            gamePhaseManager.BeginGame();
        }

        private void SpawnAISeat()
        {
            if (aiPlayerPrefab == null)
            {
                Debug.LogWarning("[Coordinator] AI fill requested but no aiPlayerPrefab assigned.");
                return;
            }

            int idx = aiSeatCounter++;
            var ai = Instantiate(aiPlayerPrefab, spawnParent);
            ai.PublicId = $"ai{idx}";           // public display identity only
            ai.PlayerNameText = $"AI {idx + 1}";
            // No NetworkId, no NetworkInput — AI runs via AITurnSequencer and is
            // skipped for private_state (routing keys on NetworkId).
            PlayerService.Register(ai);
            seats.Add(ai);
            OnRosterChanged?.Invoke();
        }
    }
}
