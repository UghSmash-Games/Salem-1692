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
        // {target} is filled from the resolved target PASSED to CardLogFormatter, not from any field on
        // the card — the old shared-asset `target` field was removed (it leaked state between plays;
        // recipients are threaded by parameter through ExecuteCardEffect).
        public string LogMessage = "{source} played {card} on {target}.";

        // True for cards created at runtime via ScriptableObject.Instantiate (e.g. Will Grigs' Alibi
        // played "as a Witness" places a runtime Witness proxy). These are NOT real deck cards, so they
        // must be DESTROYED rather than returned to the discard pile — otherwise ReshuffleDiscardPile
        // (Deck.AddRange(DiscardPile)) would inflate the deck with phantom cards. NonSerialized: only
        // ever set on runtime copies, never persisted on the shared asset.
        [System.NonSerialized] public bool IsRuntimeInstance;

        // The game's "black cards" (the special draw-and-resolve cards) are Night and Conspiracy.
        // NOTE: they are authored as CardColor.White in the SOs (Black Cat is Blue), so identify them
        // by NAME — the same way CardEffectManager and GameSetup do. Used by Samuel Parris's ability
        // ("no black cards" from the discard pile).
        public static bool IsBlackCard(Card c) => c != null && (c.Name == "Night" || c.Name == "Conspiracy");
    }
}