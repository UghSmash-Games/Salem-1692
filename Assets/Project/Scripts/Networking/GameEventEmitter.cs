using Salem.Cards;
using Salem.Data;
using Salem.GameFlow;
using Salem.Players;
using UnityEngine;

namespace Salem.Networking
{
    /// <summary>
    /// Translates game events into PUBLIC <see cref="GameEventMsg"/> entries for the "What Has
    /// Passed" log on the host screen and mirrors.
    ///
    /// TIER: this class lives in Salem.Networking, which may legitimately read Player models. The
    /// RENDERER (Salem.UI.HostDisplay) may not — it only ever sees the emitted DTO. That split is
    /// what keeps the host screen's masking structural.
    ///
    /// 🔴 THE PRIVACY MODEL — read <see cref="GameEventKind"/> first.
    /// This is the ONLY place log entries are created, and it can only emit the closed set of kinds.
    /// It carries ids and short public labels; it never carries a sentence. The prose lives in the
    /// renderer, so a secret cannot be smuggled into the log even by a careless call site.
    ///
    /// ⛔ Do NOT subscribe this class to any secret-phase source: witch votes, constable saves,
    /// black-cat placement, fellow-witch reveals, or in-window confessions. It deliberately
    /// subscribes only to sources whose effects are already public in game_state_update.
    ///
    /// NOTE ON <see cref="CardEffectManager.OnCardPlayed"/>: that event carries a PRE-FORMATTED
    /// prose string (for the local debug log). This class deliberately uses the structured
    /// OnCardPlayedDetail sibling instead — putting the formatted string on the wire would hand
    /// every future call site a free-text channel and destroy the guarantee above.
    /// </summary>
    public class GameEventEmitter : MonoBehaviour
    {
        [SerializeField] private GamePhaseManager gamePhaseManager;
        [SerializeField] private GameManager gameManager;

        private void Awake()
        {
            if (!gamePhaseManager) gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
            if (!gameManager) gameManager = FindFirstObjectByType<GameManager>();
        }

        private void OnEnable()
        {
            CardEffectManager.OnCardPlayedDetail += HandleCardPlayed;
            Player.TryalCardRevealed += HandleTryalRevealed;
            PlayerService.OnPlayerEliminated += HandleEliminated;
            TrialService.OnDoubleWitchRevealed += HandleDoubleWitch;
            GamePhaseManager.OnConfessionRevealed += HandleConfessionRevealed;
            GamePhaseManager.OnGavelPlaced += HandleGavelPlaced;
            GamePhaseManager.OnGameStarted += HandleGameStarted;
            GameTurnManager.CardsDrawn += HandleCardsDrawn;

            if (gamePhaseManager != null) gamePhaseManager.OnPhaseChange += HandlePhaseChange;
            if (gameManager != null) gameManager.OnGameEnded += HandleGameEnded;
        }

        private void OnDisable()
        {
            CardEffectManager.OnCardPlayedDetail -= HandleCardPlayed;
            Player.TryalCardRevealed -= HandleTryalRevealed;
            PlayerService.OnPlayerEliminated -= HandleEliminated;
            TrialService.OnDoubleWitchRevealed -= HandleDoubleWitch;
            GamePhaseManager.OnConfessionRevealed -= HandleConfessionRevealed;
            GamePhaseManager.OnGavelPlaced -= HandleGavelPlaced;
            GamePhaseManager.OnGameStarted -= HandleGameStarted;
            GameTurnManager.CardsDrawn -= HandleCardsDrawn;

            if (gamePhaseManager != null) gamePhaseManager.OnPhaseChange -= HandlePhaseChange;
            if (gameManager != null) gameManager.OnGameEnded -= HandleGameEnded;
        }

        // ─── Handlers ──────────────────────────────────────────────

        private void HandleGameStarted()
        {
            // No actor, no target, no value. The deal itself is the news; WHAT was dealt is the
            // game's central secret. The renderer supplies a fixed line.
            Emit(GameEventKind.GameStarted);
        }

        /// <summary>
        /// The COUNT only — never what was drawn. Exists so the audio layer has a public event for
        /// the card-draw cue; both log renderers drop it on purpose (see GameEventKind.CardsDrawn).
        /// </summary>
        private void HandleCardsDrawn(Player drawer, int count)
        {
            Emit(GameEventKind.CardsDrawn, actor: drawer, value: count.ToString());
        }

        private void HandleCardPlayed(Player source, Card card, Player target)
        {
            Emit(GameEventKind.CardPlayed,
                 actor: source, target: target,
                 cardName: card != null ? card.Name : null);
        }

        // NOTE: deliberately NOT subscribed to Player.AccusationCountChanged. Three of its four call
        // sites are RESETS to zero (Abigail's discard, an Alibi removal, the red discard after a
        // reveal), so it would spam "0/7" after every reveal and mislabel removals as additions.
        // The accusation is already logged as CardPlayed; the running total lives on the seat.

        private void HandleTryalRevealed(Player p, TryalCard card)
        {
            // A reveal is public by definition — this is the same fact the board already shows via
            // PublicPlayerMsg.revealedTryals.
            Emit(GameEventKind.TryalRevealed, actor: null, target: p, value: LabelFor(card));
        }

        private void HandleEliminated(Player p, EliminationCause cause)
        {
            Emit(GameEventKind.PlayerEliminated, actor: null, target: p, value: cause.ToString());
        }

        private void HandleConfessionRevealed(Player p, TryalCard card)
        {
            // Fires only at the synchronized revealAt, when the flip is public by the rulebook.
            // There is deliberately NO kind for the confess WINDOW itself — that would leak who
            // confessed while the masked timing is still protecting them.
            Emit(GameEventKind.ConfessionRevealed, actor: null, target: p, value: LabelFor(card));
        }

        private void HandleGavelPlaced(Player recipient)
        {
            // ⛔ target ONLY. `actor` stays null on purpose: naming the constable would publish a
            // secret role, and the physical token doesn't reveal who placed it either.
            Emit(GameEventKind.GavelPlaced, actor: null, target: recipient);
        }

        private void HandleDoubleWitch(Player p)
        {
            Emit(GameEventKind.DoubleWitchRevealed, actor: null, target: p);
        }

        private void HandlePhaseChange(GamePhase phase)
        {
            Emit(GameEventKind.PhaseChanged, actor: null, target: null, value: phase.ToString());
        }

        private void HandleGameEnded(EndGameResult result)
        {
            // 🐛 WAS `result.ToString()`. EndGameResult is a CLASS with no ToString() override, so
            // that emitted the literal type name "Salem.Data.EndGameResult" — which matches neither
            // "witch" nor "town"/"village" in either renderer's DescribeWinner, so the game-over
            // entry was silently DROPPED from the log on the host and every mirror. The winning
            // TEAM is the enumerable public fact this field is for.
            // ⚠ Never emit `result` itself or `result.Reason`: Reason is free prose, and free text
            // on a log event is the one thing GameEventKind's closed vocabulary exists to prevent.
            Emit(GameEventKind.GameOver, actor: null, target: null,
                 value: result != null ? result.WinningTeam.ToString() : null);
        }

        // ─── Emit ──────────────────────────────────────────────────

        private static void Emit(GameEventKind kind, Player actor = null, Player target = null,
                                 string cardName = null, string value = null)
        {
            var nm = NetworkManager.Instance;
            if (nm == null) return;

            nm.SendGameEvent(new GameEventMsg
            {
                kind = WireName(kind),
                actorId = actor != null ? NetworkStateBroadcaster.PublicIdFor(actor) : null,
                targetId = target != null ? NetworkStateBroadcaster.PublicIdFor(target) : null,
                cardName = cardName,
                value = value,
                // Stamped HERE, once, by the host. Clients format it in their own local time.
                atMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
        }

        /// <summary>PascalCase enum → lowercase snake_case wire string ("TryalRevealed" → "tryal_revealed").</summary>
        private static string WireName(GameEventKind kind)
        {
            var name = kind.ToString();
            var sb = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i])) sb.Append('_');
                sb.Append(char.ToLowerInvariant(name[i]));
            }
            return sb.ToString();
        }

        private static string LabelFor(TryalCard card)
        {
            if (card == null) return null;
            switch (card.TryalCardType)
            {
                case TryalCardType.Witch: return "Witch";
                case TryalCardType.Constable: return "Constable";
                default: return "Not a Witch";
            }
        }
    }
}
