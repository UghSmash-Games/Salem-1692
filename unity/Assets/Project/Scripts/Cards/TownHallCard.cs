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
using NUnit.Framework.Internal.Commands;

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

        public void applyEffect()
        {
            switch (CardName)
            {
                case TownhallName.WillGrigs:
                    break;
                case TownhallName.SarahGood:
                    break;
                case TownhallName.JohnProctor:
                    break;
                case TownhallName.SamuelParris:
                    break;
                case TownhallName.RebeccaNurse:
                    break;
                case TownhallName.MarthaCorey:
                    break;
                case TownhallName.ThomasDanforth:
                    break;
                case TownhallName.CottonMather:
                    break;
                case TownhallName.Tituba:
                    break;
                case TownhallName.AbigailWilliams:
                    break;
                case TownhallName.AnnePutnam:
                    break;
                case TownhallName.GilesCorey:
                    break;
                case TownhallName.MaryWarren:
                    break;
                case TownhallName.WilliamsPhipps:
                    break;
                case TownhallName.GeorgeBurroughs:
                    break;
            }
        }
    }
}