/*
* AUTHOR:
* REFERENCES:
* NOTES:
*   Primary Purpose: Passive bonus ScriptableObject given at game start.
*   Responsibilities:
*        • Stores stat changes or passive ability
*   Access Requirements:
*        • Player
*        • GameSetup

* TODO:
*    • Use interfaces or delegates to define effects
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
                // ⚠ CORRECTED. This previously read "take all blue cards in front of them and all
                // cards in their hand", which is the PRE-correction rule and is not what the game
                // implements: John takes from the HAND only, it is a CHOICE of up to three, and the
                // eliminated player's cards IN PLAY are discarded, not taken. See
                // docs/character-spec.md #5. The stale text fed the host's IN EFFECT panel via
                // HostCardSpriteRegistry.description, so it was being shown to players.
                TownhallName.JohnProctor => "When a player is eliminated, choose up to three cards from their hand to take. The rest are discarded.",
                TownhallName.WillGrigs => "You may choose to use alibi cards as if they were witness cards, worth seven total accusations.",
                TownhallName.SamuelParris => "Twice per game, draw up to 2 cards from the discard pile instead of the deck. No Black cards.",
                TownhallName.GilesCorey => "If you draw 2 red cards on your turn, show the other players and draw a 3rd card.",
                TownhallName.RebeccaNurse => "Each time a Tryal is revealed on another player (from accusations), draw 1 card.",
                TownhallName.MarthaCorey => "You have the same ability as the first living player to your right.",
                TownhallName.ThomasDanforth => "When you accuse, the threshold is reduced by 1 (6th accusation triggers reveal).",
                TownhallName.CottonMather => "Evidence cards played against you are worth only 1 accusation.",
                TownhallName.WilliamsPhipps => "Once per game, you may confess without revealing one of your Tryal cards.",
                TownhallName.Tituba => "Once per game, on your turn before drawing, rearrange the deck for 60 seconds.",
                TownhallName.AbigailWilliams => "If you place the final accusation on a tryal, you may discard all accusations in front of you.",
                TownhallName.AnnePutnam => "At the end of your turn, draw two cards for each tryal card you revealed during your turn.",
                TownhallName.GeorgeBurroughs => "8 total accusations must be played against you to reveal a Tryal.",
                TownhallName.MaryWarren => "You are immune to the ill effects of Matchmaker and Black Cat.",
                _ => ""
            };
        }
    }
}