using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Salem.Networking; // PUBLIC DTOs ONLY — see the masking-boundary banner in HostTableView.cs.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// Deck + discard on the HOST (TV) table, from the public <see cref="GameStateUpdateMsg"/> ONLY
    /// (revised 7b — sits at the CENTER of the table like the physical deck). Two card-stack visuals
    /// with count badges; a stack hides itself when its pile is empty. Replaces the legacy
    /// Deck/DiscardPile prefabs — which showed no live counts and whose Deck button
    /// (DrawPileUI → TryDrawTwoCards) was an unauthorized input path on a display-only screen.
    /// Display-only by design: no buttons here, ever.
    /// </summary>
    public class HostDeckView : MonoBehaviour
    {
        [Header("Deck stack")]
        [Tooltip("Card-back stack image; hidden when the deck is empty.")]
        [SerializeField] private Image deckStackImage;
        [SerializeField] private TMP_Text deckCountText;      // count badge, e.g. "37"

        [Header("Discard stack")]
        [Tooltip("Discard stack image; hidden when the discard pile is empty.")]
        [SerializeField] private Image discardStackImage;
        [SerializeField] private TMP_Text discardCountText;   // count badge, e.g. "12"

        [Header("Top discard card (face-up, as on a real table)")]
        [Tooltip("Resolves the top card's art from its public label.")]
        [SerializeField] private HostCardSpriteRegistry sprites;
        [Tooltip("Face-up art of the TOP discard card. Hidden when the pile is empty.")]
        [SerializeField] private Image discardTopImage;
        [Tooltip("Optional caption for the card, e.g. \"Accusation\".")]
        [SerializeField] private TMP_Text discardTopNameText;
        [Tooltip("Dashed placeholder shown ONLY when the discard pile is empty.")]
        [SerializeField] private GameObject discardEmptyRoot;

        [Header("Labels (set empty to show bare numbers)")]
        [SerializeField] private string deckLabel = "Deck";
        [SerializeField] private string discardLabel = "Discard";

        public void Render(GameStateUpdateMsg state)
        {
            if (state == null) return;

            if (deckStackImage != null) deckStackImage.enabled = state.deckCount > 0;
            if (discardStackImage != null) discardStackImage.enabled = state.discardCount > 0;

            if (deckCountText != null) deckCountText.text = Format(deckLabel, state.deckCount);
            if (discardCountText != null) discardCountText.text = Format(discardLabel, state.discardCount);

            RenderTopDiscard(state.topDiscard);
        }

        /// <summary>
        /// The face-up top card of the discard pile. Public by the card rules — a discard pile is
        /// face-up at a physical table — and the wire carries the TOP CARD'S NAME ONLY, never the
        /// ordered pile (see PublicPlayerMsg/GameStateUpdateMsg notes).
        /// </summary>
        private void RenderTopDiscard(string label)
        {
            bool hasCard = !string.IsNullOrEmpty(label);
            var art = hasCard && sprites != null ? sprites.Get(label) : null;

            if (discardTopImage != null)
            {
                discardTopImage.sprite = art;
                discardTopImage.enabled = art != null;
            }

            if (discardTopNameText != null)
            {
                discardTopNameText.gameObject.SetActive(hasCard);
                if (hasCard) discardTopNameText.text = label;
            }

            if (discardEmptyRoot != null) discardEmptyRoot.SetActive(!hasCard);
        }

        private static string Format(string label, int count) =>
            string.IsNullOrEmpty(label) ? count.ToString() : $"{label}  {count}";
    }
}
