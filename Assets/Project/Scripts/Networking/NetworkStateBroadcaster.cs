using System.Collections.Generic;
using System.Linq;
using Salem.Cards;
using Salem.Data;
using Salem.Deck;
using Salem.GameFlow;
using Salem.Players;
using UnityEngine;

namespace Salem.Networking
{
    /// <summary>
    /// Emits outbound state so phones/mirrors stay in sync during Day turns.
    /// Subscribes to game events; the game systems never call networking directly.
    ///
    /// PRIVACY (enforced here):
    ///  • game_state_update is PUBLIC — built only from public fields
    ///    (names, accusation counts, eliminated status, public status cards).
    ///    It NEVER contains tryals, role, or hands.
    ///  • private_state is built per-player and sent addressed to that player's
    ///    NetworkId; the server routes it to that one socket. One message per
    ///    player — never a broadcast. AI seats (no NetworkId) get no private_state.
    /// </summary>
    public class NetworkStateBroadcaster : MonoBehaviour
    {
        [SerializeField] private GamePhaseManager gamePhaseManager;
        [SerializeField] private GameTurnManager gameTurnManager;
        [SerializeField] private DeckManager deckManager;

        private void Awake()
        {
            if (!gamePhaseManager) gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
            if (!gameTurnManager) gameTurnManager = FindFirstObjectByType<GameTurnManager>();
            if (!deckManager) deckManager = FindFirstObjectByType<DeckManager>();
        }

        private void OnEnable()
        {
            PlayerService.OnPlayerEliminated += HandleEliminated;
            CardEffectManager.OnCardPlayed += HandleCardPlayed;
        }

        private void Start()
        {
            if (gameTurnManager != null)
            {
                gameTurnManager.TurnStarted += HandleTurnChanged;
                gameTurnManager.TurnEnded += HandleTurnChanged;
            }
            if (gamePhaseManager != null)
            {
                gamePhaseManager.OnPhaseChange += HandlePhaseChanged;
            }
        }

        private void OnDisable()
        {
            PlayerService.OnPlayerEliminated -= HandleEliminated;
            CardEffectManager.OnCardPlayed -= HandleCardPlayed;
        }

        private void OnDestroy()
        {
            if (gameTurnManager != null)
            {
                gameTurnManager.TurnStarted -= HandleTurnChanged;
                gameTurnManager.TurnEnded -= HandleTurnChanged;
            }
            if (gamePhaseManager != null)
            {
                gamePhaseManager.OnPhaseChange -= HandlePhaseChanged;
            }
        }

        // ─── Event handlers → broadcast ───────────────────────────

        private void HandleTurnChanged(Player _) => BroadcastAll();
        private void HandlePhaseChanged(GamePhase _) => BroadcastAll();
        private void HandleEliminated(Player _, EliminationCause __) => BroadcastAll();
        private void HandleCardPlayed(string _) => BroadcastAll();

        // ─── Broadcast ────────────────────────────────────────────

        private void BroadcastAll()
        {
            if (PlayerService.Mode != GameMode.Networked) return;
            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsConnected) return;

            // 1) Public board → everyone.
            nm.SendGameStateUpdate(BuildGameStateUpdate());

            // 2) Private state → each remote player individually (by NetworkId).
            SendPrivateStates();
        }

        /// <summary>
        /// Send each remote player their own private_state (tryals/hand/role and, once
        /// revealed at dawn, fellow witches). One addressed message per player — never
        /// a broadcast. Called by the dawn witch-reveal as well as the normal tick.
        /// </summary>
        public void SendPrivateStates()
        {
            if (PlayerService.Mode != GameMode.Networked) return;
            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsConnected) return;

            foreach (var p in PlayerService.All)
            {
                if (p == null || string.IsNullOrEmpty(p.NetworkId)) continue; // AI/local: no phone
                nm.SendPrivateState(BuildPrivateState(p));
            }
        }

        // ─── Public message (NO private data) ─────────────────────

        private GameStateUpdateMsg BuildGameStateUpdate()
        {
            var players = new List<PublicPlayerMsg>();
            foreach (var p in PlayerService.All)
            {
                if (p == null) continue;
                players.Add(new PublicPlayerMsg
                {
                    playerId = PublicIdFor(p),
                    displayName = p.PlayerNameText,
                    accusations = p.currentAccusationCount,
                    eliminated = p.IsEliminated,
                    statusCards = BuildPublicStatusCards(p),
                });
            }

            return new GameStateUpdateMsg
            {
                phase = gamePhaseManager != null
                    ? gamePhaseManager.CurrentPhase.ToString().ToLowerInvariant()
                    : "",
                whoseTurn = gameTurnManager != null
                    ? PublicIdFor(gameTurnManager.CurrentPlayer)
                    : "",
                players = players.ToArray(),
                deckCount = deckManager != null ? deckManager.DeckCount : 0,
                discardCount = deckManager != null ? deckManager.DiscardCount : 0,
            };
        }

        // Public blue/status cards in front of a player (names only) + Black Cat.
        private static string[] BuildPublicStatusCards(Player p)
        {
            var names = new List<string>();
            if (p.StatusCards != null)
            {
                names.AddRange(p.StatusCards.Where(c => c != null).Select(c => c.Name));
            }
            if (p.IsBlackCatHolder) names.Add("Black Cat");
            return names.ToArray();
        }

        // ─── Private message (sent ONLY to its owner by NetworkId) ─

        private PrivateStateMsg BuildPrivateState(Player p)
        {
            var tryals = (p.TryalCards ?? new List<TryalCard>())
                .Where(t => t != null)
                .Select(t => new TryalViewMsg
                {
                    label = LabelFor(t.TryalCardType),
                    faceUp = t.IsRevealed,
                })
                .ToArray();

            var hand = p.HandManager != null && p.HandManager.Hand != null
                ? p.HandManager.Hand.Where(c => c != null).Select(c => c.Name).ToArray()
                : new string[0];

            // Fellow witches: only for a witch, and only after the dawn reveal. Private
            // channel (routed to this player's socket) — never appears in public state.
            string[] fellowWitches = new string[0];
            if (p.IsWitch && gamePhaseManager != null && gamePhaseManager.WitchesRevealed)
            {
                fellowWitches = PlayerService.GetAliveWitches()
                    .Where(w => w != null && w != p)
                    .Select(w => w.PlayerNameText)
                    .ToArray();
            }

            // Live witch-vote tally (other witches' tentative picks) — witch-only,
            // populated only during a witch round. Private channel; never broadcast.
            WitchVoteMsg[] witchVotes = (p.IsWitch && gamePhaseManager != null)
                ? gamePhaseManager.BuildWitchTallyFor(p)
                : new WitchVoteMsg[0];

            return new PrivateStateMsg
            {
                playerId = p.NetworkId,
                tryals = tryals,
                hand = hand,
                role = RoleFor(p),
                isWitch = p.IsWitch,         // independent truths — both can be set
                isConstable = p.IsConstable, // (evil constable holds both tryals)
                fellowWitches = fellowWitches,
                witchVotes = witchVotes,
            };
        }

        // Public display id: NetworkId for human seats, synthetic PublicId for AI.
        // Used ONLY in the public message; private_state still routes by NetworkId.
        private static string PublicIdFor(Player p)
        {
            if (p == null) return "";
            return !string.IsNullOrEmpty(p.NetworkId) ? p.NetworkId : (p.PublicId ?? "");
        }

        private static string LabelFor(TryalCardType type)
        {
            switch (type)
            {
                case TryalCardType.Witch: return "Witch";
                case TryalCardType.Constable: return "Constable";
                default: return "Not a Witch";
            }
        }

        private static string RoleFor(Player p)
        {
            if (p.IsWitch) return "witch";
            if (p.IsConstable) return "constable";
            return "townsperson";
        }
    }
}
