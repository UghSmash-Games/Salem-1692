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

public class DeckManager : MonoBehaviour
{
    #region Vars
    [Tooltip("Populate with cards in Inspector")]
    [SerializeField] private List<Card> Deck = new List<Card>();
    private List<Card> DiscardPile = new List<Card>();
    #endregion

    #region Standard Functions
    private void Start()
    {
        InitializeDeck();
    }
    #endregion

    #region Accessor Functions
    public void DrawCard(Player player)
    {
        if (Deck.Count == 0)
        {
            ReshuffleDiscardPile();
        }
        Card drawnCard = Deck[0];
        Deck.RemoveAt(0);
        player.HandManager.AddCard(drawnCard);
        if (drawnCard.Type == "Black")
        {
            ResolveBlackCardEffect(drawnCard);
        }
    }

    public void DrawMultipleCards(Player player, int count)
    {
        for (int i = 0; i < count; i++)
        {
            DrawCard(player);
        }
    }
    #endregion

    #region Helper Functions
    private void ResolveBlackCardEffect(Card card)
    {
        Debug.Log("Function Not Implemented");
        throw new System.NotImplementedException();
    }

    private void ShuffleDeck()
    {
        for (int i = 0; i < Deck.Count; i++)
        {
            Card temp = Deck[i];
            int randomIndex = Random.Range(0, Deck.Count);
            Deck[i] = Deck[randomIndex];
            Deck[randomIndex] = temp;
        }
    }

    private void ReshuffleDiscardPile()
    {
        Deck.AddRange(DiscardPile);
        DiscardPile.Clear();
        ShuffleDeck();
    }

    private void InitializeDeck()
    {
        ShuffleDeck();
    }
    #endregion
}
