using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// One row in the IN EFFECT panel: card name, its rules text, and who it is on.
    /// Purely presentational — it receives finished strings and knows nothing about game state.
    /// </summary>
    public class HostInEffectRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("Who the card is in front of, e.g. \"BISHOP\".")]
        [SerializeField] private TMP_Text holderText;
        [Tooltip("The 2px accent bar down the left edge.")]
        [SerializeField] private Image accentBar;

        public void Set(string cardName, string description, string holder, Color accent)
        {
            if (nameText != null) nameText.text = cardName;
            if (holderText != null) holderText.text = holder;

            if (descriptionText != null)
            {
                bool has = !string.IsNullOrEmpty(description);
                descriptionText.gameObject.SetActive(has);
                if (has) descriptionText.text = description;
            }

            if (accentBar != null) accentBar.color = accent;
        }
    }
}
