/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Resolves logic when cards are played.
*   Responsibilities:
*        • Interpret effect type
*        • Trigger gameplay consequences
*   Access Requirements:
*        • DeckManager
*        • Player
*        • GameStateManager

* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using System;
using System.Collections.Generic;
using Salem.Cards;
using Salem.Data;
using Salem.Deck;
using Salem.Players;
using Salem.UI;
using UnityEngine;


namespace Salem.GameFlow
{
    public class CardEffectManager : MonoBehaviour
    {
        public static CardEffectManager Instance { get; private set; }
        // Pre-formatted prose, for the LOCAL debug log (CardLogManager) only.
        public static event Action<string> OnCardPlayed;

        // Structured (source, card, target) for consumers that must NOT receive prose — notably the
        // networked event log, whose whole privacy guarantee is that no free text crosses the wire.
        // Same moment, same data, unformatted. See GameEventKind in NetworkMessages.
        public static event Action<Player, Card, Player> OnCardPlayedDetail;
        [SerializeField] private GameManager GameManager;
        [SerializeField] private GamePhaseManager GamePhaseManager;
        [SerializeField] private DeckManager DeckManager;
        [SerializeField] private TableLayoutController tableLayoutController;
        [SerializeField, Tooltip("Canonical Witness card SO (e.g. 'Witness 1'). Cloned at runtime when " +
            "Will Grigs plays an Alibi as a Witness. MUST be wired or the conversion is a no-op.")]
        private ActionCardSO witnessTemplate;

        private Player CurrentPlayer;
        private IRng Rng => GameManager != null ? GameManager.Rng : _fallbackRng;
        private readonly IRng _fallbackRng = new XorShiftRng(1UL); // only if GM missing
        private delegate void CardOp(Player src, Player primary, Player secondary, IRng rng, ActionCardSO card);
        private Dictionary<ActionOp, CardOp> _ops;

        void OnValidate()
        {
            if (!GameManager) GameManager = FindFirstObjectByType<GameManager>();
            if (!GamePhaseManager) GamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
            if (!DeckManager) DeckManager = FindFirstObjectByType<DeckManager>();
        }
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (!GameManager) Debug.LogError("[CardEffectManager] Missing GameManager reference for RNG.");
            if (!GamePhaseManager) Debug.LogError("[CardEffectManager] Missing GamePhaseManager reference.");
            if (!DeckManager) Debug.LogError("[CardEffectManager] Missing DeckManager reference.");

            _ops = new()
                {
                    { ActionOp.Accusation, (s,t,_,_,_) => t.ApplyAccusation(0, s) },
                    { ActionOp.Evidence,   (s,t,_,_,_) => t.ApplyAccusation(0, s) },
                    { ActionOp.Witness,    (s,t,_,_,_) => t.ApplyAccusation(0, s) },
                    { ActionOp.Alibi,      (s,t,_,_,_) => {
                        if (t == null) { Debug.LogWarning("[Alibi] No target provided."); return; }
                        // Will Grigs "may choose to use alibi cards as if they were witness cards."
                        // The MODE is resolved by the input layer (a networked prompt) into the transient
                        // s.GrigsAlibiAsWitness flag BEFORE this runs. Witness mode places a PERSISTENT
                        // Witness proxy (worth 7, survives recomputes); otherwise the normal defensive
                        // Alibi removes up to 3 accusations from the target.
                        if (s.HasTownHall(TownhallName.WillGrigs) && s.GrigsAlibiAsWitness)
                            PlaceWitnessProxy(t, s);
                        else
                            t.ApplyAlibi(3);
                    }},
                    { ActionOp.Stocks,     (s,t,_,_,c) => {
                        // Stocks stays in front of the target until their turn is skipped.
                        // Use TakeCard (not RemoveCard) to avoid discarding — the card is
                        // being transferred to the target's status cards, not discarded.
                        s.HandManager.TakeCard(c);
                        t.AddStatusCard(c);
                        t.RecomputeStatusFromStatusCards();
                    }},
                    // Arson burns the target's hand. BurnHand (not ClearHand) so the cards reach the
                    // DISCARD PILE — the deck re-forms from the discard, so a bare clear would remove
                    // them from circulation for the rest of the game. Sarah Good is immune.
                    { ActionOp.Arson,      (s,t,_,_,_) => { if (!t.HasTownHall(TownhallName.SarahGood)) t.BurnHand(); } },
                    { ActionOp.Robbery,    (s,t,u,_,_) => { if (!t.HasTownHall(TownhallName.SarahGood)) t.TransferEntireHandTo(u); } },
                    { ActionOp.Scapegoat,  (s,t,u,_,_) => t.TransferAllStatusesTo(u) },
                    { ActionOp.Curse,      (s,t,_,_,_) =>
                        {
                            // Curse discards ONE blue card from the victim. The Black Cat is one option
                            // among the blue cards (it is a Blue status card), NOT forced first. A
                            // networked player CHOOSES via a card_pick sub-prompt (threaded on
                            // s.CurseChosenBlueCard); AI/local fall to a deterministic default.
                            Card chosen = s.CurseChosenBlueCard;
                            bool chosenValid = chosen != null
                                && chosen.Type == Card.CardColor.Blue
                                && t.StatusCards.Contains(chosen);
                            if (!chosenValid)
                                chosen = PickDefaultCurseTarget(t);

                            if (chosen == null)
                            {
                                Debug.Log($"[Curse] {t.PlayerNameText} has no Blue cards to discard.");
                                return;
                            }

                            bool wasPiety = chosen.Name == "Piety";

                            // Black Cat needs its dedicated bookkeeping (blackCatCard / IsBlackCatHolder);
                            // every other blue is a plain status removal.
                            if (chosen.Name == "Black Cat" && t.IsBlackCatHolder)
                            {
                                var removed = t.RemoveBlackCat(true);
                                if (removed != null) DeckManager?.AddToDiscardPile(removed);
                                Debug.Log($"[Curse] Removed Black Cat from {t.PlayerNameText}.");
                            }
                            else
                            {
                                t.RemoveStatusCard(chosen);
                                t.RecomputeStatusFromStatusCards();
                                DeckManager?.AddToDiscardPile(chosen);
                                Debug.Log($"[Curse] Removed {chosen.Name} from {t.PlayerNameText}.");
                            }

                            // Rulebook p13: losing Piety at/over threshold forces an immediate reveal,
                            // chosen by the remover (s). Fires uniformly for AI/local/networked Curse.
                            if (wasPiety)
                                t.TriggerPietyLossReveal(s);
                        }
                    },
                    { ActionOp.Asylum,     (s,t,_,_,c) => s.PlayStatusCardOnTarget(c, t) },
                    { ActionOp.Piety,      (s,t,_,_,c) => s.PlayStatusCardOnTarget(c, t) },
                    { ActionOp.Matchmaker, (s,t,_,_,c) => {
                        // Rulebook (p13): "A player cannot be given a second matchmaker card if they
                        // already have one." General refusal — the card is not placed. (This is also
                        // what keeps a spared Mary's persistent Matchmaker card safe: she can't be given
                        // a second, and a NEW copy on another player forms a legitimate fresh link.)
                        if (t.HasStatus("Matchmaker"))
                        {
                            Debug.Log($"[Matchmaker] {t.PlayerNameText} already holds a Matchmaker — " +
                                      $"cannot receive a second; play refused.");
                            return;
                        }
                        // Mary Warren IS linkable (rulebook D1) — she is immune to the elimination
                        // CHAIN, not to the link itself. Her chain immunity lives at the cascade in
                        // PlayerService.Eliminate, NOT here.
                        s.PlayStatusCardOnTarget(c, t);
                        Player.TryFormMatchmakerLink();
                    }},
                    { ActionOp.Conspiracy, (s,_,_,_,_) => Debug.LogWarning("[Conspiracy] Triggered on draw, not played.") },
                    // The Black Cat is dealt out of the deck at setup and PLACED at dawn — but it can
                    // return to the deck (a Curse discards it, the deck later re-forms from the
                    // discard) and then be drawn into a hand, at which point it is played like the
                    // blue card it is. Before this it was intercepted at draw and assigned
                    // immediately, which in networked play meant a RANDOM recipient: the "let the
                    // drawer choose" branch was gated on IsLocalPlayer, and no one is local when
                    // every human is remote.
                    { ActionOp.BlackCat,   (s,t,_,_,c) => s.GiveBlackCatTo(c, t) },
                };
        }

        #region Helper Functions
        public bool HandleCardDrawn(Player drawer, Card card)
        {
            if (card == null)
            {
                return false;
            }

            if (card.Name == "Night")
            {
                Debug.Log($"[Effect] Night card drawn by {drawer?.PlayerNameText ?? "Unknown"}.");
                GamePhaseManager?.HandleNightCardDrawn(card);
                return true;
            }


            if (card.Name == "Conspiracy")
            {
                Debug.Log($"[Effect] Conspiracy card drawn by {drawer?.PlayerNameText ?? "Unknown"}.");
                DeckManager?.AddToDiscardPile(card);
                GamePhaseManager?.HandleConspiracyCardDrawn(drawer);
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Play a card. Returns TRUE only if the effect actually ran; FALSE on every rejection
        /// (wrong phase, invalid primary/secondary target, 2-player disable).
        ///
        /// ⚠ CALLERS MUST HONOUR THE RETURN VALUE before consuming the card. A `void` early-return used
        /// to be indistinguishable from success, so `NetworkInput` discarded the card anyway — that is
        /// exactly how Robbery "did nothing but vanished from my hand."
        ///
        /// `secondary` is the second target for two-target ops (Robbery's recipient, Scapegoat's
        /// destination). It is passed BY PARAMETER — never stored on the card. `ActionCardSO` is a
        /// shared project asset, so writing the recipient onto it leaked state across plays and
        /// between copies of the same card.
        /// </summary>
        public bool ExecuteCardEffect(Card card, Player target, Player secondary = null)
        {
            var phaseMgr = FindFirstObjectByType<GamePhaseManager>();
            if (phaseMgr != null && phaseMgr.CurrentPhase != GamePhase.Day)
            {
                Debug.LogWarning($"[Effect] Ignored {card.Name}: not in Day phase.");
                return false;
            }

            UpdateCurrentPlayer();
            //Debug.Log($"[Effect] Executing {card.Name} on {target?.PlayerNameText ?? "N/A"}");

            if (card is ActionCardSO ac)
            {
                // Playable at all right now? (2-player disable for Robbery/Scapegoat.) Host-side
                // enforcement — the phone is also told not to offer these, but never trust the client.
                int aliveCount = PlayerService.GetAlivePlayers().Count;
                if (!Salem.Rules.TargetingPolicy.ValidatePlayable(ac.Op, aliveCount, out var whyNot))
                {
                    Debug.LogWarning($"[{ac.Op}] {whyNot}");
                    return false;
                }

                // Primary target check
                if (ac.NeedsTarget)
                {
                    if (!Salem.Rules.TargetingPolicy.ValidatePrimary(CurrentPlayer, target, ac.Op, out var why))
                    {
                        Debug.LogWarning($"[{ac.Op}] {why}");
                        return false;
                    }
                }

                // Secondary target check for two-target ops. The caller supplies it (the playing
                // player chose it); it is NOT read off the card asset any more.
                if (ac.RequiresSecondTarget)
                {
                    if (!Salem.Rules.TargetingPolicy.ValidateSecondary(CurrentPlayer, target, secondary, ac.Op, out var why2))
                    {
                        Debug.LogWarning($"[{ac.Op}] {why2}");
                        return false;
                    }
                }
            }

            // Red cards: place in front of target BEFORE executing effect
            // (so they're tracked when threshold check runs)
            // Use TakeCard (not RemoveCard) to avoid discarding — the card is
            // being transferred to the target's status cards, not discarded.
            if (card.Type == Card.CardColor.Red && target != null)
            {
                CurrentPlayer.HandManager.TakeCard(card);
                target.AddStatusCard(card);
            }

            if (card is ActionCardSO action)
            {
                ExecuteActionOp(action, target, secondary);
            }
            else
            {
                Debug.LogWarning($"[Effect] Non-action card played via effect path: {card.Name}");
            }

            // Green cards: remove from hand after effect (goes to discard)
            // Exception: Stocks stays in front of target (already handled in its op)
            if (card.Type == Card.CardColor.Green && card is ActionCardSO greenAc && greenAc.Op != ActionOp.Stocks)
                CurrentPlayer.HandManager.RemoveCard(card);

            // Raise event for CardLogManager to listen to
            OnCardPlayed?.Invoke(CardLogFormatter.Format(CurrentPlayer, card, target));
            OnCardPlayedDetail?.Invoke(CurrentPlayer, card, target);

            GameTurnManager.Instance.NotifyCardPlayed(CurrentPlayer);
            return true;
        }

        /// <summary>
        /// Will Grigs' "use this Alibi as a Witness": place a PERSISTENT Witness on the target so it
        /// behaves exactly like a real Witness card — worth 7 in RecalculateAccusations, transferable by
        /// Scapegoat, ignored by Curse (red, not blue), etc. A one-shot ApplyAccusation(7) bonus would
        /// evaporate on the next recompute; a real card does not.
        ///
        /// Only ONE Witness SO asset exists (shared across the deck), so we clone a RUNTIME copy
        /// (IsRuntimeInstance) that DeckManager.AddToDiscardPile destroys instead of recycling. The
        /// played Alibi is discarded normally by the generic green-card removal and returns to the deck.
        /// </summary>
        private void PlaceWitnessProxy(Player target, Player accuser)
        {
            if (witnessTemplate == null)
            {
                Debug.LogError("[Grigs] witnessTemplate is not wired on CardEffectManager — cannot place " +
                    "the Witness proxy. Falling back to a transient +7 (will not persist).");
                target.ApplyAccusation(7, accuser); // degraded fallback so the play isn't a silent no-op
                return;
            }

            var proxy = Instantiate(witnessTemplate); // fresh runtime instance, NOT the shared asset
            proxy.IsRuntimeInstance = true;
            target.AddStatusCard(proxy);
            // Recompute from status cards (now includes the +7 Witness) and run the threshold check —
            // the same path a real Witness play uses (ExecuteActionOp Witness → ApplyAccusation(0)).
            target.ApplyAccusation(0, accuser);
            Debug.Log($"[TownHall] Will Grigs ({accuser.PlayerNameText}) played an Alibi as a Witness (+7 persistent) on {target.PlayerNameText}.");
        }

        // `secondary` comes from the caller (the playing player's choice), never from action.target —
        // that field is a shared asset and writing to it leaked state between plays.
        /// <summary>
        /// Deterministic blue-card pick for a Curse when the player did not explicitly choose (AI, local
        /// host, or a timed-out/declined networked prompt). Priority: Piety when stripping it forces an
        /// immediate reveal (max tempo — see <see cref="Player.PietyRemovalWouldReveal"/>), then Asylum
        /// (removes night protection), then Matchmaker, then whatever is first (Black Cat falls out here
        /// if it is the only blue). Uses List.FindAll/Find — no LINQ dependency.
        /// </summary>
        private Card PickDefaultCurseTarget(Player t)
        {
            var blues = t.StatusCards.FindAll(c => c.Type == Card.CardColor.Blue);
            if (blues.Count == 0) return null;

            if (t.PietyRemovalWouldReveal())
            {
                var piety = blues.Find(c => c.Name == "Piety");
                if (piety != null) return piety;
            }
            var asylum = blues.Find(c => c.Name == "Asylum");
            if (asylum != null) return asylum;
            var matchmaker = blues.Find(c => c.Name == "Matchmaker");
            if (matchmaker != null) return matchmaker;
            return blues[0];
        }

        private void ExecuteActionOp(ActionCardSO action, Player target, Player secondary)
        {
            //Debug.Log(action.Op.ToString() );
            if (_ops.TryGetValue(action.Op, out var op))
                op(CurrentPlayer, target, secondary, Rng, action);
            else
                Debug.LogWarning($"[Effect] Unhandled op {action.Op}");
        }

        /// <summary>
        /// The chooser picks which face-down tryal turns. Serves BOTH the accusation threshold and
        /// the piety-loss reveal (TriggerPietyLossReveal routes through the same event).
        ///
        /// ⚠ THIS METHOD IS SYNCHRONOUS — it is an event handler on a chain that runs inside
        /// ExecuteCardEffect, so it cannot yield to await a phone. That is why a NETWORKED chooser
        /// is handed off via a pending flag (drained by NetworkInput.RunTurn a beat later, still
        /// their turn) rather than prompted here. Same shape, and for the same reason, as
        /// Abigail Williams' PendingAbigailDiscardChoice.
        /// </summary>
        private void HandleAccusationRevealChoice(Player accused, Player accuser, string reason)
        {
            if (accused == null) return;

            // Networked chooser: defer to their turn loop, which CAN await the prompt.
            if (accuser != null && accuser.Input is NetworkInput)
            {
                accuser.PendingTryalRevealTarget = accused;
                accuser.PendingTryalRevealReason = reason;
                return;
            }

            // Both remaining branches route the FLIP through GamePhaseManager so it lands as a
            // synchronized beat on the host and every mirror, instead of appearing in board state
            // with no animation. Fire-and-forget because this method cannot yield; offline that is
            // still immediate, since EmitSynchronizedReveal short-circuits when not networked.
            // NB: the serialized field is named the same as its type, so `GamePhaseManager.Instance`
            // here would lean on C#'s "Color Color" rule to mean the static. Use the already-resolved
            // field instead — same object, no ambiguity for the next reader.
            var gpm = GamePhaseManager;

            // If the accuser is a local human, let them choose which Tryal to reveal
            if (accuser != null && accuser.IsHuman && accuser.IsLocalPlayer && tableLayoutController != null)
            {
                tableLayoutController.BeginTryalSelection(accused, idx =>
                {
                    if (gpm != null) gpm.RevealTryalSynchronized(accused, idx);
                    else accused.RevealTryalCard(idx, fromAccusation: true);
                });
            }
            else
            {
                // AI or fallback: reveal a random unrevealed Tryal
                var rng = accuser?.Rng ?? Rng;
                int? idx = accused.GetRandomUnrevealedTryalIndex(rng);
                if (idx.HasValue)
                {
                    if (gpm != null) gpm.RevealTryalSynchronized(accused, idx.Value);
                    else accused.RevealTryalCard(idx.Value, fromAccusation: true);
                }
            }
        }

        private void OnEnable()
        {
            Player.OnAccusationRevealNeeded += HandleAccusationRevealChoice;
        }

        private void OnDisable()
        {
            Player.OnAccusationRevealNeeded -= HandleAccusationRevealChoice;
        }

        private void UpdateCurrentPlayer()
        {
            var players = PlayerService.GetAlivePlayers();
            if (GameTurnManager.CurrentPlayerIndex < players.Count)
            {
                CurrentPlayer = players[GameTurnManager.CurrentPlayerIndex];
            }
        }
        #endregion
    }
}
