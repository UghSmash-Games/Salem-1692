/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Salem.Players;

namespace Salem.UI
{
    public class WinningPlayerEntryUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;

        public void Bind(Player player)
        {
            nameText.text = player.PlayerNameText;

            
            var icon = player.townHallCardIcon; 
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }
        }
    }
}
