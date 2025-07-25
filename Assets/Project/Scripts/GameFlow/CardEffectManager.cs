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

namespace Salem.GameFlow
{
    public class CardEffectManager : MonoBehaviour
    {
        public static CardEffectManager Instance;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void ExecuteCardEffect(Card card, Player target)
        {
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
            if (card.Type == Card.CardType.Green)
                PlayerService.GetLocalPlayer().HandManager.RemoveCard(card);

            GameTurnManager.Instance.EndTurn();
        }
    }
}