using System.Collections.Generic;
using System.Linq;
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
            p.PlayerNameText = UniqueName(displayName); // names are used to resolve targets
            p.Input = new NetworkInput(p);

            PlayerService.Register(p);
            PlayerService.RegisterNetworkId(playerId, p);
            seats.Add(p);

            Debug.Log($"[Coordinator] Seated {playerId} ({displayName}). Players: {seats.Count}");
            OnRosterChanged?.Invoke();
        }

        private void HandlePlayerLeft(string playerId)
        {
            var p = PlayerService.GetByNetworkId(playerId);
            if (p == null) return;

            // Mark the seat disconnected so the secret-phase wait set drops it
            // immediately (RunNetworkedSecretPhase reads Player.IsConnected live) —
            // a dropped human no longer stalls a phase to its timeout.
            p.IsConnected = false;

            // Pre-game: also free the lobby seat.
            seats.Remove(p);
            Debug.Log($"[Coordinator] {playerId} left (IsConnected=false).");
            OnRosterChanged?.Invoke();
            // Note: full seat cleanup (PlayerService.All removal + Destroy), reconnect,
            // and turn-order removal remain post-4a (4c only stops the phase stall).
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
            ai.PlayerNameText = UniqueName($"AI {idx + 1}");
            // No NetworkId, no NetworkInput — AI runs via AITurnSequencer and is
            // skipped for private_state (routing keys on NetworkId).
            PlayerService.Register(ai);
            seats.Add(ai);
            OnRosterChanged?.Invoke();
        }

        // Targets resolve by PlayerNameText, so names must be unique across ALL seats
        // (humans + AI). Append " (2)", " (3)", … on collision.
        private string UniqueName(string desired)
        {
            if (string.IsNullOrWhiteSpace(desired)) desired = "Player";
            bool Taken(string n) => PlayerService.All.Any(pl => pl != null && pl.PlayerNameText == n);
            if (!Taken(desired)) return desired;
            for (int i = 2; ; i++)
            {
                var candidate = $"{desired} ({i})";
                if (!Taken(candidate)) return candidate;
            }
        }
    }
}
