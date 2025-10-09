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
using System.Linq;
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
        [SerializeField] private TargetPickerUI TargetPicker;
        [SerializeField] private TryalPickerUI TryalPicker;
        [SerializeField] private GameManager GameManager;
        [SerializeField] private GamePhaseManager GamePhaseManager;
        [SerializeField] private DeckManager DeckManager;

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
                    { ActionOp.Accusation, (s,t,_,_,_) => t.ApplyAccusation(1) },
                    { ActionOp.Evidence,   (s,t,_,_,_) => t.ApplyAccusation(t.PlayerNameText=="Cotton Mather" ? 1 : 3) },
                    { ActionOp.Witness,    (s,t,_,_,_) => t.ApplyAccusation(7) },
                    { ActionOp.Alibi,      (s,_,_,_,_) => s.ApplyAlibi(3) },
                    { ActionOp.Stocks,     (s,t,_,_,_) => t.ApplyStocks(1) },
                    { ActionOp.Arson,      (s,t,_,_,_) => { if (t.PlayerNameText!="Sarah Good") t.ClearHand(); } },
                    { ActionOp.Robbery,    (s,t,u,_,_) => t.TransferEntireHandTo(u) },
                    { ActionOp.Scapegoat,  (s,t,u,_,_) => t.TransferAllStatusesTo(u) },
                    { ActionOp.Curse,      (s,t,_,_,c) =>
                        {
                            var removed = t.RemoveBlackCat(false);
                            if (removed != null)
                            {
                                DeckManager?.AddToDiscardPile(removed);
                            }
                            t.AddStatusCardAndRecompute(c);
                        }
                    },
                    { ActionOp.Asylum,     (s,t,_,_,c) => s.PlayStatusCardOnTarget(c, t) },
                    { ActionOp.Piety,      (s,t,_,_,c) => s.PlayStatusCardOnTarget(c, t) },
                    { ActionOp.Matchmaker, (s,t,_,_,c) => { s.PlayStatusCardOnTarget(c, t); Player.TryFormMatchmakerLink(); } },
                    { ActionOp.Conspiracy, (s,_,_,rng,_) => ExecuteConspiracy(rng) },
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
                GamePhaseManager?.HandleNightCardDrawn();
                return true;
            }

            if (card.Name == "Black Cat")
            {
                if (drawer != null)
                {
                    drawer.AssignBlackCat(card);
                }
                else
                {
                    Debug.LogWarning("[Effect] Black Cat drawn but no player was provided. Card will be discarded.");
                    DeckManager?.AddToDiscardPile(card);
                }
                return true;
            }

            return false;
        }
        public void ExecuteCardEffect(Card card, Player target)
        {
            UpdateCurrentPlayer();
            Debug.Log($"[Effect] Executing {card.Name} on {target?.PlayerNameText ?? "N/A"}");

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

            if (card is ActionCardSO action)
            {
                ExecuteActionOp(action, target);
            }
            else
            {
                Debug.LogWarning($"[Effect] Non-action card played via effect path: {card.Name}");
            }

            // Remove from hand if appropriate
            if (card.Type == Card.CardColor.Green || card.Type == Card.CardColor.Red)
                CurrentPlayer.HandManager.RemoveCard(card);

            // Raise event for CardLogManager to listen to
            OnCardPlayed?.Invoke(CardLogFormatter.Format(CurrentPlayer, card, target));
            
            GameTurnManager.Instance.NotifyCardPlayed(CurrentPlayer);
        }

        private void ExecuteActionOp(ActionCardSO action, Player target)
        {          
            Debug.Log(action.Op.ToString() );
            var secondary = action.RequiresSecondTarget ? action.target : null;
            if (_ops.TryGetValue(action.Op, out var op))
                op(CurrentPlayer, target, secondary, Rng, action);
            else
                Debug.LogWarning($"[Effect] Unhandled op {action.Op}");
        }

        private void ExecuteConspiracy(IRng rng)
        {
            var alive = PlayerService.GetAlivePlayers();
            var blackCat = alive.Find(p => p.IsBlackCatHolder);

            void AfterReveal()
            {
                ExecuteConspiracySwap(rng); // your existing swap code
            }

            if (blackCat != null && CurrentPlayer.IsLocalPlayer && TryalPicker != null)
            {
                // local drawer chooses which Tryal the Black Cat reveals
                TryalPicker.Open(blackCat, idx => { blackCat.RevealTryalCard(idx); AfterReveal(); });
            }
            else
            {
                // fallback: RNG choice
                var choices = new List<int>();
                for (int i = 0; i < blackCat?.TryalCards.Count; i++)
                    if (!blackCat.TryalCards[i].IsRevealed) choices.Add(i);
                if (blackCat != null && choices.Count > 0)
                    blackCat.RevealTryalCard(choices[rng.NextInt(0, choices.Count)]);
                AfterReveal();
            }
        }
        private void ExecuteConspiracySwap(IRng rng)
        {
            // Candidates: alive players who have at least one unrevealed Tryal
            var candidates = PlayerService.GetAlivePlayers()
                .Where(p => p.TryalCards != null && p.TryalCards.Any(tc => !tc.IsRevealed))
                .ToList();

            if (candidates.Count < 2)
            {
                Debug.LogWarning("[Conspiracy] Not enough candidates to swap Tryal cards.");
                return;
            }

            // Pick two distinct players deterministically
            int aIndex = rng.NextInt(0, candidates.Count);
            int bIndex = aIndex;
            // Ensure bIndex != aIndex
            if (candidates.Count > 1)
                while (bIndex == aIndex) bIndex = rng.NextInt(0, candidates.Count);

            var playerA = candidates[aIndex];
            var playerB = candidates[bIndex];

            // Pick one unrevealed Tryal index for each
            var aTryalIndex = playerA.GetRandomUnrevealedTryalIndex(rng);
            var bTryalIndex = playerB.GetRandomUnrevealedTryalIndex(rng);

            if (aTryalIndex == null || bTryalIndex == null)
            {
                Debug.LogWarning("[Conspiracy] Could not find unrevealed Tryal indices for both players.");
                return;
            }

            // Remove selected cards
            var aCard = playerA.RemoveTryalAt(aTryalIndex.Value);
            var bCard = playerB.RemoveTryalAt(bTryalIndex.Value);

            if (aCard == null || bCard == null)
            {
                Debug.LogWarning("[Conspiracy] Null Tryal card during removal—swap aborted.");
                // Try to roll back if needed (edge case), but we only removed if not null
                if (aCard != null) playerA.AddTryalCardAndNotify(aCard);
                if (bCard != null) playerB.AddTryalCardAndNotify(bCard);
                return;
            }

            // Swap
            playerA.AddTryalCardAndNotify(bCard);
            playerB.AddTryalCardAndNotify(aCard);

            Debug.Log($"[Conspiracy] Swapped unrevealed Tryals between {playerA.PlayerNameText} and {playerB.PlayerNameText}.");
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
