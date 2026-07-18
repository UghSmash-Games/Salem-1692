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
        public static event Action<string> OnCardPlayed;
        [SerializeField] private GameManager GameManager;
        [SerializeField] private GamePhaseManager GamePhaseManager;
        [SerializeField] private DeckManager DeckManager;
        [SerializeField] private TableLayoutController tableLayoutController;

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
                        // Will Griggs: Alibi can be used offensively as a Witness (+7 accusations on target)
                        if (t != null && s.HasTownHall(TownhallName.WillGrigs))
                            t.ApplyAccusation(7, s);
                        else if (t != null)
                            t.ApplyAlibi(3);
                        else
                            Debug.LogWarning("[Alibi] No target provided.");
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
                    { ActionOp.Curse,      (s,t,_,_,c) =>
                        {
                            // Discard one Blue status card from the target
                            if (t.IsBlackCatHolder)
                            {
                                var removed = t.RemoveBlackCat(true);
                                if (removed != null)
                                    DeckManager?.AddToDiscardPile(removed);
                            }
                            else
                            {
                                var blueStatus = t.StatusCards.Find(sc => sc.Type == Card.CardColor.Blue);
                                if (blueStatus != null)
                                {
                                    t.RemoveStatusCard(blueStatus);
                                    t.RecomputeStatusFromStatusCards();
                                    DeckManager?.AddToDiscardPile(blueStatus);
                                    Debug.Log($"[Curse] Removed {blueStatus.Name} from {t.PlayerNameText}.");
                                }
                                else
                                {
                                    Debug.Log($"[Curse] {t.PlayerNameText} has no Blue cards to discard.");
                                }
                            }
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
                    { ActionOp.BlackCat,   (s,_,_,_,_) => Debug.LogWarning("[Black Cat] Assigned at Dawn, not played.") },
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

            if (card.Name == "Black Cat")
            {
                if (drawer == null)
                {
                    Debug.LogWarning("[Effect] Black Cat drawn but no player was provided. Card will be discarded.");
                    DeckManager?.AddToDiscardPile(card);
                    return true;
                }

                if (drawer.IsHuman && drawer.IsLocalPlayer && tableLayoutController != null)
                {
                    tableLayoutController.BeginTargetSelection(
                        drawer,
                        "Choose a player to receive Black Cat.",
                        target =>
                            target != null &&
                            !target.IsEliminated &&
                            target != drawer, // Mary Warren CAN be given the Black Cat (immune to its effect, not refused)
                        target =>
                        {
                            target.AssignBlackCat(card);
                            Debug.Log($"[Black Cat] {drawer.PlayerNameText} assigned Black Cat to {target.PlayerNameText}.");
                        }
                    );
                }
                else
                {
                    Player target = AITargetingHelper.SelectRandomTarget(drawer);

                    if (target != null)
                    {
                        target.AssignBlackCat(card);
                    }
                    else
                    {
                        DeckManager?.AddToDiscardPile(card);
                    }
                }

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

            GameTurnManager.Instance.NotifyCardPlayed(CurrentPlayer);
            return true;
        }

        // `secondary` comes from the caller (the playing player's choice), never from action.target —
        // that field is a shared asset and writing to it leaked state between plays.
        private void ExecuteActionOp(ActionCardSO action, Player target, Player secondary)
        {
            //Debug.Log(action.Op.ToString() );
            if (_ops.TryGetValue(action.Op, out var op))
                op(CurrentPlayer, target, secondary, Rng, action);
            else
                Debug.LogWarning($"[Effect] Unhandled op {action.Op}");
        }

         private void HandleAccusationRevealChoice(Player accused, Player accuser)
        {
            if (accused == null) return;

            // If the accuser is a local human, let them choose which Tryal to reveal
            if (accuser != null && accuser.IsHuman && accuser.IsLocalPlayer && tableLayoutController != null)
            {
                tableLayoutController.BeginTryalSelection(accused, idx =>
                {
                    accused.RevealTryalCard(idx, fromAccusation: true);
                });
            }
            else
            {
                // AI or fallback: reveal a random unrevealed Tryal
                var rng = accuser?.Rng ?? Rng;
                int? idx = accused.GetRandomUnrevealedTryalIndex(rng);
                if (idx.HasValue)
                    accused.RevealTryalCard(idx.Value, fromAccusation: true);
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
