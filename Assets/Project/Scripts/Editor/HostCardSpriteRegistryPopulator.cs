// EDITOR-ONLY. Lives in Assets/Project/Scripts/Editor/ — Unity's special "Editor" folder rule puts
// this in Assembly-CSharp-Editor, so it is never in a player build.
//
// ⚠ IT LIVES HERE DELIBERATELY, NOT IN Scripts/UI/HostDisplay/. Populating the registry requires
// reading Card/TryalCard ScriptableObjects, which would breach the HostDisplay masking-boundary grep
// (see the banner in HostTableView.cs). The ASSET this tool produces is pure data — labels and
// sprite references — and carries no model reference, so the runtime side stays clean.

using System.Collections.Generic;
using System.Linq;
using Salem.Cards;
using Salem.UI.HostDisplay;
using UnityEditor;
using UnityEngine;

namespace Salem.EditorTools
{
    /// <summary>
    /// One-shot populator for <see cref="HostCardSpriteRegistry"/>. Scans every Card ScriptableObject
    /// in the project and fills the registry's label→face-up-sprite table from
    /// <c>Card.Name</c> → <c>Card.RevealedCardImage</c>, plus the single shared face-down back from a
    /// tryal's <c>HiddenCardImage</c>.
    ///
    /// Re-runnable: it rebuilds the table from scratch, so adding card art means re-running this
    /// rather than hand-editing 25 rows.
    /// </summary>
    public static class HostCardSpriteRegistryPopulator
    {
        private const string MenuPath = "Tools/Salem/Populate Host Card Sprite Registry";

        [MenuItem(MenuPath)]
        public static void Populate()
        {
            var registry = ResolveRegistry();
            if (registry == null)
            {
                EditorUtility.DisplayDialog(
                    "Host Card Sprite Registry",
                    "No HostCardSpriteRegistry asset found.\n\n" +
                    "Create one first: right-click in the Project window ▸ Create ▸ Card Game ▸ " +
                    "Host Card Sprite Registry.",
                    "OK");
                return;
            }

            var cards = LoadAllCards();
            if (cards.Count == 0)
            {
                EditorUtility.DisplayDialog("Host Card Sprite Registry",
                    "No Card assets found in the project.", "OK");
                return;
            }

            // Preserve hand-authored presentation metadata across re-runs. Descriptions for the
            // IN EFFECT panel (e.g. Asylum's "Recipient cannot be eliminated during the night") and
            // badge accents are authored by hand for the blue cards — rebuilding the table blind
            // would silently delete them every time someone re-ran this.
            var keptDescription = new Dictionary<string, string>();
            var keptAccent = new Dictionary<string, Color>();
            foreach (var existing in registry.Entries ?? System.Array.Empty<HostCardSpriteRegistry.Entry>())
            {
                if (string.IsNullOrWhiteSpace(existing.label)) continue;
                var k = HostCardSpriteRegistry.Normalize(existing.label);
                if (!string.IsNullOrWhiteSpace(existing.description)) keptDescription[k] = existing.description;
                if (existing.accent.a > 0f) keptAccent[k] = existing.accent;
            }

            var entries = new List<HostCardSpriteRegistry.Entry>();
            var seen = new HashSet<string>();
            var missingArt = new List<string>();
            var duplicates = new List<string>();

            foreach (var card in cards.OrderBy(c => c.Name))
            {
                if (string.IsNullOrWhiteSpace(card.Name)) continue;

                // Black cards are never shown in front of a player on the host board. Harmless to
                // include, but skipping keeps the table to what the seat can actually render.
                if (Card.IsBlackCard(card)) continue;

                var key = HostCardSpriteRegistry.Normalize(card.Name);
                if (!seen.Add(key))
                {
                    duplicates.Add(card.Name);
                    continue;
                }

                if (card.RevealedCardImage == null)
                {
                    missingArt.Add(card.Name);
                    continue;
                }

                // Town Hall cards carry their own rules text; everything else keeps whatever was
                // authored by hand (blank until someone fills it in).
                string description = keptDescription.TryGetValue(key, out var d) ? d : null;
                if (string.IsNullOrWhiteSpace(description) && card is TownHallCard th)
                    description = th.GetRulesText();

                entries.Add(new HostCardSpriteRegistry.Entry
                {
                    label = card.Name,
                    sprite = card.RevealedCardImage,
                    description = description ?? string.Empty,
                    accent = keptAccent.TryGetValue(key, out var a) ? a : default,
                });
            }

            var back = ResolveCardBack(cards);

            var so = new SerializedObject(registry);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("label").stringValue = entries[i].label;
                element.FindPropertyRelative("sprite").objectReferenceValue = entries[i].sprite;
                element.FindPropertyRelative("description").stringValue = entries[i].description;
                element.FindPropertyRelative("accent").colorValue = entries[i].accent;
            }

            if (back != null)
            {
                so.FindProperty("cardBack").objectReferenceValue = back;
            }

            so.ApplyModifiedProperties();
            registry.InvalidateCache();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            Debug.Log(BuildReport(registry, entries.Count, back, missingArt, duplicates), registry);
        }

        /// <summary>Selected registry if one is selected, else the only one in the project.</summary>
        private static HostCardSpriteRegistry ResolveRegistry()
        {
            if (Selection.activeObject is HostCardSpriteRegistry selected) return selected;

            var guids = AssetDatabase.FindAssets($"t:{nameof(HostCardSpriteRegistry)}");
            if (guids.Length == 0) return null;

            if (guids.Length > 1)
            {
                Debug.LogWarning($"[HostCardSpriteRegistry] {guids.Length} registry assets found — " +
                                 "using the first. Select one in the Project window to target it.");
            }
            return AssetDatabase.LoadAssetAtPath<HostCardSpriteRegistry>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static List<Card> LoadAllCards()
        {
            return AssetDatabase.FindAssets($"t:{nameof(Card)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Card>)
                .Where(c => c != null)
                .ToList();
        }

        /// <summary>
        /// The single shared face-down back. Prefers a TryalCard's HiddenCardImage (that IS the
        /// tryal back); falls back to any card that has one.
        /// </summary>
        private static Sprite ResolveCardBack(List<Card> cards)
        {
            var fromTryal = cards.OfType<TryalCard>()
                                 .FirstOrDefault(t => t.HiddenCardImage != null);
            if (fromTryal != null) return fromTryal.HiddenCardImage;

            return cards.FirstOrDefault(c => c.HiddenCardImage != null)?.HiddenCardImage;
        }

        private static string BuildReport(
            HostCardSpriteRegistry registry,
            int count,
            Sprite back,
            List<string> missingArt,
            List<string> duplicates)
        {
            var report = $"[HostCardSpriteRegistry] Populated \"{registry.name}\" with {count} label→sprite entries. " +
                         $"Face-down back: {(back != null ? back.name : "NONE — set it manually")}.";

            if (missingArt.Count > 0)
                report += $"\n  Skipped (no RevealedCardImage): {string.Join(", ", missingArt)}";

            if (duplicates.Count > 0)
                report += $"\n  Skipped (duplicate label): {string.Join(", ", duplicates)}";

            return report;
        }
    }
}
