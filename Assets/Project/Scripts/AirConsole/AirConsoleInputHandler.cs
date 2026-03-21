/*
* AUTHOR: Claude Code
* REFERENCES: AirConsole Unity Plugin API (NDream.AirConsole)
* NOTES:
*   Primary Purpose: Processes incoming controller messages and translates them into game actions.
*   Responsibilities:
*        • Parse controller messages by action type
*        • Validate that actions are legal for the current turn state
*        • Call into GameTurnManager and CardEffectManager to execute actions
*        • Manage pending target selection state
*   Access Requirements:
*        • AirConsoleManager
*        • GameTurnManager
*        • CardEffectManager
*        • Player / HandManager

* TODO: Add timeout for pending target selections
* FIXME: [Known bugs or issues]
*/
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Salem.Cards;
using Salem.Data;
using Salem.GameFlow;
using Salem.Players;
using Newtonsoft.Json.Linq;

namespace Salem.AirConsole
{
    public class AirConsoleInputHandler : MonoBehaviour
    {
        // Tracks pending target selection when a played card requires a target
        private Player pendingTargetPlayer;
        private ActionCardSO pendingCard;
        private List<Player> pendingValidTargets;
        private bool pendingNeedsSecondTarget;
        private Player pendingPrimaryTarget;

        /// <summary>
        /// Called by AirConsoleManager when a message arrives from a controller.
        /// Routes the message to the appropriate handler based on the action field.
        /// </summary>
        public void ProcessMessage(Player player, string action, JToken data)
        {
            switch (action)
            {
                case "play_card":
                    HandlePlayCard(player, data);
                    break;
                case "select_target":
                    HandleSelectTarget(player, data);
                    break;
                case "draw_cards":
                    HandleDrawCards(player);
                    break;
                case "end_turn":
                    HandleEndTurn(player);
                    break;
                default:
                    Debug.LogWarning($"[AirConsoleInput] Unknown action: {action}");
                    break;
            }
        }

        private void HandlePlayCard(Player player, JToken data)
        {
            int cardIndex = data["cardIndex"]?.ToObject<int>() ?? -1;
            if (cardIndex < 0 || cardIndex >= player.HandManager.Hand.Count)
            {
                Debug.LogWarning($"[AirConsoleInput] Invalid card index {cardIndex} from {player.PlayerNameText}.");
                return;
            }

            if (GameTurnManager.Instance == null || !GameTurnManager.Instance.TryBeginPlayPhase(player))
            {
                Debug.LogWarning($"[AirConsoleInput] {player.PlayerNameText} cannot play cards right now.");
                return;
            }

            Card card = player.HandManager.Hand[cardIndex];
            var ac = card as ActionCardSO;

            if (ac == null)
            {
                // Non-action card — play directly
                CardEffectManager.Instance.ExecuteCardEffect(card, null);
                OnActionCompleted(player);
                return;
            }

            if (ac.NeedsTarget)
            {
                // Card requires a target — enter target selection mode
                BeginTargetSelection(player, ac);
            }
            else
            {
                // No target needed — execute immediately
                CardEffectManager.Instance.ExecuteCardEffect(ac, null);
                OnActionCompleted(player);
            }
        }

        private void BeginTargetSelection(Player player, ActionCardSO card)
        {
            pendingTargetPlayer = player;
            pendingCard = card;
            pendingNeedsSecondTarget = card.RequiresSecondTarget;
            pendingPrimaryTarget = null;

            // Build valid target list: all alive players except the acting player
            pendingValidTargets = PlayerService.GetAlivePlayers()
                .Where(p => p != player)
                .ToList();

            AirConsoleManager.Instance.SendTargetRequest(player, pendingValidTargets, pendingNeedsSecondTarget);
        }

        private void HandleSelectTarget(Player player, JToken data)
        {
            if (pendingTargetPlayer != player || pendingCard == null)
            {
                Debug.LogWarning($"[AirConsoleInput] {player.PlayerNameText} sent target selection but has no pending card.");
                return;
            }

            int targetIndex = data["targetIndex"]?.ToObject<int>() ?? -1;
            if (targetIndex < 0 || targetIndex >= pendingValidTargets.Count)
            {
                Debug.LogWarning($"[AirConsoleInput] Invalid target index {targetIndex} from {player.PlayerNameText}.");
                return;
            }

            Player selectedTarget = pendingValidTargets[targetIndex];

            if (pendingNeedsSecondTarget && pendingPrimaryTarget == null)
            {
                // First target selected, need a second one
                pendingPrimaryTarget = selectedTarget;

                // Send updated target list excluding the first pick
                var remaining = pendingValidTargets.Where(p => p != selectedTarget).ToList();
                AirConsoleManager.Instance.SendTargetRequest(player, remaining, false);
                return;
            }

            // Resolve the card effect
            Player primaryTarget = pendingPrimaryTarget ?? selectedTarget;
            Player secondaryTarget = pendingPrimaryTarget != null ? selectedTarget : null;

            if (secondaryTarget != null)
            {
                pendingCard.target = secondaryTarget;
            }

            CardEffectManager.Instance.ExecuteCardEffect(pendingCard, primaryTarget);
            ClearPendingTarget();
            OnActionCompleted(player);
        }

        private void HandleDrawCards(Player player)
        {
            if (GameTurnManager.Instance != null)
            {
                GameTurnManager.Instance.TryDrawTwoCards(player);
                // TryDrawTwoCards already calls EndTurn on success
                AirConsoleManager.Instance.SendHandUpdate(player);
            }
        }

        private void HandleEndTurn(Player player)
        {
            if (GameTurnManager.Instance != null)
            {
                GameTurnManager.Instance.RequestEndTurn(player);
            }
        }

        private void OnActionCompleted(Player player)
        {
            // Update the acting player's hand on their controller
            AirConsoleManager.Instance.SendHandUpdate(player);

            // Broadcast updated hands to all (some cards affect other players' hands)
            AirConsoleManager.Instance.BroadcastHandUpdates();
        }

        private void ClearPendingTarget()
        {
            pendingTargetPlayer = null;
            pendingCard = null;
            pendingValidTargets = null;
            pendingNeedsSecondTarget = false;
            pendingPrimaryTarget = null;
        }
    }
}
