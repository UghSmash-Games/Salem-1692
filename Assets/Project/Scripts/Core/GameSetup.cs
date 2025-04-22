/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
 * FIXME: [Known bugs or issues]
*/
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameSetup : MonoBehaviour
{
    #region Vars
    [Tooltip("Must Be Ordered: Constable, Witch, Not A Witch")]
    [SerializeField] private ScriptableObject[] TryalCards;
    private List<TryalCard> TryalDeck = new List<TryalCard>();
    private DeckManager DeckManager;
    #endregion

    #region Standard Functions
    void Awake()
    {
        DeckManager = GetComponent<DeckManager>();
    }
    #endregion

    #region Accessor Functions
    //Called In GamePhaseManager durning Setup
    public void SetupNewGame(List<Player> players, int count)
    {
        SetupTryalCards(players);
        SetupInitalHand(players, count);
    }
    #endregion

    #region Helper Functions
    private void SetupTryalCards(List<Player> players)
    {
        int numberOfWitches = players.Count / 3; 
        Debug.Log($"There are {numberOfWitches} Witches.");

        int numberOfTryalCardsNeeded = players.Count * 5;

        // Add cards to the deck, Start with the Constable
        TryalCard constableCard = (TryalCard)Instantiate(TryalCards[0]);
        constableCard.TryalCardType = TryalCardType.Constable;
        TryalDeck.Add(constableCard);

        //Create our Witch Cards
        for (int i = 0; i < numberOfWitches; i++) 
        {
            TryalCard card = (TryalCard)Instantiate(TryalCards[1]);
            card.TryalCardType = TryalCardType.Witch;
            TryalDeck.Add(card);
        }

        //Finish the deck with NotAWitch Cards
        for (int i = TryalDeck.Count; i < numberOfTryalCardsNeeded; i++) 
        {
            TryalCard card = (TryalCard)Instantiate(TryalCards[2]);
            card.TryalCardType = TryalCardType.NotAWitch;
            TryalDeck.Add(card);
        } 

        Debug.Log($"There are {TryalDeck.Count} total Tryal Cards.");

        // Shuffle and distribute
        ShuffleTryalDeck(TryalDeck);

        foreach (var player in players)
        {
            player.TryalCards = DrawCards(5, TryalDeck);
            player.DetermineRole();
        }
    }

    //Give the players their starting hand
    private void SetupInitalHand(List<Player> players, int count)
    {
        foreach (var player in players)
        {
            DeckManager.DrawMultipleCards(player, count);
        }
    }

    //Have players draw their Tryal Cards
    private List<TryalCard> DrawCards(int count, List<TryalCard> deck)
    {
        List<TryalCard> cards = deck.Take(count).ToList();
        deck.RemoveRange(0, count);
        return cards;
    }

    private void ShuffleTryalDeck(List<TryalCard> deck)
    {
        deck = deck.OrderBy(x => Random.value).ToList();
    }
    #endregion
}
