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
                    { ActionOp.Arson,      (s,t,_,_,_) => { if (!t.HasTownHall(TownhallName.SarahGood)) t.ClearHand(); } },
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
                        // Mary Warren is immune to Matchmaker
                        if (t.HasTownHall(TownhallName.MaryWarren))
                        {
                            Debug.Log($"[TownHall] Mary Warren ({t.PlayerNameText}) is immune to Matchmaker.");
                            return;
                        }
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
                            target != drawer &&
                            !target.HasTownHall(TownhallName.MaryWarren),
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
        
        public void ExecuteCardEffect(Card card, Player target)
        {
            var phaseMgr = FindFirstObjectByType<GamePhaseManager>();
            if (phaseMgr != null && phaseMgr.CurrentPhase != GamePhase.Day)
            {
                Debug.LogWarning($"[Effect] Ignored {card.Name}: not in Day phase.");
                return;
            }

            UpdateCurrentPlayer();
            //Debug.Log($"[Effect] Executing {card.Name} on {target?.PlayerNameText ?? "N/A"}");

            if (card is ActionCardSO ac)
            {
                // Primary target check
                if (ac.NeedsTarget)
                {
                    if (!Salem.Rules.TargetingPolicy.ValidatePrimary(CurrentPlayer, target, ac.Op, out var why))
                    {
                        Debug.LogWarning($"[{ac.Op}] {why}");
                        return;
                    }
                }

                // Secondary target check for two-target ops
                if (ac.RequiresSecondTarget)
                {
                    var secondary = ac.target; // we store the second target in card.target
                    if (!Salem.Rules.TargetingPolicy.ValidateSecondary(CurrentPlayer, target, secondary, ac.Op, out var why2))
                    {
                        Debug.LogWarning($"[{ac.Op}] {why2}");
                        return;
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
                ExecuteActionOp(action, target);
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
        }

        private void ExecuteActionOp(ActionCardSO action, Player target)
        {          
            //Debug.Log(action.Op.ToString() );
            var secondary = action.RequiresSecondTarget ? action.target : null;
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
