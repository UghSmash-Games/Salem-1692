using System.Collections.Generic;
using UnityEngine;
using Salem.Networking; // PUBLIC DTOs ONLY — see the masking-boundary banner in HostTableView.cs.

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// The "IN EFFECT" panel at the top of the event rail: one row per persistent card currently in
    /// front of a player — Asylum, Piety, Matchmaker, Stocks, the Black Cat.
    ///
    /// Everything comes from the PUBLIC board. <c>PublicPlayerMsg.statusCards</c> is already
    /// narrowed to non-Red (accusations live in <c>accusationCards</c>), so it IS the set of
    /// persistent cards — no colour logic is needed here, which is the point of having split them
    /// on the wire.
    ///
    /// The rules TEXT is static copy from <see cref="HostCardSpriteRegistry"/>, not game state, so
    /// it needs no wire field.
    /// </summary>
    public class HostInEffectPanel : MonoBehaviour
    {
        [Header("Rows")]
        [SerializeField] private Transform rowContainer;
        [SerializeField] private HostInEffectRow rowPrefab;
        [Tooltip("Shown only when nothing is in play, e.g. \"No cards in play.\"")]
        [SerializeField] private GameObject emptyStateRoot;

        [Header("Copy")]
        [Tooltip("Supplies each card's rules text and accent colour.")]
        [SerializeField] private HostCardSpriteRegistry sprites;
        [Tooltip("Accent bar colour for cards with no accent authored in the registry.")]
        [SerializeField] private Color defaultAccent = new Color(0.788f, 0.541f, 0.247f); // #C98A3F

        private readonly List<HostInEffectRow> rows = new();

        public void Render(GameStateUpdateMsg state)
        {
            var players = state?.players;
            if (rowContainer == null || rowPrefab == null) return;

            int used = 0;

            if (players != null)
            {
                foreach (var p in players)
                {
                    if (p?.statusCards == null) continue;

                    // A dead player's cards are discarded on elimination, so statusCards should
                    // already be empty — skipping is belt-and-braces, not a behaviour change.
                    if (p.eliminated) continue;

                    foreach (var label in p.statusCards)
                    {
                        if (string.IsNullOrEmpty(label)) continue;

                        var row = GetRow(used++);
                        row.Set(
                            label,
                            sprites != null ? sprites.GetDescription(label) : string.Empty,
                            (p.displayName ?? string.Empty).ToUpperInvariant(),
                            sprites != null ? sprites.GetAccent(label, defaultAccent) : defaultAccent);
                    }
                }
            }

            // Park the surplus rather than destroying — Render runs on every public broadcast.
            for (int i = used; i < rows.Count; i++)
            {
                if (rows[i] != null) rows[i].gameObject.SetActive(false);
            }

            if (emptyStateRoot != null) emptyStateRoot.SetActive(used == 0);
        }

        private HostInEffectRow GetRow(int index)
        {
            while (rows.Count <= index) rows.Add(Instantiate(rowPrefab, rowContainer));

            var row = rows[index];
            row.gameObject.SetActive(true);
            return row;
        }
    }
}
