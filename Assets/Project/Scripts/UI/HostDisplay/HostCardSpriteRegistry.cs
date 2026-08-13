// MASKING BOUNDARY — see the banner in HostTableView.cs. This file references ONLY UnityEngine and
// string labels. It must NEVER reference Player, PlayerService, TryalCard, HandManager, or
// StatusCards. It is deliberately a dumb label→Sprite table: it holds no game state, so it cannot
// be asked a question about a card the host was not explicitly told about.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// Resolves a PUBLIC card label from the wire (e.g. "Witch", "Piety", "Abigail Williams") to its
    /// face-up sprite, so the host TV can draw real card art while being fed only public DTOs.
    ///
    /// WHY THIS EXISTS: the legacy board read <c>card.RevealedCardImage</c> straight off the
    /// ScriptableObject. The host has no Card object — only JSON strings — so the sprite lookup has
    /// to live on this side of the boundary.
    ///
    /// PRIVACY PROPERTY (the important one): <see cref="Back"/> is a SINGLE shared sprite, not a
    /// per-card lookup. A face-down tryal is rendered by repeating that one image
    /// (<c>tryalTotal - revealedTryals.Length</c> times), so there is no code path in which an
    /// unrevealed card's identity could select a sprite — the host is never sent one.
    /// </summary>
    [CreateAssetMenu(fileName = "HostCardSpriteRegistry",
                     menuName = "Card Game/Host Card Sprite Registry")]
    public class HostCardSpriteRegistry : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Exact PUBLIC label as it appears on the wire. Matching is normalized, so " +
                     "\"Not a Witch\" and \"NotAWitch\" are equivalent.")]
            public string label;

            [Tooltip("FACE-UP art for this card.")]
            public Sprite sprite;

            [Tooltip("Rules text for the IN EFFECT panel, e.g. \"Recipient cannot be eliminated " +
                     "during the night\". STATIC COPY — never game state, so it needs no wire field.")]
            [TextArea(1, 3)]
            public string description;

            [Tooltip("Accent colour for this card's seat badge. Alpha 0 = use the caller's default.")]
            public Color accent;
        }

        [Header("Shared face-down back")]
        [Tooltip("Used for EVERY unrevealed tryal. Intentionally one sprite for all of them — see " +
                 "the privacy note in the class summary.")]
        [SerializeField] private Sprite cardBack;

        [Header("Face-up art by public label")]
        [Tooltip("Populate via Tools ▸ Salem ▸ Populate Host Card Sprite Registry.")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private Dictionary<string, Entry> lookup;
        private HashSet<string> warnedLabels;

        /// <summary>The one shared face-down sprite. Never varies by card.</summary>
        public Sprite Back => cardBack;

        /// <summary>Read-only view of the configured entries (used by the Editor populator).</summary>
        public Entry[] Entries => entries;

        /// <summary>
        /// Resolve a public label to its face-up sprite, or null if unknown (callers hide the slot).
        /// Logs at most ONCE per distinct missing label so a typo surfaces without spamming a console
        /// that is being written to on every broadcast.
        /// </summary>
        public Sprite Get(string label) => TryGet(label, out var e) ? e.sprite : null;

        /// <summary>Rules text for the IN EFFECT panel. Empty when unknown.</summary>
        public string GetDescription(string label)
            => TryGet(label, out var e) ? e.description : string.Empty;

        /// <summary>
        /// Accent colour for this card's badge. Entries left at alpha 0 fall back to
        /// <paramref name="fallback"/>, so only cards that need a distinct colour must be authored.
        /// </summary>
        public Color GetAccent(string label, Color fallback)
        {
            if (!TryGet(label, out var e)) return fallback;
            return e.accent.a > 0f ? e.accent : fallback;
        }

        private bool TryGet(string label, out Entry entry)
        {
            entry = default;
            if (string.IsNullOrWhiteSpace(label)) return false;

            EnsureLookup();

            var key = Normalize(label);
            if (lookup.TryGetValue(key, out entry)) return true;

            warnedLabels ??= new HashSet<string>();
            if (warnedLabels.Add(key))
            {
                Debug.LogWarning($"[HostCardSpriteRegistry] No entry for public label \"{label}\" " +
                                 $"(normalized \"{key}\"). The slot will render empty.", this);
            }
            return false;
        }

        /// <summary>
        /// Normalizes a label for matching: trims, lowercases invariantly, and strips ALL whitespace.
        ///
        /// The whitespace strip is load-bearing, not cosmetic. Town Hall labels reach the host as
        /// DISPLAY names ("Abigail Williams") while the source enum is "AbigailWilliams"; both
        /// normalize to "abigailwilliams", so the registry is immune to which one a caller uses.
        /// Same for "Not a Witch" vs "NotAWitch".
        /// </summary>
        public static string Normalize(string label)
        {
            if (string.IsNullOrEmpty(label)) return string.Empty;

            var sb = new System.Text.StringBuilder(label.Length);
            foreach (var ch in label)
            {
                if (!char.IsWhiteSpace(ch)) sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        private void EnsureLookup()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, Entry>();
            if (entries == null) return;

            foreach (var e in entries)
            {
                if (string.IsNullOrWhiteSpace(e.label)) continue;

                var key = Normalize(e.label);
                if (lookup.ContainsKey(key))
                {
                    Debug.LogWarning($"[HostCardSpriteRegistry] Duplicate label \"{e.label}\" " +
                                     $"(normalized \"{key}\") — keeping the first entry.", this);
                    continue;
                }
                lookup[key] = e;
            }
        }

        /// <summary>Drops the cached lookup so Editor edits take effect without a domain reload.</summary>
        public void InvalidateCache()
        {
            lookup = null;
            warnedLabels = null;
        }

        private void OnValidate() => InvalidateCache();
    }
}
