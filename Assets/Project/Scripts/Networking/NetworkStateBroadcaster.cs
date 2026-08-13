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
    ///  • game_state_update is PUBLIC — built only from public fields (names, accusation counts +
    ///    the red cards behind them, eliminated status, public blue status cards, printed Town Hall
    ///    identity, tryal COUNT, the labels of ALREADY-REVEALED tryals, hand COUNT, and the top
    ///    discard card's name).
    ///    It NEVER contains UNREVEALED tryal identities, role, or hand CONTENTS.
    ///
    ///    ⚠ STRUCTURAL NOTE — the public builder DOES read private-adjacent collections:
    ///    Player.TryalCards (tryalTotal + revealedTryals), Player.HandManager (handCount), and the
    ///    discard pile (topDiscard). Before Phase 7 it read none of them, so the public path was
    ///    structurally incapable of leaking them; it is now merely *correct*. The server relays
    ///    game_state_update verbatim (server/src/dispatch.js, host-role gate only, no field
    ///    filtering), so there is NO server-side backstop — these helpers ARE the enforcement:
    ///      • <see cref="BuildRevealedTryalLabels"/> — the single .Where(IsRevealed)
    ///      • <see cref="BuildPublicHandCount"/>    — returns an int, never card names
    ///      • <see cref="BuildPublicTopDiscard"/>   — returns ONE name, never the pile
    ///    Each is deliberately narrow and greppable. Do not inline them, do not widen their return
    ///    types, and do not add a second access path. The contract test in
    ///    server/__tests__/public-payload-contract.test.js is the permanent regression guard.
    ///  • private_state is built per-player and sent addressed to that player's
    ///    NetworkId; the server routes it to that one socket. One message per
    ///    player — never a broadcast. AI seats (no NetworkId) get no private_state.
    /// </summary>
    public class NetworkStateBroadcaster : MonoBehaviour
    {
        [SerializeField] private GamePhaseManager gamePhaseManager;
        [SerializeField] private GameTurnManager gameTurnManager;
        [SerializeField] private DeckManager deckManager;

        /// <summary>
        /// Fired with the PUBLIC board DTO every time it is broadcast — the exact same object sent to
        /// phones and mirrors. The host TV display (Salem.UI.HostDisplay) subscribes to this so it renders
        /// from the public contract ONLY, never from Player models. This is the Phase-7 masking boundary:
        /// a subscriber physically cannot see private data through this channel.
        /// </summary>
        public static event System.Action<GameStateUpdateMsg> OnPublicState;

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
            // Reveal → immediate re-broadcast so a role change from a tryal reveal (e.g. a revealed
            // constable losing the "Constable" tag) propagates AT the reveal, not on the next incidental
            // broadcast. Both are static events, subscribed/unsubscribed with the pair above.
            Player.TryalCardRevealed += HandleTryalRevealed;
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
            Player.TryalCardRevealed -= HandleTryalRevealed;
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
        private void HandleTryalRevealed(Player _, TryalCard __) => BroadcastAll();

        // ─── Broadcast ────────────────────────────────────────────

        /// <summary>
        /// Push current public + private state to everyone immediately. Used at a
        /// synchronized revealAt so model changes that don't fire an elimination event
        /// (e.g. a confession-only night, or a save) still propagate at the reveal moment.
        /// </summary>
        public void BroadcastNow() => BroadcastAll();

        private void BroadcastAll()
        {
            if (PlayerService.Mode != GameMode.Networked) return;
            var nm = NetworkManager.Instance;
            if (nm == null || !nm.IsConnected) return;

            // 1) Public board → everyone, AND to the host's own display via OnPublicState (same DTO).
            var publicState = BuildGameStateUpdate();
            nm.SendGameStateUpdate(publicState);
            OnPublicState?.Invoke(publicState);

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
                    accusationLimit = p.currentAccusationLimit, // base→piety×2 threshold (public; not Danforth-adjusted)
                    eliminated = p.IsEliminated,
                    statusCards = BuildPublicStatusCards(p),
                    accusationCards = BuildPublicAccusationCards(p),
                    townHall = BuildPublicTownHall(p),
                    tryalTotal = p.TryalCards != null ? p.TryalCards.Count : 0,
                    revealedTryals = BuildRevealedTryalLabels(p),
                    handCount = BuildPublicHandCount(p),
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
                topDiscard = BuildPublicTopDiscard(),
            };
        }

        // Public BLUE/persistent cards in front of a player (names only) + Black Cat.
        // Red accusation cards share the same Player.StatusCards list but are split out into
        // BuildPublicAccusationCards — same information as before the split, two clean fields.
        private static string[] BuildPublicStatusCards(Player p)
        {
            var names = new List<string>();
            if (p.StatusCards != null)
            {
                names.AddRange(p.StatusCards
                    .Where(c => c != null && c.Type != Card.CardColor.Red)
                    .Select(c => c.Name));
            }
            // Safety net for a holder whose card somehow isn't in StatusCards — but DE-DUPLICATED.
            // Player.AssignBlackCat already puts the card INTO StatusCards, and it is Blue, so the
            // sweep above normally emits it. Adding unconditionally listed "Black Cat" twice, which
            // showed as two effect badges and a bogus "×2" stack on the host seat.
            if (p.IsBlackCatHolder && !names.Contains("Black Cat")) names.Add("Black Cat");
            return names.ToArray();
        }

        // Public RED accusation cards in front of a player (names only).
        // Public by the card rules — accusations are played face-up on the table, and the aggregate
        // `accusations` int has always been broadcast. These names were already going out inside
        // statusCards before the split; this is a re-shape, not a new disclosure.
        private static string[] BuildPublicAccusationCards(Player p)
        {
            if (p.StatusCards == null) return System.Array.Empty<string>();
            return p.StatusCards
                .Where(c => c != null && c.Type == Card.CardColor.Red)
                .Select(c => c.Name)
                .ToArray();
        }

        // Printed Town Hall identity (PUBLIC — dealt face-up, read aloud at setup).
        // Reads the ASSIGNED card only. It must NEVER be filled from the ≤7-player "deal two, keep
        // one" draft pool (GameSetup) — the DISCARDED option is not public.
        // Deliberately the PRINTED name, not GetEffectiveTownHallName(): Martha Corey's copy is a
        // live derivation from already-public inputs (seat order, alive status, printed cards), so
        // clients can derive it, while the printed fact is what the table actually sees.
        private static string BuildPublicTownHall(Player p)
        {
            return p.townhallCard != null ? p.townhallCard.Name : null;
        }

        /// <summary>
        /// THE single place the PUBLIC broadcast path is allowed to touch <c>Player.TryalCards</c>.
        ///
        /// Filters to revealed FIRST, then maps to labels, so an unrevealed card is never projected
        /// to its identity at all — there is no intermediate collection holding hidden labels to be
        /// accidentally serialized. Result is sorted into a CANONICAL order so the array carries no
        /// positional information about the player's face-down cards (see the NEVER-change warning
        /// on PublicPlayerMsg.revealedTryals).
        ///
        /// ⚠ Do not inline this, do not add an overload that returns unrevealed cards, and do not
        /// widen it to return TryalCard objects. It is intentionally greppable: this method plus the
        /// `tryalTotal` count are the only public reads of TryalCards in the codebase.
        /// </summary>
        private static string[] BuildRevealedTryalLabels(Player p)
        {
            if (p.TryalCards == null) return System.Array.Empty<string>();
            var labels = p.TryalCards
                .Where(t => t != null && t.IsRevealed)
                .Select(t => LabelFor(t.TryalCardType))
                .ToList();
            labels.Sort(System.StringComparer.Ordinal); // canonical, position-free
            return labels.ToArray();
        }

        /// <summary>
        /// THE single place the PUBLIC broadcast path is allowed to touch <c>Player.HandManager</c>.
        ///
        /// Returns a COUNT and nothing else. Hand size is openly countable at a physical table; hand
        /// contents are private and belong to <see cref="BuildPrivateState"/> alone.
        ///
        /// ⚠ Same discipline as <see cref="BuildRevealedTryalLabels"/>: do not inline this, and never
        /// change the return type to anything that can carry card identities. The <c>int</c> return
        /// is the guard — a leak here would require changing the signature, which is reviewable.
        /// </summary>
        private static int BuildPublicHandCount(Player p)
        {
            if (p.HandManager == null || p.HandManager.Hand == null) return 0;
            return p.HandManager.Hand.Count;
        }

        /// <summary>
        /// Name of the TOP discard card, or null when the pile is empty. The discard pile is face-up
        /// at a physical table, so its top card is public.
        ///
        /// ⚠ TOP CARD ONLY. Never return the pile — the ordered contents would leak play history
        /// beyond what the table can see, and would hand Samuel Parris' discard-draw pool to every
        /// client. "Top" is the LAST element: AddToDiscardPile appends, and DrawFromDiscardPile reads
        /// from the end.
        /// </summary>
        private string BuildPublicTopDiscard()
        {
            if (deckManager == null) return null;

            var pile = deckManager.GetDiscardPileCards();
            if (pile == null || pile.Count == 0) return null;

            return pile[pile.Count - 1]?.Name;
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
        // Public so GameEventEmitter uses the SAME id mapping — two implementations could drift and
        // silently make log entries reference seats the board doesn't have.
        public static string PublicIdFor(Player p)
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
