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
        [Tooltip("TryalDistribution supports 4–12 players, so 12 is the ceiling.")]
        [SerializeField] private int maxPlayers = 12;
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

        /// <summary>True once StartGame has handed off to GamePhaseManager. Drives the host lobby
        /// panel's dismissal; also guards against a double Start.</summary>
        public bool HasStarted { get; private set; }
        public event System.Action OnGameStarted;

        /// <summary>Minimum seats needed to begin — read by the lobby panel for its "N more" copy.</summary>
        public int MinPlayers => minPlayers;

        /// <summary>
        /// Upper bound on seats. GameSetup.TryalDistribution defines 4–12 ONLY and logs an error and
        /// aborts setup outside that range, so the lobby must never let the host aim past it.
        /// </summary>
        public int MaxPlayers => maxPlayers;

        // ── Lobby settings (host-operator controlled, pre-game only) ──

        /// <summary>Whether empty seats are filled with AI up to <see cref="TargetPlayerCount"/>.</summary>
        public bool FillWithAI => fillWithAI;

        /// <summary>Seat count to fill up to when <see cref="FillWithAI"/> is on. Clamped to 4–12.</summary>
        public int TargetPlayerCount => Mathf.Clamp(targetPlayerCount, minPlayers, maxPlayers);

        /// <summary>Raised when a lobby SETTING changes (AI fill / target count), so the panel repaints.
        /// Distinct from OnRosterChanged, which means the set of seats itself changed.</summary>
        public event System.Action OnLobbySettingsChanged;

        public void SetFillWithAI(bool value)
        {
            if (HasStarted || fillWithAI == value) return;
            fillWithAI = value;
            OnLobbySettingsChanged?.Invoke();
        }

        public void SetTargetPlayerCount(int value)
        {
            if (HasStarted) return;
            int clamped = Mathf.Clamp(value, minPlayers, maxPlayers);
            if (clamped == targetPlayerCount) return;
            targetPlayerCount = clamped;
            OnLobbySettingsChanged?.Invoke();
        }

        /// <summary>
        /// THE single authority on whether Start is legal, shared by the button's enabled state and
        /// by StartGame's own refusal. Keeping one predicate means the button can never offer a start
        /// that StartGame would then reject (or grey out one it would have accepted).
        ///
        /// `reason` is human copy for the lobby panel when it returns false.
        /// </summary>
        public bool CanStart(out string reason)
        {
            if (HasStarted) { reason = "The game has already begun."; return false; }

            int humans = PlayerService.All.Count;
            int finalCount = fillWithAI ? Mathf.Max(humans, TargetPlayerCount) : humans;

            if (finalCount < minPlayers)
            {
                int shortfall = minPlayers - finalCount;
                reason = fillWithAI
                    ? $"Raise the table size — {shortfall} more seat{(shortfall == 1 ? "" : "s")} needed."
                    : $"Waiting for {shortfall} more player{(shortfall == 1 ? "" : "s")} — or fill with AI.";
                return false;
            }

            if (finalCount > maxPlayers)
            {
                reason = $"Too many players — {maxPlayers} is the maximum.";
                return false;
            }

            reason = "";
            return true;
        }

        /// <summary>
        /// One lobby seat, with NO reference to the Player model.
        ///
        /// This exists so the host display can render the lobby roster without breaching its
        /// masking boundary: `Seats` above is IReadOnlyList&lt;Player&gt;, and every file in
        /// Assets/Project/Scripts/UI/HostDisplay is forbidden from naming Player at all. Carries
        /// only what a person can already see across the table — a name, and whether the seat is a
        /// bot. It is deliberately a projection, not a view onto the live object, so it cannot grow
        /// access to a hand or a tryal later.
        /// </summary>
        public readonly struct LobbySeatInfo
        {
            public readonly string DisplayName;
            public readonly bool IsAI;

            public LobbySeatInfo(string displayName, bool isAI)
            {
                DisplayName = displayName;
                IsAI = isAI;
            }
        }

        /// <summary>The lobby roster in join order, projected free of the Player model.</summary>
        public List<LobbySeatInfo> BuildLobbySeats()
        {
            var list = new List<LobbySeatInfo>(seats.Count);
            foreach (var s in seats)
            {
                if (s == null) continue;
                list.Add(new LobbySeatInfo(s.PlayerNameText, !s.IsHuman));
            }
            return list;
        }

        // Switch to networked mode as early as possible (before GameManager.Awake).
        private void Awake()
        {
            PlayerService.Mode = GameMode.Networked;

            // TimerSettings is static, so it survives a domain reload and would otherwise carry the
            // previous game's pace — and its LOCK — into this lobby, leaving the host unable to
            // change it.
            TimerSettings.ResetForNewGame();
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
            nm.OnPlayerRejoined += HandlePlayerRejoined;

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
            nm.OnPlayerRejoined -= HandlePlayerRejoined;
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

            // ⚠ ONLY the lobby frees the chair. Mid-game the seat is HELD: it holds tryal cards, a
            // hand, a place in the turn order and possibly a witch identity, and phones drop
            // constantly in normal play (screen lock, backgrounded tab, wifi blip). Removing the
            // seat on every blip is what used to make a dropped player unrecoverable — the relay
            // now reserves their seat too, and HandlePlayerRejoined puts them back in it.
            if (!HasStarted)
            {
                // 🐛 The seat must leave PlayerService too, not just the lobby list. StartGame deals
                // from PlayerService.All, so a seat removed from `seats` alone was still dealt a
                // hand and tryals and then never acted — an orphan player nobody could see leaving.
                // Reconnection made it worse: rejoining registered a SECOND seat for one human.
                seats.Remove(p);
                PlayerService.Unregister(p);
                Destroy(p.gameObject);
                Debug.Log($"[Coordinator] {playerId} left the lobby — seat freed.");
            }
            else
            {
                Debug.Log($"[Coordinator] {playerId} dropped mid-game — seat HELD for reconnect.");
            }

            OnRosterChanged?.Invoke();
        }

        /// <summary>
        /// A seat came back on a new socket. The phone reconnected with an EMPTY store — it knows
        /// neither its tryals nor its hand, and not the prompt it may be holding the game up on — so
        /// the host re-sends all three. Nothing here trusts the phone: the relay already proved the
        /// seat with the token before this event was raised.
        /// </summary>
        private void HandlePlayerRejoined(string playerId, string displayName)
        {
            var p = PlayerService.GetByNetworkId(playerId);

            if (p == null)
            {
                // They left during the LOBBY, so the chair was freed above. Seat them again under
                // the SAME playerId — their phone is still holding that seat's token, and minting a
                // new id here would strand it.
                if (!HasStarted)
                {
                    Debug.Log($"[Coordinator] {playerId} returned to the lobby — re-seating.");
                    HandlePlayerJoined(playerId, displayName);
                }
                else
                {
                    // Mid-game with no seat should be impossible (the seat is held above), so this
                    // is a real inconsistency rather than something to paper over by inventing a
                    // seat — a new seat mid-game would have no tryals and break the deal.
                    Debug.LogWarning($"[Coordinator] {playerId} rejoined mid-game with no seat.");
                }
                return;
            }

            p.IsConnected = true;
            Debug.Log($"[Coordinator] {playerId} reconnected — restoring their state.");
            OnRosterChanged?.Invoke();

            // 1) Board + this player's own private state (tryals, hand, role, fellow witches).
            // Found the same way GamePhaseManager finds it — the broadcaster is a scene component,
            // not a singleton.
            FindFirstObjectByType<NetworkStateBroadcaster>()?.BroadcastNow();

            // 2) The prompt they may still owe an answer to. Without this the game waits on a phone
            // that no longer knows it was asked, until the phase times out.
            NetworkManager.Instance?.ResendPendingPrompt(playerId);
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
            // ⚠ IDEMPOTENT. Harmless while the only trigger was an Inspector context menu; essential
            // now that a button can be double-clicked — a second call would run BeginGame's setup
            // coroutine over a game already being dealt.
            if (HasStarted)
            {
                Debug.LogWarning("[Coordinator] StartGame ignored — the game has already started.");
                return;
            }

            // A fresh game owes nobody an answer — drop any prompt left remembered for replay.
            NetworkManager.Instance?.ClearAllPendingPrompts();

            if (gamePhaseManager == null)
            {
                Debug.LogError("[Coordinator] No GamePhaseManager assigned.");
                return;
            }

            // ⚠ VALIDATE BEFORE SPAWNING. The old order filled AI seats first and only then checked
            // the minimum, so a target below minPlayers stranded orphan AI seats in the lobby that
            // no later click could clear — the roster was permanently polluted with bots for a game
            // that never began.
            if (!CanStart(out string reason))
            {
                Debug.LogWarning($"[Coordinator] StartGame refused: {reason}");
                return;
            }

            if (fillWithAI)
            {
                int needed = TargetPlayerCount - PlayerService.All.Count;
                for (int i = 0; i < needed; i++)
                {
                    SpawnAISeat();
                }
            }

            int count = PlayerService.All.Count;

            Debug.Log($"[Coordinator] Starting game with {count} players.");

            // Flagged BEFORE the hand-off: BeginGame runs the setup coroutine, and the lobby panel
            // must be gone by the time the first public broadcast paints the board behind it.
            HasStarted = true;

            // Freeze the pace: a window must not move while players are already racing it, and a
            // mid-phase change could resolve a round early.
            TimerSettings.Lock();

            OnGameStarted?.Invoke();

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
