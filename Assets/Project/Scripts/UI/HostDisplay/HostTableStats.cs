using TMPro;
using UnityEngine;
using Salem.Networking; // PUBLIC DTOs ONLY — see the masking-boundary banner in HostTableView.cs.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// The three tallies in the middle of the Meeting House: WITCHES REVEALED / TRYALS FLIPPED /
    /// STILL LIVING.
    ///
    /// All three are DERIVED from the public board — nothing new is broadcast for them. That is
    /// deliberate: a derived number cannot leak, because it can only ever restate what the seats
    /// already show.
    /// </summary>
    public class HostTableStats : MonoBehaviour
    {
        [Header("Counters")]
        [Tooltip("Revealed witch CARDS, not witch players — see the note on CountRevealedWitches.")]
        [SerializeField] private TMP_Text witchesRevealedText;
        [SerializeField] private TMP_Text tryalsFlippedText;
        [SerializeField] private TMP_Text stillLivingText;

        public void Render(GameStateUpdateMsg state)
        {
            var players = state?.players;
            if (players == null) return;

            int witches = 0, flipped = 0, living = 0;

            foreach (var p in players)
            {
                if (p == null) continue;

                if (!p.eliminated) living++;

                var revealed = p.revealedTryals;
                if (revealed == null) continue;

                flipped += revealed.Length;
                foreach (var label in revealed)
                {
                    if (IsWitchLabel(label)) witches++;
                }
            }

            Set(witchesRevealedText, witches);
            Set(tryalsFlippedText, flipped);
            Set(stillLivingText, living);
        }

        /// <summary>
        /// ⚠ EXACT match, never Contains — "Not a Witch" CONTAINS "Witch", so a substring test would
        /// count every innocent reveal as a witch and the headline number would be nonsense.
        ///
        /// This counts revealed witch CARDS, which is the only reading the public board supports: a
        /// player holding two witch cards with one revealed contributes 1 and is still alive. Do not
        /// "fix" this into a player count.
        /// </summary>
        private static bool IsWitchLabel(string label) =>
            string.Equals(label, "Witch", System.StringComparison.OrdinalIgnoreCase);

        private static void Set(TMP_Text text, int value)
        {
            if (text != null) text.text = value.ToString();
        }
    }
}
