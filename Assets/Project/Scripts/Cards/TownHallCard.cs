/*
* AUTHOR:
* REFERENCES:
* NOTES:
*   Primary Purpose: Passive bonus ScriptableObject given at game start.
*   Responsibilities:
*        • Stores stat changes or passive ability
*        • Provides rules text description for UI display
*   Access Requirements:
*        • Player
*        • GameSetup
*
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using UnityEngine;
using Salem.Players;
using Salem.Gameplay.Setup;

namespace Salem.Cards
{
    public enum TownhallName
    {
        WillGrigs,
        SarahGood,
        JohnProctor,
        SamuelParris,
        RebeccaNurse,
        MarthaCorey,
        ThomasDanforth,
        CottonMather,
        Tituba,
        AbigailWilliams,
        AnnePutnam,
        GilesCorey,
        MaryWarren,
        WilliamsPhipps,
        GeorgeBurroughs
    }
    [CreateAssetMenu(fileName = "NewTownHallCard", menuName = "Card Game/TownHall Card")]
    public class TownHallCard : Card
    {
        public TownhallName CardName;

        [TextArea(2, 5)]
        public string RulesText;

        /// <summary>
        /// Returns the rules text description for this Town Hall card.
        /// If no RulesText is set on the asset, returns a hardcoded default.
        /// </summary>
        public string GetRulesText()
        {
            if (!string.IsNullOrEmpty(RulesText)) return RulesText;

            return CardName switch
            {
                TownhallName.SarahGood => "Robbery and Arson cards have no effect on you and are discarded.",
                TownhallName.JohnProctor => "When a player is eliminated, take all blue cards in front of them and all cards in their hand.",
                TownhallName.WillGrigs => "You may use Alibi cards as Witness cards, worth 7 total accusations.",
                TownhallName.SamuelParris => "Twice per game, draw up to 2 cards from the discard pile instead of the deck. No Black cards.",
                TownhallName.GilesCorey => "If you draw 2 Accusation cards on your turn, show them and draw a 3rd card.",
                TownhallName.RebeccaNurse => "Each time a Tryal is revealed on another player (from accusations), draw 1 card.",
                TownhallName.MarthaCorey => "You have the same ability as the first living player to your right.",
                TownhallName.ThomasDanforth => "When you accuse, the threshold is reduced by 1 (6th accusation triggers reveal).",
                TownhallName.CottonMather => "Evidence cards played against you are worth only 1 accusation.",
                TownhallName.WilliamsPhipps => "Once per game, you may confess without revealing one of your Tryal cards.",
                TownhallName.Tituba => "Once per game, on your turn before drawing, rearrange the deck for 60 seconds.",
                TownhallName.AbigailWilliams => "When you trigger a Tryal reveal, discard all accusations in front of your own Tryals.",
                TownhallName.AnnePutnam => "When you trigger a Tryal reveal, draw 2 cards before the Tryal is revealed.",
                TownhallName.GeorgeBurroughs => "8 total accusations must be played against you to reveal a Tryal.",
                TownhallName.MaryWarren => "You are immune to the ill effects of Matchmaker and Black Cat.",
                _ => ""
            };
        }
    }
}
