using Salem.Players;
using TMPro;
using UnityEngine;

namespace Salem.UI
{
    public class WinningPlayerEntryUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text playerNameText;

        public void Bind(Player player)
        {
            if (playerNameText == null)
            {
                Debug.LogError("[WinningPlayerEntryUI] Missing playerNameText reference.");
                return;
            }

            if (player == null)
            {
                playerNameText.text = "Unknown Player";
                return;
            }

            playerNameText.text = player.PlayerNameText;
        }
    }
}