/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
 * FIXME: [Known bugs or issues]
*/
using UnityEngine;

public enum TryalCardType { Witch, NotAWitch, Constable }

[System.Serializable]
[CreateAssetMenu(fileName = "NewTryalCard", menuName = "Card Game/TryalCard")]
public class TryalCard : Card
{
    #region Vars
    public TryalCardType TryalCardType;
    public bool IsRevealed;

    private TryalCardUI tryalCardUI;
    #endregion

    #region Standard Functions
    void Awake()
    {
        tryalCardUI = (TryalCardUI)FindFirstObjectByType(typeof(TryalCardUI));
    }
    #endregion

    #region Accessor Functions
    public void Reveal()
    {
        IsRevealed = true;
        Debug.Log($"Tryal Card Revealed: {TryalCardType}");
        tryalCardUI.UpdateCardVisual(this);
    }
    #endregion
}
