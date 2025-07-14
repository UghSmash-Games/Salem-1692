/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
*   Primary Purpose: Represents each player’s state, hand, and Tryal cards.
*   Responsibilities:
*        • Track Tryal cards
*        • Check elimination
*        • Receive cards
*   Access Requirements:
*        • HandManager
*        • TryalCard
*        • GameStateManager
*        • PlayerHandUI

* TODO:
*   • Split state/data if needed
*   • Expose public events for changes
* FIXME: [Known bugs or issues]
*   • recognizing what townhall ability the player uses.
*      • currently has the variables name and PlayerName (what the string variable was when first developing the implementations)
*      • could switch to just using the PlayerNameText, but what about Martha Corey's ability to copy anothers?
*      • (also name is just the name of the object it is on, so while it works as a placeholder, this is not useable in function)
*   
*/
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Salem.Managers.GameState;
using Salem.Managers.Hands;
using Salem.Cards;
using TMPro;

namespace Salem.Players
{
    public class Player : MonoBehaviour
    {
        #region Vars
        public event Action OnStatusCardsChanged;
        public String PlayerNameText;
        public HandManager HandManager;
        public List<TryalCard> TryalCards = new List<TryalCard>();
        public List<Card> StatusCards { get; private set; } = new();
        public bool IsLocalPlayer = false;
        public bool IsWitch { get; private set; }  // Now determined dynamically
        public bool IsEliminated => TryalCards.TrueForAll(card => card.IsRevealed);
        //Added by Alex Craig-Hastings
        //the amount of accusations needed to reveal a tryal. This is modified by town hall cards at the beginning of the game, but not by cards like piety
        public byte baseAccusationLimit { get; private set; } = 7;
        //the amount of accusation cards needed to reveal a tryal currently. This is affected by cards like piety, and default back to the base version when those effects end
        public byte currentAccusationLimit { get; private set; }
        //the current amount of accusations against the player, once this goes over the currentAccusationLimit, a tryal card is revealed and it gets reset to 0
        public byte currentAccusationCount { get; private set; }
        //if the turn should be skipped or not
        public bool skipTurn { get; private set; }
        //if the player is safe during the night phase or not
        public bool hasAsylum { get; private set; }
        //in the case of matchmaker, what player is connected to this one?
        public Player MatchedPlayer;
        //the amount of uses a town hall ability has currently
        public byte townHallAbilityCharges { get; private set; }
        #endregion

        #region Standard Functions
        void Awake()
        {
            HandManager = GetComponent<HandManager>();
            //george burroughs ability boosts the number of accusations needed to reveal a tryal card by 1
            if(PlayerNameText == "George Burroughs")
            {
                baseAccusationLimit++;
            }
            else if(PlayerNameText == "William Phipps" || PlayerNameText == "Tituba")
            {
                townHallAbilityCharges = 1;
            }
            else if(PlayerNameText == "Samuel Parris")
            {
                townHallAbilityCharges= 2;
            }
        }
        #endregion

        #region Accessor Functions
        public void DetermineRole()
        {
            // A player is a Witch if they have at least one Witch TryalCard
            //FIX - this function will need to be called again when tryal cards get moved around, but even if the witch card gets removed from their hand, they stay a witch
            //put the check in first so witches cant be undone
            if(!IsWitch)
            {
                IsWitch = TryalCards.Any(card => card.TryalCardType == TryalCardType.Witch);
            }
        }

        public void RevealTryalCard(int index)
        {
            if (index < 0 || index >= TryalCards.Count) return;

            TryalCard card = TryalCards[index];
            if (!card.IsRevealed)
            {
                card.Reveal();
                Debug.Log($"{PlayerNameText} revealed a {card.Type} card!");
            }
            //arent we going to need a check if they try to reveal an already revealed card?
            CheckElimination();
        }

        public void AddStatusCard(Card card)
        {
            StatusCards.Add(card);
            OnStatusCardsChanged?.Invoke();
        }

        public void RemoveStatusCard(Card card)
        {
            StatusCards.Remove(card);
            OnStatusCardsChanged?.Invoke();
        }

        public void ClearStatusCards()
        {
            StatusCards.Clear();
            OnStatusCardsChanged?.Invoke();
        }

        public void shiftBaseAccusations(bool shiftUp)
        {
            if(shiftUp) { baseAccusationLimit++; } // if Thomas Danforth dies.... I think their ability ends, so use this on everyone to bring them back to normal
            else { baseAccusationLimit--; } //use in the case of Thomas Danforth, his ability will drop everyones limit by 1
        }
        #endregion

        #region Helper Functions
        //Called in Hand Manager.
        public virtual void ApplyCardEffect(Card card)
        {
            switch (card.Type)
            {
                case "Green":
                    //played then discarded
                    switch (card.name)
                    {
                        case "Arson":
                            if(PlayerNameText == "Sarah Good") { return; } //sarah good's ability makes her immune to this
                            HandManager.GetCards().Clear();
                            break;
                        case "Robbery":
                            if (PlayerNameText == "Sarah Good") { return; } //sarah good's ability makes her immune to this
                            card.target.HandManager.AddCard(HandManager.GetCards());
                            HandManager.GetCards().Clear();
                            break;
                        case "Alibi":
                            currentAccusationCount -= 3;
                            if(currentAccusationCount < 0) { currentAccusationCount = 0; }
                            break;
                        case "Stocks":
                            skipTurn = true;
                            break;
                        case "Scapegoat":
                            card.target.StatusCards.AddRange(StatusCards);
                            StatusCards.Clear();
                            break;
                    }
                    break;
                case "Blue":
                    // Remain in play
                    switch (card.name)
                    {
                        case "Piety":
                            currentAccusationLimit = (byte)(baseAccusationLimit * 2);
                            break;
                        case "Asylum":
                            hasAsylum = true;
                            break;
                        case "Matchmaker":
                            //the target of the card will be the other player that has the matchmaker card
                            MatchedPlayer = card.target;
                            if(MatchedPlayer != null)
                            {
                                MatchedPlayer.MatchedPlayer = this; //create a 2 way link between the 2 players in the case either die
                            }
                            break;
                    }
                    break;
                case "Red":
                    //played, then check for tryal reveal
                    switch (card.name)
                    {
                        case "Accusations":
                            currentAccusationCount++;
                            CheckAccusations();
                            break;
                        case "Evidence":
                            currentAccusationCount += 3;
                            if(PlayerNameText == "Cotton Mather") { currentAccusationCount -= 2; } //Cotton mather's ability has evidence only count as 1, so fix the number to reflect that
                            CheckAccusations();
                            break;
                        case "Witness":
                            currentAccusationCount += 7;
                            CheckAccusations();
                            break;
                    }
                    break;
            }
        }
        #endregion

        #region Helper Functions

        private void CheckElimination()
        {
            if (IsEliminated)
            {
                Debug.Log($"{PlayerNameText} is ELIMINATED!");
                //UI Update needs to happen here.
            }
        }

        private void CheckAccusations()
        {
            if (currentAccusationCount >= currentAccusationLimit)
            {
                //reveal tryal
                currentAccusationCount = 0;
            }
        }
        #endregion
    }
}