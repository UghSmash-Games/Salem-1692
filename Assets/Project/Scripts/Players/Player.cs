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
using Salem.Cards;
using Salem.Data;
using Salem.GameFlow;
using Salem.Managers.Hands;
using TMPro;
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

        // Network connection state. Defaults true; flipped false when the server
        // reports this seat's socket dropped (NetworkManager.OnPlayerLeft, consumed
        // mid-game by NetworkGameCoordinator.HandlePlayerLeft). Read live by the
        // secret-phase wait set so a dropped human can't stall a phase to its
        // timeout. NOTE (4c scope): this is ONLY the wait-set signal — seat
        // cleanup / reconnect / turn-order removal remain post-4a.
        public bool IsConnected = true;

        public static event Action<Player, byte, byte> AccusationCountChanged;
        public static event Action<Player, byte, byte> AccusationThresholdReached;
        public static event Action<Player, TryalCard> TryalCardRevealed;
        // Fired when accusation limit is reached: (accused, accuser). Listener should reveal a Tryal on accused.
        public static event Action<Player, Player> OnAccusationRevealNeeded;

        public bool IsLocalPlayer; //=> isLocalPlayer;
        public event Action OnStatusCardsChanged;
        public event Action OnTryalCardsChanged;

        // Network playerId (e.g. "p0") for networked-mode seats; empty for local/AI.
        public string NetworkId;

        // Public DISPLAY identity for game_state_update. For human seats this is
        // the NetworkId; for AI seats it's a synthetic id (e.g. "ai0") so boards
        // stay unique on the client. AI keep NetworkId empty (no private_state).
        public string PublicId;

        // Where this player's decisions come from. Lazily defaults to LocalUIInput
        // so the local/AI game works with no coordinator; the network coordinator
        // assigns a NetworkInput for remote seats. AI players never use this
        // (GameTurnManager runs AIPlayer via AITurnSequencer).
        private IPlayerInput _input;
        public IPlayerInput Input
        {
            get => _input ??= new LocalUIInput();
            set => _input = value;
        }

        // Abigail Williams: set when she places a threshold-crossing accusation, meaning she owes a
        // "may I discard my accusations?" decision. Set in CheckAccusations (which is synchronous and
        // so cannot await a prompt) and consumed by NetworkInput.RunTurn on its next loop tick, still
        // within her turn. Only ever set for a NetworkInput seat; AI/local auto-clear instead.
        public bool PendingAbigailDiscardChoice;

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
        // Martha Corey copy re-resolve (Phase 5 #6): _intrinsicBase is Martha's own (uncopied)
        // accusation base, captured once at setup; _appliedCopySource is the ability her copied
        // charges/limits currently reflect. Together they let a mid-game source change (a right
        // neighbour dying) reset-then-reapply EXACTLY once — never double-incrementing George's +1
        // and never resurrecting a spent Tituba/Parris charge (we only reset when the source
        // actually changes). Unused for non-Martha players.
        private byte _intrinsicBase;
        private TownhallName? _appliedCopySource;
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
            if(card == null) { return; }
            townhallCard = card;
            ApplyTownHallAbility();
        }

        private void ApplyTownHallAbility()
        {
            if (townhallCard == null) return;

            // Reset ability-modified stats to defaults FIRST so re-assigning a town hall card cleanly
            // REPLACES the prior card's effects instead of stacking on them. This matters because the
            // Phase-5 forced-seat debug override calls setTownhall a SECOND time: a seat whose random
            // deal was George (base→8) then forced to another character used to keep the stray +1,
            // corrupting that character's base AND Martha's _intrinsicBase capture (the root cause of the
            // base-9 double-count). Harmless for the normal single-assignment path (defaults == initial).
            baseAccusationLimit = 7;
            currentAccusationLimit = 7;
            townHallAbilityCharges = 0;

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

            // Capture each player's TRUE intrinsic (uncopied) accusation base ONCE per card assignment,
            // before any Martha copy. Martha's stays 7 (no bump); George's is 8. ReResolveMarthaCopy
            // resets to this — capturing here (not in ApplyMarthaCoreyCopy) makes the baseline immune to
            // the re-capture pollution that produced base 9.
            _intrinsicBase = baseAccusationLimit;

            // DIAGNOSTIC (Tituba option trace): confirm the charge was set at setup.
            Debug.Log($"[Tituba?] ApplyTownHallAbility {PlayerNameText}: card={townhallCard.CardName}, " +
                      $"charges={townHallAbilityCharges}");
        }

         /// <summary>
        /// Checks if this player has the given Town Hall ability, accounting for Martha Corey's copy ability.
        /// </summary>
        public bool HasTownHall(TownhallName name)
        {
            if (townhallCard == null) return false;
            if (townhallCard.CardName == name) return true;
            if (townhallCard.CardName == TownhallName.MarthaCorey)
                return GetEffectiveTownHallName() == name;
            return false;
        }

        /// <summary>
        /// Returns the effective Town Hall identity. For Martha Corey, returns the ability of the
        /// first living player to her right. For all others, returns their own CardName.
        /// </summary>
        public TownhallName? GetEffectiveTownHallName()
        {
            if (townhallCard == null) return null;
            if (townhallCard.CardName != TownhallName.MarthaCorey) return townhallCard.CardName;

            // Find first living player to the right (next in turn order)
            var allPlayers = PlayerService.All;
            int myIndex = -1;
            for (int i = 0; i < allPlayers.Count; i++)
            {
                if (allPlayers[i] == this)
                {
                    myIndex = i;
                    break;
                }
            }

            if (myIndex < 0) return null;

            for (int i = 1; i < allPlayers.Count; i++)
            {
                var candidate = allPlayers[(myIndex + i) % allPlayers.Count];
                if (!candidate.IsEliminated && candidate != this && candidate.townhallCard != null)
                    return candidate.townhallCard.CardName;
            }
            return null;
        }

        /// <summary>
        /// SETUP entry point for Martha Corey's copy. Captures her intrinsic (uncopied) base once,
        /// then resolves the copied charge/limit through <see cref="ReResolveMarthaCopy"/>. This is the
        /// SAME code path used mid-game on every elimination, so setup and neighbour-death re-resolves
        /// can never drift. Must be called after all players have their Town Hall cards assigned.
        /// </summary>
        public void ApplyMarthaCoreyCopy()
        {
            // _intrinsicBase is captured in ApplyTownHallAbility (once, before any copy) — NOT here,
            // so a re-run can never re-capture an already-copied base.
            _appliedCopySource = null;            // force the first resolve to apply
            ReResolveMarthaCopy();
        }

        /// <summary>
        /// Restores Martha's copied stat modifiers to her intrinsic baseline: base accusation limit
        /// back to <c>_intrinsicBase</c> and copied charges cleared. <see cref="ReResolveMarthaCopy"/>
        /// re-applies the current source's fresh modifiers afterward. Reuses
        /// <see cref="RecomputeStatusFromStatusCards"/> so <c>currentAccusationLimit</c> and Piety ×2
        /// are re-derived from the reset base.
        /// </summary>
        private void ResetCopiedModifiers()
        {
            baseAccusationLimit = _intrinsicBase;
            townHallAbilityCharges = 0;         // clear copied charges; the reapply re-grants fresh if applicable
            RecomputeStatusFromStatusCards();   // currentAccusationLimit = base, then Piety ×2 if present
        }

        /// <summary>
        /// Re-resolves Martha Corey's copied charge/limit to her current effective source (the first
        /// living player to her right, via <see cref="GetEffectiveTownHallName"/>). Called at setup and
        /// on every elimination (by the character dispatcher). "Fresh charges on switch only": if the
        /// effective source is UNCHANGED we return immediately, preserving any consumed charges; only a
        /// real source change resets-then-reapplies (so George's +1 never double-counts and a spent
        /// Tituba/Parris charge is never resurrected). No-op for non-Martha holders.
        /// </summary>
        public void ReResolveMarthaCopy()
        {
            if (townhallCard == null || townhallCard.CardName != TownhallName.MarthaCorey) return;

            var src = GetEffectiveTownHallName();
            if (src == _appliedCopySource) return; // unchanged → keep consumed-charge state as-is

            ResetCopiedModifiers();

            switch (src)
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

            _appliedCopySource = src;
        }

        public void ConsumeTownHallCharge()
        {
            if (townHallAbilityCharges > 0) townHallAbilityCharges--;
        }

        public void ResetAccusationCount()
        {
            DiscardRedStatusCards();
            NotifyAccusationChanged();
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

        public void RevealTryalCard(int index, bool fromAccusation = false)
        {
            Debug.Log("REVEAL");
            if (index < 0 || index >= TryalCards.Count) return;

            TryalCard card = TryalCards[index];
            if (!card.IsRevealed)
            {
                card.Reveal();
                Debug.Log($"{PlayerNameText} revealed a {card.Type} card!");

                TrialService.OnTrialCardRevealed(this, card, fromAccusation);
                TryalCardRevealed?.Invoke(this, card);
            }
            //TODO:arent we going to need a check if they try to reveal an already revealed card?
            
            GameManager.Instance?.EvaluateEndGame();
        }

        public void InvokeOnTryalCardsChanged()
        {
            OnTryalCardsChanged?.Invoke();
        }

/*
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
*/

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
         public void ApplyAccusation(int bonusAmount, Player accuser = null)
        {
            RecomputeStatusFromStatusCards();
            // bonusAmount adds accusations beyond what's tracked by physical cards
            // (e.g., Will Griggs offensive Alibi). Normal card ops pass 0.
            if (bonusAmount > 0)
                currentAccusationCount = (byte)Math.Min(255, currentAccusationCount + bonusAmount);
            Debug.Log($"Acc limit:{currentAccusationLimit} Acc count:{currentAccusationCount}");
            CheckAccusations(accuser);
        }
        public void ApplyAlibi(int removeCount)
        {
            // Remove up to N Accusation cards from in front of this player
            int removed = 0;
            var dm = UnityEngine.Object.FindFirstObjectByType<Salem.Deck.DeckManager>();
            for (int i = StatusCards.Count - 1; i >= 0 && removed < removeCount; i--)
            {
                if (StatusCards[i] is ActionCardSO ac && ac.Op == ActionOp.Accusation)
                {
                    dm?.AddToDiscardPile(StatusCards[i]);
                    StatusCards.RemoveAt(i);
                    removed++;
                }
            }
            if (removed > 0)
                OnStatusCardsChanged?.Invoke();
            RecomputeStatusFromStatusCards();
            NotifyAccusationChanged();
        }     
        public void ApplyStocks(int turns = 1) => RecomputeStatusFromStatusCards();
        
        /// <summary>
        /// Removes one Stocks card from in front of this player and discards it.
        /// Called when the player's turn is skipped due to Stocks.
        /// </summary>
        public void ConsumeOneStocks()
        {
            int idx = StatusCards.FindIndex(c => c is ActionCardSO ac && ac.Op == ActionOp.Stocks);
            if (idx < 0) return;
            var card = StatusCards[idx];
            StatusCards.RemoveAt(idx);
            var dm = UnityEngine.Object.FindFirstObjectByType<Salem.Deck.DeckManager>();
            dm?.AddToDiscardPile(card);
            OnStatusCardsChanged?.Invoke();
            RecomputeStatusFromStatusCards();
        }

        // Hand
        /// <summary>
        /// Empties the hand WITHOUT discarding — the cards simply cease to exist.
        /// ⚠ Only correct when ownership has ALREADY moved elsewhere (e.g. TransferEntireHandTo
        /// re-adds each card to the recipient first). To DESTROY a hand, use <see cref="BurnHand"/>:
        /// a bare clear permanently removes cards from circulation, because the deck is re-formed
        /// from the discard pile (DeckManager.ReshuffleDiscardPile) — anything never discarded can
        /// never come back, shrinking the deck for the rest of the game.
        /// </summary>
        public void ClearHand()
        {
            HandManager.ClearHand(); // already raises OnHandChanged
        }

        /// <summary>
        /// Destroy this player's hand the way the rules mean it: every card goes to the DISCARD PILE,
        /// then the hand is emptied. Cards stay in circulation (the deck re-forms from the discard
        /// pile), unlike a bare <see cref="ClearHand"/>. Used by Arson and by elimination when no
        /// John Proctor drafter is alive to claim the hand.
        /// </summary>
        public void BurnHand()
        {
            var dm = UnityEngine.Object.FindFirstObjectByType<Salem.Deck.DeckManager>();
            foreach (var c in HandManager.GetCards())   // GetCards() returns a copy — safe to clear after
                if (c != null) dm?.AddToDiscardPile(c);
            HandManager.ClearHand();
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
            // Take from my hand WITHOUT discarding — the card is being transferred
            // to the target's status cards, not sent to the discard pile.
            HandManager.TakeCard(statusCard);

            // Add to target's statuses and recompute (fires OnStatusCardsChanged + updates derived flags)
            target.AddStatusCard(statusCard);
            target.RecomputeStatusFromStatusCards();
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

        // Derivations from statuses (call whenever statuses change)
        public void RecomputeStatusFromStatusCards()
        {
            // Reset to base; then re-apply statuses each time
            currentAccusationLimit = baseAccusationLimit;

            bool hasPiety = StatusCards.Any(c => c.Name == "Piety"); // doubles limit
            if (hasPiety) currentAccusationLimit = (byte)(baseAccusationLimit * 2);

            // Asylum blocks Night targeting/elimination
            hasAsylum = StatusCards.Any(c => c.Name == "Asylum");

            // Stocks: skip turn if any Stocks cards are in front of this player
            skipTurn = StatusCards.Any(c => c is ActionCardSO ac && ac.Op == ActionOp.Stocks);

            // Asylum blocks Night targeting/elimination
            hasAsylum = StatusCards.Any(c => c.Name == "Asylum");

            // If Matchmaker status fell off, clear the bond
            if (!StatusCards.Any(c => c.Name == "Matchmaker") && MatchedPlayer != null)
                ClearMatch();

            // Derive accusation count from red cards in front of this player
            byte accusationTotal = 0;
            foreach (var c in StatusCards)
            {
                if (c.Type != Card.CardColor.Red || !(c is ActionCardSO ac)) continue;
                switch (ac.Op)
                {
                    case ActionOp.Accusation: accusationTotal += 1; break;
                    case ActionOp.Evidence: accusationTotal += (byte)(HasTownHall(TownhallName.CottonMather) ? 1 : 3); break;
                    case ActionOp.Witness: accusationTotal += 7; break;
                }
            }
            currentAccusationCount = accusationTotal;
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

            // Mary Warren CAN be given and hold the Black Cat (rulebook: she's immune to its ILL
            // EFFECTS, not refused). Her immunity is applied at Conspiracy step 1
            // (GamePhaseManager.ConspiracyRoutine skips the reveal when the holder is Mary), NOT here.

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
            bool wasWitch = IsWitch;
            TryalCards.Add(card);
            // If this player *gained* a Witch, allow DetermineRole to lock in witchhood
            DetermineRole();
            OnTryalCardsChanged?.Invoke();

            // If this player just became a witch (e.g., via Conspiracy swap), re-evaluate endgame —
            // witches win if all remaining players are now witches. Pass `this` so that, if this
            // turning is what ends the game (they were the LAST non-witch), they are recorded as the
            // loser and excluded from the winning witch team (rulebook: "that player loses").
            if (!wasWitch && IsWitch)
            {
                GameManager.Instance?.EvaluateEndGame(this);
            }
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
        // Elimination is triggered by TrialService.OnTrialCardRevealed() on first Witch
        // reveal or when all Tryals are revealed, which calls PlayerService.Eliminate().
        public void EliminateNow()
        {
            if (IsEliminated) return;
            for (int i = 0; i < TryalCards.Count; i++)
                if (!TryalCards[i].IsRevealed) RevealTryalCard(i);
        }

        // Called after IsEliminated is set. STATUS cards (red + blue) and the Black Cat are ALWAYS
        // discarded — "cards in play affecting the eliminated player are eliminated" (rulebook). The
        // HAND is John Proctor's ability target: John (or a Martha effectively-John) takes UP TO 3
        // cards FROM THE HAND and the rest are discarded. Because that pick is a networked choice, we
        // can't resolve it inline here — so if a drafter is alive we LEAVE the hand in place and the
        // CharacterAbilityDispatcher runs the draft (via OnPlayerEliminated) after this returns; the
        // draft coroutine discards the leftovers. With no drafter, the hand discards immediately (as
        // before). See CharacterAbilityDispatcher / JohnProctorAbility.
        public void OnElimination()
        {
            var dm = UnityEngine.Object.FindFirstObjectByType<Salem.Deck.DeckManager>();

            // HAND — leave for the draft only if a live drafter exists (real John OR a Martha whose
            // effective ability is John, via HasTownHall). Otherwise discard now.
            bool hasDrafter = PlayerService.GetAlivePlayers()
                .Any(p => p != this && p.HasTownHall(TownhallName.JohnProctor));
            if (hasDrafter)
            {
                // Hand stays on this (eliminated) player until the dispatcher's draft coroutine
                // takes/discards it. Nobody else reads a dead player's hand in the interim.
            }
            else
            {
                Debug.Log($"[Elimination] {PlayerNameText}'s cards discarded.");
                BurnHand();   // discard-then-clear (same canonical path Arson uses)
            }

            // BLACK CAT + STATUS cards — always discarded, never transferred (not even to John).
            var blackCat = RemoveBlackCat(false);
            if (blackCat != null && dm != null) dm.AddToDiscardPile(blackCat);
            foreach (var sc in StatusCards)
                if (dm != null) dm.AddToDiscardPile(sc);
            ClearStatusCardsAndRecompute();

            RecomputeStatusFromStatusCards();

            // Town Hall card is visible to all until elimination — clear it now
            townhallCard = null;
            townHallCardIcon = null;
        }

        private void CheckAccusations(Player accuser = null)
        {
            // Reveal threshold for THIS accusation. Danforth (the ACCUSER) reduces the
            // TARGET's BASE by 1 BEFORE piety doubling (rulebook). For a non-Danforth accuser,
            // currentAccusationLimit is already correct (base → piety×2); only the Danforth
            // case recomputes from the base.
            int effectiveLimit = currentAccusationLimit;
            if (accuser != null && accuser.HasTownHall(TownhallName.ThomasDanforth))
            {
                int effBase = Math.Max(1, baseAccusationLimit - 1);                  // −1 on the BASE
                bool targetHasPiety = StatusCards.Any(c => c.Name == "Piety");
                effectiveLimit = targetHasPiety ? effBase * 2 : effBase;             // then piety ×2
            }

            if (currentAccusationCount >= effectiveLimit)
            {
                AccusationThresholdReached?.Invoke(this, currentAccusationCount, currentAccusationLimit);

                // Anne Putnam's draw is NO LONGER here. Her card is "at the END of your turn, draw two
                // cards for EACH tryal you revealed this turn" — tallied per actual reveal in
                // TrialService.OnTrialCardRevealed → GameTurnManager, consumed at EndTurn.

                // Discard all red cards in front of this player
                DiscardRedStatusCards();
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
                        RevealTryalCard(tryalToReveal.Value, fromAccusation: true);
                    }
                }

                // Abigail Williams: "If you place the final accusation on a tryal, you MAY discard all
                // accusations in front of you." Trigger = placing the accusation that CROSSES the
                // threshold (not the reveal itself). The choice is real — keeping accusations is
                // legitimate (Scapegoat can transfer them onto another player), so a phone-driven
                // Abigail is prompted rather than auto-cleared.
                //
                // This method is SYNCHRONOUS (ApplyAccusation ← CardEffectManager ← ExecuteCardEffect),
                // so a networked prompt can't be awaited here. Instead set a pending flag that
                // NetworkInput.RunTurn consumes on its next loop tick — still her turn, before she can
                // act again. AI and local-host seats have no prompt UI and auto-clear (same posture as
                // the other phone-driven abilities).
                if (accuser != null && accuser.HasTownHall(TownhallName.AbigailWilliams))
                {
                    if (accuser.Input is NetworkInput)
                    {
                        accuser.PendingAbigailDiscardChoice = true;
                    }
                    else
                    {
                        accuser.ResetAccusationCount();
                        Debug.Log($"[TownHall] Abigail Williams ({accuser.PlayerNameText}) clears her own accusations (auto — no prompt UI).");
                    }
                }
            }
        }
        private void DiscardRedStatusCards()
        {
            var dm = UnityEngine.Object.FindFirstObjectByType<Salem.Deck.DeckManager>();
            for (int i = StatusCards.Count - 1; i >= 0; i--)
            {
                if (StatusCards[i].Type == Card.CardColor.Red)
                {
                    dm?.AddToDiscardPile(StatusCards[i]);
                    StatusCards.RemoveAt(i);
                }
            }
            OnStatusCardsChanged?.Invoke();
            RecomputeStatusFromStatusCards();
        }

        private void NotifyAccusationChanged()
        {
            AccusationCountChanged?.Invoke(this, currentAccusationCount, currentAccusationLimit);
        }
        #endregion
    }
    
}