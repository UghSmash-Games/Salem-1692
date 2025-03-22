/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Track Cards from DeckManager, Add Cards to Hand, Manage Discarding Cards via HandManager]
 * FIXME: [Known bugs or issues]
*/
using System.Collections.Generic;
using UnityEngine;

public class HandManager : MonoBehaviour
{
    #region Vars
    public List<Card> Hand = new List<Card>();
    
    #endregion

    #region Accessor Functions
    public void PlayCard(Card card, Player target)
    {
        Hand.Remove(card);
        target.ApplyCardEffect(card);
    }

    public void AddCard(Card card)
    {
        Hand.Add(card);
    }
    #endregion
}
