/*
* AUTHOR:
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


namespace Salem.GameFlow
{
    public class CardEffectManager : MonoBehaviour
    {
        public static CardEffectManager Instance { get; private set; }
        public static event Action<string> OnCardPlayed;


        private Player CurrentPlayer;


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }


        public void ExecuteCardEffect(Card card, Player target)
        {
            UpdateCurrentPlayer();
            Debug.Log($"[Effect] Executing {card.Name} on {target?.PlayerNameText ?? "N/A"}");


            switch (card.Name)
            {
                case "Accusation":
                    // Trigger Accusation logic
                    break;
                case "Stocks":
                    // Silence player
                    break;
                default:
                    Debug.LogWarning($"[Effect] No logic implemented for {card.Name}");
                    break;
            }


            // Remove from hand if appropriate
            if (card.Type == Card.CardColor.Green)
                CurrentPlayer.HandManager.RemoveCard(card);


            //Prepare message
            string message = FormatCardLogMessage(card, target);
            // Raise event for CardLogManager to listen to
            OnCardPlayed?.Invoke(message);
            GameTurnManager.Instance.EndTurn();
        }


        #region Helper Functions
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
