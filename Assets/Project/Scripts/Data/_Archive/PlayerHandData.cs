/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Lightweight data container for UI to access relevant Player card information 
*   without exposing full game logic or gameplay dependencies.
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/
using System.Collections.Generic;
using Salem.Cards;

namespace Salem.Data
{
    public class PlayerHandData
    {
        public List<Card> HandCards;
        public List<TryalCard> TryalCards;

        public PlayerHandData(List<Card> handCards, List<TryalCard> tryalCards)
        {
            HandCards = handCards;
            TryalCards = tryalCards;
        }
    }
}