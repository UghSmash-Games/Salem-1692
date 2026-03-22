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
*/
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Unity.VisualStudio.Editor;
using Salem.Cards;
using Salem.Data;
using Salem.GameFlow;
using Salem.Managers.GameState;
using Salem.Managers.Hands;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

namespace Salem.Players
{
    [RequireComponent(typeof(HandManager))]
    public class Player : MonoBehaviour, IPlayerController
    {
        #region Vars
        [Header("Control")]
        [SerializeField] private bool isHuman = true;     // set TRUE for you, FALSE for bots
        [SerializeField] private HandManager handManager;

        //needed RNG, but figured giving access to full manager was a bad idea
        public IRng Rng { get; private set; }
        public bool IsHuman => isHuman;

        public static event Action<Player, byte, byte> AccusationCountChanged;
        public static event Action<Player, byte, byte> AccusationThresholdReached;
        public static event Action<Player, TryalCard> TryalCardRevealed;
        // Fired when accusation limit is reached: (accused, accuser). Listener should reveal a Tryal on accused.
        public static event Action<Player, Player> OnAccusationRevealNeeded;

        public bool IsLocalPlayer; //=> isLocalPlayer;
        public event Action OnStatusCardsChanged;
        public event Action OnTryalCardsChanged;
        
        public String PlayerNameText;
        public TownHallCard townhallCard { get; private set; }
        public Sprite townHallCardIcon { get; private set; }
        public List<TryalCard> TryalCards = new List<TryalCard>();
        public List<Card> StatusCards { get; private set; } = new();
        public bool IsWitch { get; private set; }  // Now determined dynamically
        public bool IsConstable => TryalCards.Any(card => card.TryalCardType == TryalCardType.Constable);
        public bool IsEliminated;
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
        // Black Cat holder flag (keep separate from StatusCards to avoid Scapegoat moving it)
        public bool IsBlackCatHolder { get; private set; }
        public HandManager HandManager => handManager ??= GetComponent<HandManager>();

        private Card blackCatCard;
        #endregion

        #region Standard Functions
        private void OnValidate()
        {
            if (handManager == null) handManager = GetComponent<HandManager>();
        }
        void Awake()
        {
            if (HandManager == null)
            {
                Debug.LogError($"Player {PlayerNameText} is missing a HandManager component!");
            }
        }
        #endregion

        #region Accessor Functions
        public void setRng(IRng rng)
        {
            if(rng != null)
            {
                Rng = rng;
            }
        }

        public void setTownhall(TownHallCard card)
        {
            if (card == null) return;
            townhallCard = card;
            ApplyTownHallAbility();
        }

        private void ApplyTownHallAbility()
        {
            if (townhallCard == null) return;

            switch (townhallCard.CardName)
            {
                case TownhallName.GeorgeBurroughs:
                    baseAccusationLimit++;
                    currentAccusationLimit = baseAccusationLimit;
                    break;
                case TownhallName.WilliamsPhipps:
                case TownhallName.Tituba:
                    townHallAbilityCharges = 1;
                    break;
                case TownhallName.SamuelParris:
                    townHallAbilityCharges = 2;
                    break;
            }
        }

        public void DetermineRole()
        {
            RecomputeStatusFromStatusCards();
            // A player is a Witch if they have at least one Witch TryalCard
            //FIX - this function will need to be called again when tryal cards get moved around, but even if the witch card gets removed from their hand, they stay a witch
            //put the check in first so witches cant be undone
            if (!IsWitch)
            {
                IsWitch = TryalCards.Any(card => card.TryalCardType == TryalCardType.Witch);
            }
        }

        public void RevealTryalCard(int index)
        {
            Debug.Log("REVEAL");
            if (index < 0 || index >= TryalCards.Count) return;

            TryalCard card = TryalCards[index];
            if (!card.IsRevealed)
            {
                card.Reveal();
                Debug.Log($"{PlayerNameText} revealed a {card.Type} card!");
                /*if (card.TryalCardType == TryalCardType.Witch)
                {
                    EliminateNow();
                }*/
                TrialService.OnTrialCardRevealed(this, card);
                TryalCardRevealed?.Invoke(this, card);
            }
            //TODO:arent we going to need a check if they try to reveal an already revealed card?
            CheckElimination();
        }

        public void InvokeOnTryalCardsChanged()
        {
            OnTryalCardsChanged?.Invoke();
        }

        public void AddTryalCard(TryalCard card)
        {
            TryalCards.Add(card);
            OnTryalCardsChanged?.Invoke();
        }

        public void RemoveTryalCard(TryalCard card)
        {
            if (TryalCards.Remove(card))
            {
                OnTryalCardsChanged?.Invoke();
            }
        }

        public void ClearTryalCards()
        {
            TryalCards.Clear();
            OnTryalCardsChanged?.Invoke();
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
            if (shiftUp) { baseAccusationLimit++; } // if Thomas Danforth dies.... I think their ability ends, so use this on everyone to bring them back to normal
            else { baseAccusationLimit--; } //use in the case of Thomas Danforth, his ability will drop everyones limit by 1
        }
        #endregion

        #region IPlayerController Implementation
        public virtual Card SelectCard()
        {
            if (HandManager != null && HandManager.Hand.Count > 0)
            {
                return HandManager.Hand[0];
            }

            return null;
        }

        public virtual void PerformTurnAction(ActionCardSO selectedCard)
        {
            if (selectedCard == null)
            {
                return;
            }

            if (CardEffectManager.Instance == null)
            {
                Debug.LogError("CardEffectManager.Instance is null!");
                return;
            }

            Player primary = selectedCard.RequiresTarget ? selectedCard.target : null;
            Player secondary = selectedCard.RequiresSecondTarget ? selectedCard.target : null;
            CardEffectManager.Instance.ExecuteCardEffect(selectedCard, primary);
            HandManager?.RemoveCard(selectedCard);
        }
        #endregion

        #region Helper Functions
        //Called in Hand Manager.
        //leave for backwards compatibiltiy as of 8/31/25
        public virtual void ApplyCardEffect(Card card)
        {
            switch (card.Type)
            {
                case Card.CardColor.Green:
                    //played then discarded
                    switch (card.name)
                    {
                        case "Arson":
                            if (PlayerNameText == "Sarah Good") { return; } //sarah good's ability makes her immune to this
                            HandManager.ClearHand();
                            break;
                        case "Robbery":
                            if (PlayerNameText == "Sarah Good") { return; } //sarah good's ability makes her immune to this
                            //card.target.HandManager.AddCard(HandManager.GetCards());
                            HandManager.ClearHand();
                            break;
                        case "Alibi":
                            currentAccusationCount -= 3;
                            if (currentAccusationCount < 0) { currentAccusationCount = 0; }
                            NotifyAccusationChanged();
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
                case Card.CardColor.Blue:
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
                            if (MatchedPlayer != null)
                            {
                                MatchedPlayer.MatchedPlayer = this; //create a 2 way link between the 2 players in the case either die
                            }
                            break;
                    }
                    break;
                case Card.CardColor.Red:
                    //played, then check for tryal reveal
                    switch (card.name)
                    {
                        case "Accusations":
                            currentAccusationCount++;
                            NotifyAccusationChanged();
                            CheckAccusations();
                            break;
                        case "Evidence":
                            currentAccusationCount += 3;
                            if (PlayerNameText == "Cotton Mather") { currentAccusationCount -= 2; } //Cotton mather's ability has evidence only count as 1, so fix the number to reflect that
                            NotifyAccusationChanged();
                            CheckAccusations();
                            break;
                        case "Witness":
                            currentAccusationCount += 7;
                            NotifyAccusationChanged();
                            CheckAccusations();
                            break;
                    }
                    break;
            }
        }

        //Called in CardEffectManager
        // Accusations & turn effects
        public void ApplyAccusation(int amount, Player accuser = null)
        {
            Debug.Log("Acc limit:"+currentAccusationLimit);
            Debug.Log("Before Acc:"+currentAccusationCount);
            currentAccusationCount = (byte)Mathf.Max(0, currentAccusationCount + amount);
            Debug.Log("After Acc:" + currentAccusationCount);
            CheckAccusations(accuser);
        }
        public void ApplyAlibi(int reduceBy)
        {
            currentAccusationCount = (byte)Mathf.Max(0, currentAccusationCount - reduceBy);
            NotifyAccusationChanged();
        }        
        public void ApplyStocks(int turns = 1) => skipTurn = true; // extend later if you track duration

        // Hand
        public void ClearHand()
        {
            HandManager.ClearHand(); // already raises OnHandChanged
        }
        public void TransferEntireHandTo(Player recipient)
        {
            var cards = HandManager.GetCards();
            foreach (var c in cards) recipient.HandManager.AddCard(c);
            HandManager.ClearHand();
        }

        // Status (Blue) cards
        public void PlayStatusCardOnTarget(ActionCardSO statusCard, Player target)
        {
            // Remove from my hand (fires OnHandChanged for UI)
            HandManager.RemoveCard(statusCard);

            // Add to target's statuses and recompute (fires OnStatusCardsChanged + updates derived flags)
            target.AddStatusCard(statusCard);
            target.RecomputeStatusFromStatusCards();
        }
        public void AddStatusCardAndRecompute(Card status)
        {
            AddStatusCard(status);        // existing method
            RecomputeStatusFromStatusCards();
        }
        public bool HasStatus(string name) => StatusCards.Any(c => c.Name == name);
        public void ClearStatusCardsAndRecompute()
        {
            ClearStatusCards();           // existing method
            RecomputeStatusFromStatusCards();
        }
        public void TransferAllStatusesTo(Player recipient)
        {
            if (recipient == null)
            {
                return;
            }

            Card transferredBlackCat = RemoveBlackCat(false);

            if (StatusCards.Count > 0)
            {
                foreach (var s in StatusCards.ToList()) recipient.AddStatusCard(s);
                ClearStatusCards();
            }

            RecomputeStatusFromStatusCards();
            recipient.RecomputeStatusFromStatusCards();

            if (transferredBlackCat != null)
            {
                recipient.AssignBlackCat(transferredBlackCat);
            }
        }
        public void RemoveStatusByNameAndRecompute(string name)
        {
            var idx = StatusCards.FindIndex(c => c.Name == name);
            if (idx >= 0) StatusCards.RemoveAt(idx);
            RecomputeStatusFromStatusCards();
        }

        // Derivations from statuses (call whenever statuses change)
        public void RecomputeStatusFromStatusCards()
        {
            // Reset to base; then re-apply statuses each time
            currentAccusationLimit = baseAccusationLimit;


            hasAsylum = StatusCards.Any(c => c.Name == "Asylum"); // protected at Night

            bool hasPiety = StatusCards.Any(c => c.Name == "Piety"); // doubles limit
            if (hasPiety) currentAccusationLimit = (byte)(baseAccusationLimit * 2);

            // Curse makes accusations easier to trigger (limit -1, min 1)
            bool hasCurse = StatusCards.Any(c => c.Name == "Curse");
            if (hasCurse) currentAccusationLimit = (byte)Mathf.Max(1, currentAccusationLimit - 1);

            // Asylum blocks Night targeting/elimination
            hasAsylum = StatusCards.Any(c => c.Name == "Asylum");

            // If Matchmaker status fell off, clear the bond
            if (!StatusCards.Any(c => c.Name == "Matchmaker") && MatchedPlayer != null)
                ClearMatch();
        }

        // Matchmaker link (two-way)
        public void SetMatchWith(Player other)
        {
            MatchedPlayer = other;
            if (other != null && other.MatchedPlayer != this)
                other.MatchedPlayer = this;
        }
        public void ClearMatch()
        {
            if (MatchedPlayer != null && MatchedPlayer.MatchedPlayer == this)
                MatchedPlayer.MatchedPlayer = null;
            MatchedPlayer = null;
        }
        // Auto-link when both Matchmaker statuses are in play
        public static void TryFormMatchmakerLink()
        {
            var mmHolders = Salem.Data.PlayerService.All
                .Where(p => p.HasStatus("Matchmaker"))
                .ToList();

            // Exactly two holders → ensure they are linked
            if (mmHolders.Count == 2)
            {
                var a = mmHolders[0];
                var b = mmHolders[1];
                if (a.MatchedPlayer != b || b.MatchedPlayer != a)
                {
                    a.SetMatchWith(b);
                }
            }
        }

        //Black Cat
        public void AssignBlackCat(Card card)
        {
            if (card == null)
            {
                Debug.LogWarning("[Player] Attempted to assign a null Black Cat card.");
                return;
            }

            if (!StatusCards.Contains(card))
            {
                AddStatusCard(card);
            }

            blackCatCard = card;
            IsBlackCatHolder = true;
            RecomputeStatusFromStatusCards();
        }

        public Card RemoveBlackCat(bool recompute = true)
        {
            if (!IsBlackCatHolder)
            {
                return null;
            }

            var card = blackCatCard;
            if (card != null)
            {
                RemoveStatusCard(card);
            }

            blackCatCard = null;
            IsBlackCatHolder = false;

            if (recompute)
            {
                RecomputeStatusFromStatusCards();
            }

            return card;
        }

        public void ClearBlackCat()
        {
            RemoveBlackCat();
        }

        //For Conspiracy
        public int? GetRandomUnrevealedTryalIndex(Salem.Data.IRng rng)
        {
            if (TryalCards == null || TryalCards.Count == 0) return null;
            // collect available indices
            var indices = new List<int>();
            for (int i = 0; i < TryalCards.Count; i++)
                if (!TryalCards[i].IsRevealed) indices.Add(i);

            if (indices.Count == 0) return null;
            int pick = rng.NextInt(0, indices.Count);
            return indices[pick];
        }

        public TryalCard RemoveTryalAt(int index)
        {
            if (index < 0 || index >= TryalCards.Count) return null;
            var card = TryalCards[index];
            TryalCards.RemoveAt(index);
            OnTryalCardsChanged?.Invoke();
            return card;
        }

        public void AddTryalCardAndNotify(TryalCard card)
        {
            TryalCards.Add(card);
            // If this player *gained* a Witch, allow DetermineRole to lock in witchhood
            DetermineRole();
            OnTryalCardsChanged?.Invoke();
        }

        public bool TryRevealTryalOfType(TryalCardType type)
        {
            int index = TryalCards.FindIndex(tc => tc.TryalCardType == type && !tc.IsRevealed);
            if (index < 0)
            {
                return false;
            }

            RevealTryalCard(index);
            return true;
        }

        public bool TryConfessToSurvive()
        {
            if (TryRevealTryalOfType(TryalCardType.NotAWitch))
            {
                return true;
            }

            if (TryRevealTryalOfType(TryalCardType.Constable))
            {
                return true;
            }

            return false;
        }
        
        // Eliminate immediately (reveal all remaining Tryals safely)
        public void EliminateNow()
        {
            if (IsEliminated) return;
            for (int i = 0; i < TryalCards.Count; i++)
                if (!TryalCards[i].IsRevealed) RevealTryalCard(i);

            GameManager.Instance.OnDayLynchResolved();
        }

        // Called after IsEliminated is set. Discards hand + status cards,
        // or transfers them to John Proctor holder if one exists.
        public void OnElimination()
        {
            // Find alive player with John Proctor town hall card
            var johnProctor = PlayerService.GetAlivePlayers()
                .FirstOrDefault(p => p != this && p.townhallCard != null
                    && p.townhallCard.CardName == TownhallName.JohnProctor);

            if (johnProctor != null)
            {
                Debug.Log($"[Elimination] {PlayerNameText}'s cards transferred to {johnProctor.PlayerNameText} (John Proctor).");
                TransferEntireHandTo(johnProctor);
                // Transfer status cards (excluding Black Cat which is handled separately)
                var blackCat = RemoveBlackCat(false);
                foreach (var s in StatusCards.ToList())
                    johnProctor.AddStatusCard(s);
                ClearStatusCards();
                johnProctor.RecomputeStatusFromStatusCards();
                // Black Cat goes to discard, not to John Proctor
                if (blackCat != null)
                {
                    var dm = UnityEngine.Object.FindFirstObjectByType<Salem.Deck.DeckManager>();
                    if (dm != null) dm.AddToDiscardPile(blackCat);
                }
            }
            else
            {
                Debug.Log($"[Elimination] {PlayerNameText}'s cards discarded.");
                // Discard all hand cards
                var handCards = HandManager.GetCards();
                var dm = UnityEngine.Object.FindFirstObjectByType<Salem.Deck.DeckManager>();
                foreach (var c in handCards)
                {
                    if (dm != null) dm.AddToDiscardPile(c);
                }
                HandManager.ClearHand();
                // Remove Black Cat
                var blackCat = RemoveBlackCat(false);
                if (blackCat != null && dm != null)
                    dm.AddToDiscardPile(blackCat);
                // Clear status cards (blue cards in play)
                ClearStatusCardsAndRecompute();
            }

            RecomputeStatusFromStatusCards();
        }

        private bool eliminationNotified;

        // After any reveal, check if this player should be eliminated, then cascade to Matchmaker partner
        private void CheckElimination()
        {
            // Determine if the player should be eliminated based on revealed Tryals
            if (!IsEliminated && TryalCards.Count > 0)
            {
                if (TryalCards.Any(c => c.IsRevealed && c.TryalCardType == TryalCardType.Witch))
                {
                    PlayerService.Eliminate(this, EliminationCause.WitchTrialRevealed);
                }
                else if (TryalCards.All(c => c.IsRevealed))
                {
                    PlayerService.Eliminate(this, EliminationCause.AllTrialsRevealed);
                }
            }

            if (IsEliminated && !eliminationNotified)
            {
                eliminationNotified = true;
                Debug.Log($"{PlayerNameText} is ELIMINATED!");
                GameTurnManager.Instance?.OnPlayerEliminated(this);

                if (MatchedPlayer != null &&
                    HasStatus("Matchmaker") &&
                    MatchedPlayer.HasStatus("Matchmaker") &&
                    !MatchedPlayer.IsEliminated)
                {
                    // prevent ping-pong; clear link then eliminate partner
                    var partner = MatchedPlayer;
                    ClearMatch();
                    partner.ClearMatch();
                    partner.EliminateNow();
                }
                GameManager.Instance.OnDayLynchResolved();
            }
        }

        private void CheckAccusations(Player accuser = null)
        {
            if (currentAccusationCount >= currentAccusationLimit)
            {
                AccusationThresholdReached?.Invoke(this, currentAccusationCount, currentAccusationLimit);
                currentAccusationCount = 0;
                NotifyAccusationChanged();

                // If there's a listener (CardEffectManager), let the accuser choose which Tryal to reveal.
                // Otherwise fall back to random reveal.
                if (OnAccusationRevealNeeded != null && accuser != null)
                {
                    OnAccusationRevealNeeded.Invoke(this, accuser);
                }
                else
                {
                    int? tryalToReveal = GetRandomUnrevealedTryalIndex(Rng);
                    if (tryalToReveal.HasValue)
                    {
                        RevealTryalCard(tryalToReveal.Value);
                    }
                }
            }
        }

        private void NotifyAccusationChanged()
        {
            AccusationCountChanged?.Invoke(this, currentAccusationCount, currentAccusationLimit);
        }
        #endregion

        [Header("Status Effects")]
        [SerializeField] private bool hasBlackCat;

        // Property to check if the player holds the Black Cat
        public bool HasBlackCat
        {
            get => hasBlackCat;
            private set => hasBlackCat = value;
        }

        //Posible delete pending as of 2/13/26
        // Property to check if the player is a Witch (checks Tryal cards)
        /*
        public bool IsWitch
    {
        get
        {
            // VERIFY: Is 'myTryalHand' the correct list name?
            if (myTryalHand == null || myTryalHand.Count == 0) return false;

            foreach (var card in myTryalHand)
            {
                // VERIFY: Is '.data' the correct reference to the ScriptableObject?
                if (card != null && card.data != null)
                {
                    string name = card.data.cardName; // VERIFY: Is '.cardName' correct?
                    if (name.Contains("Witch") && !name.Contains("Not"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }*/

        // Method called by GamePhaseManager to assign the cat
        public void ReceiveBlackCat()
    {
        // VERIFY: Ensure 'PlayerNameText' is the correct property for the name
        Debug.Log($"{PlayerNameText} has received the Black Cat.");
        HasBlackCat = true;
    }

    public void RemoveBlackCat()
    {
        HasBlackCat = false;
        }
    }
}
