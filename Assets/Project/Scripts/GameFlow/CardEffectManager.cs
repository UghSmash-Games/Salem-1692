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

using UnityEngine;
using Salem.Players;
using Salem.Cards;
using Salem.Data;
using System;
using System.Linq;
using System.Collections.Generic;
using Salem.UI;


namespace Salem.GameFlow
{
    public class CardEffectManager : MonoBehaviour
    {
        public static CardEffectManager Instance { get; private set; }
        public static event Action<string> OnCardPlayed;
        [SerializeField] private TargetPickerUI TargetPicker;
        [SerializeField] private TryalPickerUI TryalPicker;

        private Player CurrentPlayer;
        private IRng rng;


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            rng = new XorShiftRng((ulong)System.DateTime.UtcNow.Ticks);
        }

        #region Helper Functions
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

                //Prepare message
                string message = FormatCardLogMessage(card, target);
                // Raise event for CardLogManager to listen to
                OnCardPlayed?.Invoke(message);
                if(CurrentPlayer.IsHuman)
                    GameTurnManager.Instance.OnHumanActionResolved();
                GameTurnManager.Instance.EndTurn();
        }

        private void ExecuteActionOp(ActionCardSO action, Player target)
        {           
            switch (action.Op)
            {
                case ActionOp.Accusation:
                    target?.ApplyAccusation(1);
                    break;
                case ActionOp.Evidence:
                    target?.ApplyAccusation(CurrentPlayer.PlayerNameText == "Cotton Mather" ? 1 : 3);
                    break;
                case ActionOp.Witness:
                    target?.ApplyAccusation(7);
                    break;
                case ActionOp.Alibi:
                    CurrentPlayer.ApplyAlibi(3);
                    break;
                case ActionOp.Stocks:
                    target?.ApplyStocks(1);
                    break;
                case ActionOp.Arson:
                    if (target == null) { Debug.LogWarning("[Arson] Target required."); break; }
                    if (target.PlayerNameText == "Sarah Good") { Debug.Log("[Arson] Sarah Good is immune."); break; }
                    target.ClearHand();
                    break;
                case ActionOp.Robbery:
                    // target = victim (not self), action.target = recipient (not self, not victim)
                    var recipient = action.target;
                    target.TransferEntireHandTo(recipient);
                    break;
                case ActionOp.Scapegoat:
                    // target = status donor, action.target = receiver
                    target.TransferAllStatusesTo(action.target);
                    break;
                case ActionOp.Curse:
                    if (target == null) { Debug.LogWarning("[Curse] Target required."); break; }
                    target.AddStatusCardAndRecompute(action);
                    break;
                case ActionOp.Asylum:
                    CurrentPlayer.PlayStatusCardOnTarget(action, target);
                    break;
                case ActionOp.Piety:
                    CurrentPlayer.PlayStatusCardOnTarget(action, target);
                    break;
                case ActionOp.Matchmaker:
                    CurrentPlayer.PlayStatusCardOnTarget(action, target);
                    Player.TryFormMatchmakerLink(); // links two different holders
                    break;
                case ActionOp.BlackCat:
                    // not played from hand; ignore here
                    Debug.LogWarning("[Black Cat] Not a playable action; it is assigned at Dawn by witches’ vote.");
                    break;
                case ActionOp.Conspiracy:
                    ExecuteConspiracy(rng);
                    break;
                default:
                    Debug.LogWarning($"[Effect] Unhandled op {action.Op}");
                    break;
            }
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


        private string FormatCardLogMessage(Card card, Player target)
        {
            // Supports dynamic substitution if used
            string sourceName = CurrentPlayer.PlayerNameText;
            string targetName = target?.PlayerNameText ?? "no target";


            if (string.IsNullOrEmpty(card.LogMessage))
                return $"{sourceName} played {card.name} on {targetName}";


            // Optional: Replace placeholders in the log message
            return card.LogMessage
                .Replace("{source}", sourceName)
                .Replace("{card}", card.Name)
                .Replace("{target}", targetName);
        }
        #endregion
    }
}
