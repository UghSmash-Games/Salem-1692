using TMPro;
using UnityEngine;

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// One row in the "What Has Passed" rail: a monospaced timestamp beside serif body text, per the
    /// locked design. Purely presentational — it receives two already-rendered strings and knows
    /// nothing about game state.
    /// </summary>
    public class HostEventLogEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text bodyText;

        public void Set(string time, string body)
        {
            if (timeText != null) timeText.text = time;
            if (bodyText != null) bodyText.text = body;
        }
    }
}
