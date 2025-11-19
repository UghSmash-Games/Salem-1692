/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Abstract interface for human or AI turn input.
*   Responsibilities:
*        • Handle card selection
*        • Define actions to take on turn
*   Access Requirements:
*        • Player
*        • GameTurnManager

* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/
using UnityEngine;
using Salem.GameFlow;
using Salem.Cards;

namespace Salem.Players
{
    public interface IPlayerController
    {
        /// <summary>
        /// Choose a card from the player's hand to play.
        /// </summary>
        /// <returns>The selected card or null if none available.</returns>
        Card SelectCard();

        /// <summary>
        /// Execute the action for the selected card.
        /// </summary>
        /// <param name="selectedCard">Card chosen by SelectCard.</param>
        void PerformTurnAction(ActionCardSO selectedCard);
    }
}