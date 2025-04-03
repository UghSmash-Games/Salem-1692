/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: Add attributes for special effects (IE Conspiracy / Night)
 * FIXME: [Known bugs or issues]
*/
using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Card Game/Card")]
public class Card : ScriptableObject
{
    public string Name;
    public string Type;
    public string Effect;
    public Sprite HiddenCardImage;
    public Sprite RevealedCardImage;
}
