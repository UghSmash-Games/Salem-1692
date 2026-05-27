/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using UnityEngine;

namespace Salem.Cards
{
    public enum ActionOp
    {
        Accusation,
        Stocks,
        Evidence,
        Witness,
        Alibi,
        BlackCat,
        Conspiracy,
        Arson,
        Curse,
        Robbery,
        Scapegoat,
        Asylum,
        Matchmaker,
        Piety
    }

    [CreateAssetMenu(fileName = "NewActionCard", menuName = "Card Game/Action Card")]
    public sealed class ActionCardSO : Card
    {
        public ActionOp Op;
        public bool NeedsTarget;
        public bool RequiresSecondTarget;
        [TextArea] public string RulesText; // optional: human-readable tooltip
    }
}

