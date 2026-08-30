using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Salem.Networking; // PUBLIC DTOs ONLY — see the masking-boundary banner in HostTableView.cs.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// One SEAT at the host (TV) table, bound to a public <see cref="PublicPlayerMsg"/>. Built to the
    /// locked design (docs/host-screen-design.pdf; spec in docs/phase-7-host-seat-design.md).
    ///
    /// Vertical composition, top → bottom:
    ///   1. PLAYED CARDS — five fixed slots. Identical cards STACK into one slot with a "×N" badge
    ///      rather than taking a slot each; unused slots show a dashed placeholder.
    ///   2. PORTRAIT + NAMES + STATS — Town Hall card art, the PLAYER name (primary), the CHARACTER
    ///      name (secondary), then "N IN HAND · X/Y" and "ACCUSATIONS n/limit".
    ///   3. TRYAL ROW — face-up art for revealed tryals, the ONE shared back for the rest.
    ///
    /// Card art resolves through <see cref="HostCardSpriteRegistry"/> from public wire LABELS; the
    /// host has no Card objects to read sprites from. Purely presentational, re-bound on every public
    /// broadcast, and structurally unable to reach a Player model.
    /// </summary>
    public class HostPlayerSeat : MonoBehaviour
    {
        /// <summary>One played-card slot. Identical cards collapse into a single slot with a ×N badge.</summary>
        [System.Serializable]
        private class PlayedSlot
        {
            [Tooltip("The card face.")]
            public Image image;
            [Tooltip("Shown when this slot holds a card.")]
            public GameObject filledRoot;
            [Tooltip("Dashed placeholder shown when this slot is empty.")]
            public GameObject emptyRoot;
            [Tooltip("Offset-shadow decoration shown only when the stack holds MORE THAN ONE card.")]
            public GameObject stackedDecor;
            [Tooltip("ROOT of the \"×N\" badge (the coloured pill). Toggled with the stack — this is " +
                     "the object that must hide, not just its label.")]
            public GameObject badgeRoot;
            [Tooltip("The \"×N\" label INSIDE badgeRoot. Text only; visibility is badgeRoot's job.")]
            public TMP_Text countBadge;
        }

        [Header("Card art")]
        [SerializeField] private HostCardSpriteRegistry sprites;


        [Header("Played cards (red accusations + blue statuses)")]
        [Tooltip("Five fixed slots, matching the design. Identical cards stack with a ×N badge.")]
        [SerializeField] private PlayedSlot[] playedSlots = new PlayedSlot[5];
        [Tooltip("\"+N\" shown only if MORE DISTINCT card types are in play than there are slots.")]
        [SerializeField] private TMP_Text overflowText;

        [Header("Identity")]
        [Tooltip("Town Hall card art — doubles as the seat portrait. Town Hall identity is PUBLIC.")]
        [SerializeField] private Image portraitImage;
        [Tooltip("The PLAYER's chosen display name (primary line).")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("The CHARACTER name (secondary line). Hidden when the player has no Town Hall card.")]
        [SerializeField] private TMP_Text characterNameText;

        [Header("Stats")]
        [Tooltip("\"2 IN HAND · 1/5\" — hand count and tryal progress.")]
        [SerializeField] private TMP_Text statsText;
        [Tooltip("\"ACCUSATIONS 1/7\" — the limit is DYNAMIC (14 with Piety, 8 for George Burroughs).")]
        [SerializeField] private TMP_Text accusationText;

        [Header("Tryal row")]
        [Tooltip("Five fixed slots (the maximum any player holds). Extras hide at lower counts.")]
        [SerializeField] private Image[] tryalSlots = new Image[5];

        [Header("Effect badges")]
        [SerializeField] private Transform effectBadgeContainer;
        [Tooltip("Pill ROOT (the Image is the pill background) with a TMP_Text child for the label.")]
        [SerializeField] private Image effectBadgePrefab;
        [Tooltip("Used for cards with no accent authored in the sprite registry.")]
        [SerializeField] private Color effectBadgeDefault = new Color(0.486f, 0.169f, 0.137f); // #7c2b23

        [Header("State")]
        [Tooltip("Ember ring shown behind the ACTIVE player's seat. Pulses while visible.")]
        [SerializeField] private GameObject turnHighlight;
        [Tooltip("Optional CanvasGroup on turnHighlight — drives the ember pulse.")]
        [SerializeField] private CanvasGroup turnHighlightGroup;
        [SerializeField] private float pulseSeconds = 2.4f;
        [SerializeField] private float pulseMinAlpha = 0.5f;
        [Tooltip("The 'HANGED' stamp overlay shown on an eliminated seat.")]
        [SerializeField] private GameObject eliminatedOverlay;
        [Tooltip("Whole-seat group dimmed when eliminated.")]
        [SerializeField] private CanvasGroup seatGroup;
        [SerializeField, Range(0f, 1f)] private float eliminatedAlpha = 0.45f;

        private readonly List<Image> effectBadges = new();
        private readonly List<string> labelBuffer = new();
        private readonly List<string> stackLabels = new();
        private readonly List<int> stackCounts = new();

        // Town Hall identity is public, but the wire field goes EMPTY at elimination
        // (Player.OnElimination nulls the card). Cache the last non-empty value so an eliminated seat
        // keeps showing who they were — rather than reaching for another data source.
        private string lastKnownTownHall;
        private bool isTurn;

        public void Bind(PublicPlayerMsg p, bool isPlayerTurn)
        {
            if (p == null) return;

            isTurn = isPlayerTurn && !p.eliminated; // eliminated is never "the turn" visually

            RenderIdentity(p);
            RenderStats(p);
            RenderPlayedCards(p.accusationCards, p.statusCards);
            RenderEffectBadges(p.statusCards);
            RenderTryals(p.tryalTotal, p.revealedTryals);
            RenderState(p.eliminated);
        }

        private void Update()
        {
            // Ember pulse on the active seat. Cheap, and avoids an Animator on twelve seats.
            if (turnHighlightGroup == null) return;
            turnHighlightGroup.alpha = isTurn
                ? Mathf.Lerp(pulseMinAlpha, 1f,
                             0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.01f, pulseSeconds)))
                : 1f;
        }

        private void RenderIdentity(PublicPlayerMsg p)
        {
            if (nameText != null) nameText.text = p.displayName;

            if (!string.IsNullOrEmpty(p.townHall)) lastKnownTownHall = p.townHall;

            if (characterNameText != null)
            {
                bool has = !string.IsNullOrEmpty(lastKnownTownHall);
                characterNameText.gameObject.SetActive(has);
                if (has) characterNameText.text = lastKnownTownHall;
            }

            if (portraitImage != null)
            {
                var art = string.IsNullOrEmpty(lastKnownTownHall) || sprites == null
                    ? null
                    : sprites.Get(lastKnownTownHall);
                portraitImage.sprite = art;
                portraitImage.enabled = art != null;
            }
        }

        private void RenderStats(PublicPlayerMsg p)
        {
            int revealed = p.revealedTryals?.Length ?? 0;

            if (statsText != null)
                statsText.text = $"{p.handCount} IN HAND · {revealed}/{p.tryalTotal}";

            // The limit is NOT a constant 7: Piety doubles it and George Burroughs' base is 8. The
            // design's hardcoded "/7" was mock data.
            if (accusationText != null)
                accusationText.text = $"ACCUSATIONS {p.accusations}/{p.accusationLimit}";
        }

        /// <summary>
        /// Red accusations and blue statuses are two wire fields but one physical pile, so they share
        /// one row (reds first). Identical cards COLLAPSE into a single slot carrying a "×N" badge —
        /// three Accusation cards occupy one slot, not three.
        /// </summary>
        private void RenderPlayedCards(string[] accusationCards, string[] statusCards)
        {
            labelBuffer.Clear();
            if (accusationCards != null) labelBuffer.AddRange(accusationCards);
            if (statusCards != null) labelBuffer.AddRange(statusCards);

            // Group by label, preserving first-appearance order so the row is stable between
            // broadcasts rather than reshuffling whenever a card is added.
            stackLabels.Clear();
            stackCounts.Clear();
            foreach (var label in labelBuffer)
            {
                if (string.IsNullOrEmpty(label)) continue;
                int at = stackLabels.IndexOf(label);
                if (at >= 0) stackCounts[at]++;
                else { stackLabels.Add(label); stackCounts.Add(1); }
            }

            int slots = playedSlots?.Length ?? 0;
            int shown = Mathf.Min(stackLabels.Count, slots);

            for (int i = 0; i < slots; i++)
            {
                var slot = playedSlots[i];
                if (slot == null) continue;

                bool filled = i < shown;
                if (slot.filledRoot != null) slot.filledRoot.SetActive(filled);
                if (slot.emptyRoot != null) slot.emptyRoot.SetActive(!filled);
                if (!filled) continue;

                if (slot.image != null)
                {
                    var art = sprites != null ? sprites.Get(stackLabels[i]) : null;
                    slot.image.sprite = art;
                    slot.image.enabled = art != null;
                }

                bool stacked = stackCounts[i] > 1;
                if (slot.stackedDecor != null) slot.stackedDecor.SetActive(stacked);

                // Hide the badge ROOT (the coloured pill), not just its label — toggling only the
                // text left the crimson circle drawn over the card corner permanently.
                if (slot.badgeRoot != null) slot.badgeRoot.SetActive(stacked);
                if (slot.countBadge != null)
                {
                    if (slot.badgeRoot == null) slot.countBadge.gameObject.SetActive(stacked);
                    if (stacked) slot.countBadge.text = $"×{stackCounts[i]}";
                }
            }

            int hiddenTypes = stackLabels.Count - shown;
            if (overflowText != null)
            {
                overflowText.gameObject.SetActive(hiddenTypes > 0);
                if (hiddenTypes > 0) overflowText.text = $"+{hiddenTypes}";
            }
        }

        private void RenderEffectBadges(string[] statusCards)
        {
            if (effectBadgeContainer == null || effectBadgePrefab == null) return;

            int n = statusCards?.Length ?? 0;

            while (effectBadges.Count < n)
                effectBadges.Add(Instantiate(effectBadgePrefab, effectBadgeContainer));

            for (int i = 0; i < effectBadges.Count; i++)
            {
                bool used = i < n && !string.IsNullOrEmpty(statusCards[i]);
                effectBadges[i].gameObject.SetActive(used);
                if (!used) continue;

                // The badge ROOT is the pill background; its label is a child. Looking DOWN the
                // hierarchy keeps the recolour scoped to this badge — searching upwards could climb
                // past the pill and tint the whole container.
                effectBadges[i].color = sprites != null
                    ? sprites.GetAccent(statusCards[i], effectBadgeDefault)
                    : effectBadgeDefault;

                var label = effectBadges[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = statusCards[i].ToUpperInvariant();
            }
        }

        /// <summary>
        /// Tryals in front of this seat: face-up art for revealed ones, the ONE shared card back for
        /// every unrevealed one. Slot count comes from <c>tryalTotal</c> (3–5 by player count) — the
        /// design's fixed "/5" was mock data.
        ///
        /// PRIVACY: <paramref name="revealed"/> holds ONLY already-revealed labels, canonically
        /// sorted and position-free. The face-down remainder is drawn purely from a COUNT, so this
        /// method has no hidden identity to leak even in principle — the host was never sent one.
        ///
        /// Per the locked design there is NO per-type border or glow: the card ART distinguishes
        /// Witch / Not a Witch / Constable, and the only ring on a seat is the active-turn ember.
        /// </summary>
        private void RenderTryals(int total, string[] revealed)
        {
            if (tryalSlots == null) return;

            int revealedCount = revealed?.Length ?? 0;
            if (revealedCount > total) revealedCount = total; // defensive: never draw more than held

            for (int i = 0; i < tryalSlots.Length; i++)
            {
                var slot = tryalSlots[i];
                if (slot == null) continue;

                bool used = i < total;
                slot.gameObject.SetActive(used);
                if (!used) continue;

                slot.sprite = i < revealedCount
                    ? (sprites != null ? sprites.Get(revealed[i]) : null)
                    : (sprites != null ? sprites.Back : null);
                slot.enabled = slot.sprite != null;
            }
        }

        private void RenderState(bool eliminated)
        {
            if (turnHighlight != null) turnHighlight.SetActive(isTurn);
            if (eliminatedOverlay != null) eliminatedOverlay.SetActive(eliminated);
            if (seatGroup != null) seatGroup.alpha = eliminated ? eliminatedAlpha : 1f;
        }
    }
}
