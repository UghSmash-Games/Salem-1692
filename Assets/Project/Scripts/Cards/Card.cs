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
using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine;

namespace Salem.Cards
{
    [CreateAssetMenu(fileName = "NewCard", menuName = "Card Game/Card")]
    public class Card : ScriptableObject
    {
        public string Name;
        public CardType Type;
        public bool RequiresTarget;
        public string Effect;
        public Sprite HiddenCardImage;
        public Sprite RevealedCardImage;
        public bool IsPlayed;
    }
}