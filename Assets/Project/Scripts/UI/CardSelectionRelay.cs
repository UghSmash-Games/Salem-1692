/*
* AUTHOR: Ron Bresett
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

using System;
using UnityEngine;
using UnityEngine.UI;
using Salem.Cards;

namespace Salem.UI
{
    public class CardSelectionRelay : MonoBehaviour
    {
        private Button btn;
        public Card Card { get; private set; }
        public event Action<Card> Clicked;

        public void Init(Card c)
        {
            Card = c;
            btn = GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => Clicked?.Invoke(Card));
        }
    }
}

