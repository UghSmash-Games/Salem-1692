/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Special card defining a player's hidden identity.
*   Responsibilities:
*        • Enum of Witch/NotAWitch/Constable
*        • Track reveal state
*   Access Requirements:
*        • Player
*        • PlayerHandUI

* TODO:
*    • Include Reveal method and IsRevealed flag
* FIXME: [Known bugs or issues]
*/
using System;
using UnityEngine;

namespace Salem.Cards
{
    public enum TryalCardType { Witch, NotAWitch, Constable }

    [System.Serializable]
    [CreateAssetMenu(fileName = "NewTryalCard", menuName = "Card Game/TryalCard")]
    public class TryalCard : Card
    {
        #region Vars
        public TryalCardType TryalCardType;
        public bool IsRevealed;
        //public Sprite HiddenSprite;
        //public Sprite RevealedSprite_Witch;
        //public Sprite RevealedSprite_NotAWitch;
        //public Sprite RevealedSprite_Constable;
        public event Action<TryalCard> OnRevealed;
        #endregion


        #region Accessor Functions
        public void Reveal()
        {
            IsRevealed = true;
            Debug.Log($"Tryal Card Revealed: {TryalCardType}");
            OnRevealed?.Invoke(this);
        }
        #endregion
    }
}