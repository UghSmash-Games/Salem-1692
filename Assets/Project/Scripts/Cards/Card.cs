/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Base ScriptableObject representing a game card.
*   Responsibilities:
*        • Store name, type, visuals
*        • Define playability
*   Access Requirements:
*        • DeckManager
*        • HandManager
* TODO: 
*    • Add attributes for special effects (IE Conspiracy / Night)
*    • Add CardID and CanPlay method
* FIXME: [Known bugs or issues]
*/
using Salem.Players;
using UnityEngine;

namespace Salem.Cards
{
    [CreateAssetMenu(fileName = "NewCard", menuName = "Card Game/Card")]
    public class Card : ScriptableObject
    {
        public enum CardColor
    {
        Green,
        Blue,
        Red,
        Black,
        White,
        Tryal
    }
        public string Name;
        public CardColor Type;
        public bool RequiresTarget;
        public string Effect;
        public Sprite HiddenCardImage;
        public Sprite RevealedCardImage;
        public bool IsPlayed;
        public Player target;
        public string LogMessage = "{source} played {card} on {target}."; //secondary target when the card is played, used to make matchmaker, robbery and scapegoat work

        // The game's "black cards" (the special draw-and-resolve cards) are Night and Conspiracy.
        // NOTE: they are authored as CardColor.White in the SOs (Black Cat is Blue), so identify them
        // by NAME — the same way CardEffectManager and GameSetup do. Used by Samuel Parris's ability
        // ("no black cards" from the discard pile).
        public static bool IsBlackCard(Card c) => c != null && (c.Name == "Night" || c.Name == "Conspiracy");
    }
}