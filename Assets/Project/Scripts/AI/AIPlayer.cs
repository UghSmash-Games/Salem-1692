/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: Implement AI Behaviors
 * FIXME: [Known bugs or issues]
*/
using System.Collections.Generic;
using UnityEngine;

public class AIPlayer : Player
{
    private List<Card> Hand = new List<Card>();
    private Card card;
    private Player target;

    public void TakeTurn()
    {
        // Placeholder for AI behavior
        DrawCards();
        //need to develop choosing card from hand and selecting target
        //PlayCards();
    }

    private void DrawCards()
    {
        // Logic for drawing cards
    }

    private void PlayCards()
    {
    foreach (Card i in Hand)
    {
            if (IsValidPlay(card))
        {
            //target = ChooseTarget(); // Basic target selection
            PlayCard(card, target);
            break; // Play one card per turn in this example
        }
        //GameTurnManager.Instance.EndTurn();
    }
    }

    private void PlayCard(Card card, Player target)
    {
        Debug.Log("Function Not Implemented");
        throw new System.NotImplementedException();
    }

    private bool IsValidPlay(Card card)
    {
        // Define simple rules for valid card plays
        return card.Type != "Black"; // Example: Skip black cards for now
    }

/*
    private Player ChooseTarget()
    {
    // Simple targeting logic (can be expanded later)
    List<Player> potentialTargets = GameManager.Instance.GetActivePlayers();
    return potentialTargets[Random.Range(0, potentialTargets.Count)];
    }
*/

}
