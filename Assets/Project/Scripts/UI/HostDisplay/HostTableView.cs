using System.Collections.Generic;
using UnityEngine;
using Salem.Networking; // PUBLIC DTOs ONLY.

// ─────────────────────────────────────────────────────────────────────────────────────────────
// MASKING BOUNDARY (Phase 7): every file under Salem.UI.HostDisplay may reference ONLY the public
// wire DTOs (Salem.Networking messages) + Unity/TMP. It must NEVER touch Player, PlayerService,
// TryalCard, HandManager, or StatusCards. The host TV screen physically cannot render private data.
// A grep of this folder for those types must return ZERO hits.
// ─────────────────────────────────────────────────────────────────────────────────────────────

namespace Salem.UI.HostDisplay
{
    /// <summary>
    /// The HOST (TV) table: positions one <see cref="HostPlayerSeat"/> per public player around a
    /// RECTANGULAR RING — 4 seats across the top, 2 down each side, 4 across the bottom at the
    /// 12-player maximum, per the locked design (docs/host-screen-design.pdf, spec in
    /// docs/phase-7-host-seat-design.md). Replaces the earlier ellipse.
    ///
    /// The ring is DYNAMIC across 4–12 players (see <see cref="Distribute"/>). Seats walk the ring
    /// CLOCKWISE in roster order starting top-left, so roster adjacency equals table adjacency —
    /// which matters because Salem has "first living player to your right" semantics.
    ///
    /// ⚠ The prototype's OWN distribution is a placeholder that only looks right at 12
    /// (`slice(0,4) / slice(4,6) / slice(6,10) / slice(10,12)` — at 4 players it puts all four seats
    /// on the top row and leaves three sides empty). <see cref="Distribute"/> is the intentional
    /// rule; do not "correct" it back towards the prototype.
    ///
    /// LAYOUT MODEL: this class only decides WHICH container each seat belongs to and in what order.
    /// Positioning and sizing are the scene's layout groups, so seats STRETCH to fill their share —
    /// matching the design, where a seat is `100% × 148px` inside a grid cell. Consequently there is
    /// no seat-scaling maths here, and the center ("The Meeting House") reflows on its own as a
    /// sibling cell rather than being computed.
    ///
    /// Expected scene structure (assign the four containers below):
    ///   topRow      — HorizontalLayoutGroup, childControl + childForceExpand on Width
    ///   rightColumn — VerticalLayoutGroup,   childControl + childForceExpand on Height
    ///   bottomRow   — HorizontalLayoutGroup, same as topRow
    ///   leftColumn  — VerticalLayoutGroup,   same as rightColumn
    ///
    /// Seats are bound from <see cref="GameStateUpdateMsg.players"/> ONLY.
    /// </summary>
    public class HostTableView : MonoBehaviour
    {
        /// <summary>Seats per side of the ring. Left ALWAYS equals right.</summary>
        public readonly struct RingDistribution
        {
            public readonly int Top, Right, Bottom, Left;
            public RingDistribution(int top, int right, int bottom, int left)
            {
                Top = top; Right = right; Bottom = bottom; Left = left;
            }
            public int Total => Top + Right + Bottom + Left;
        }

        [Header("Seat prefab")]
        [SerializeField] private HostPlayerSeat seatPrefab;
        [Tooltip("Where pooled-but-unused seats are parked. Defaults to this transform.")]
        [SerializeField] private RectTransform seatPool;

        [Header("Ring containers (scene layout does the positioning)")]
        [Tooltip("Horizontal layout group, left→right. Seats stretch to fill it.")]
        [SerializeField] private RectTransform topRow;
        [Tooltip("Vertical layout group, top→bottom.")]
        [SerializeField] private RectTransform rightColumn;
        [Tooltip("Horizontal layout group, left→right.")]
        [SerializeField] private RectTransform bottomRow;
        [Tooltip("Vertical layout group, top→bottom.")]
        [SerializeField] private RectTransform leftColumn;

        private readonly struct Placement
        {
            public readonly RectTransform Rt;
            public readonly RectTransform Container;
            public readonly int Order;
            public Placement(RectTransform rt, RectTransform container, int order)
            {
                Rt = rt; Container = container; Order = order;
            }
        }

        private readonly List<HostPlayerSeat> seats = new();
        private readonly List<Placement> placements = new();
        private PublicPlayerMsg[] lastPlayers = System.Array.Empty<PublicPlayerMsg>();
        private string lastWhoseTurn;
        private int lastSeatCount = -1;

        private void Reset()
        {
            if (seatPool == null) seatPool = GetComponent<RectTransform>();
        }

        public void Render(GameStateUpdateMsg state)
        {
            if (state == null) return;
            lastPlayers = state.players ?? System.Array.Empty<PublicPlayerMsg>();
            lastWhoseTurn = state.whoseTurn;

            if (seatPrefab == null) return;

            // Pool by index; seat order = roster (join) order, stable like physical seating.
            while (seats.Count < lastPlayers.Length)
                seats.Add(Instantiate(seatPrefab, seatPool != null ? seatPool : transform));

            for (int i = 0; i < seats.Count; i++)
            {
                bool used = i < lastPlayers.Length && lastPlayers[i] != null;
                seats[i].gameObject.SetActive(used);
                if (used)
                {
                    bool isTurn = !string.IsNullOrEmpty(lastWhoseTurn)
                                  && lastPlayers[i].playerId == lastWhoseTurn;
                    seats[i].Bind(lastPlayers[i], isTurn);
                }
            }

            Layout();
        }

        /// <summary>
        /// THE ring rule (locked — docs/phase-7-host-seat-design.md §1). Pure function, no Unity
        /// state, so it can be reasoned about and tested independently of the scene.
        ///
        ///   s      = clamp(ceil((n - 6) / 2), 1, 2)   // per side; LEFT ALWAYS == RIGHT
        ///   H      = n - 2s                           // across both horizontal rows
        ///   top    = floor(H / 2)
        ///   bottom = ceil(H / 2)                      // the odd extra goes to the BOTTOM row
        ///
        /// 4:1/1/1/1 · 5:1/1/2/1 · 6:2/1/2/1 · 7:2/1/3/1 · 8:3/1/3/1
        /// 9:2/2/3/2 · 10:3/2/3/2 · 11:3/2/4/2 · 12:4/2/4/2   (top/right/bottom/left)
        ///
        /// Left == right always (asymmetric sides read as broken; an uneven horizontal row reads as
        /// natural). Sides step to 2 at n=9 to keep the ring as square as possible — that step
        /// necessarily shortens the top row 3→2 between 8 and 9 players, which is inherent to any
        /// four-sided ring and is deliberately placed there so the rounder shape holds across 9–12.
        /// </summary>
        public static RingDistribution Distribute(int n)
        {
            if (n <= 0) return new RingDistribution(0, 0, 0, 0);

            // Below the 4-player floor there is nothing to wrap around — keep everyone on the
            // horizontal rows so the arithmetic below can never go negative.
            int s = n >= 4 ? Mathf.Clamp(Mathf.CeilToInt((n - 6) / 2f), 1, 2) : 0;

            int h = n - 2 * s;
            int top = Mathf.FloorToInt(h / 2f);
            int bottom = h - top;              // == ceil(h/2); the extra lands on the bottom row
            return new RingDistribution(top, s, bottom, s);
        }

        /// <summary>
        /// Assigns each seat to its ring container and orders it within that container. Actual
        /// POSITIONING is done by the scene's layout groups — seats stretch to fill their share,
        /// matching the locked design (where a seat is `100% × 148px` inside a grid cell). This is
        /// why there is no seat-scaling maths here: a row of 2 and a row of 4 both fill their row.
        ///
        /// Only re-parents when the seat count changes; layout groups handle every resize.
        /// </summary>
        private void Layout()
        {
            int n = lastPlayers.Length;
            if (n == lastSeatCount) return;
            lastSeatCount = n;

            var ring = Distribute(n);
            placements.Clear();

            // ── Pass 1: parenting ──
            for (int i = 0; i < seats.Count; i++)
            {
                var rt = seats[i].transform as RectTransform;
                if (rt == null) continue;

                if (i >= n)
                {
                    // Unused pooled seat — park it so it cannot occupy a slot in a layout group.
                    if (seatPool != null && rt.parent != seatPool) rt.SetParent(seatPool, false);
                    continue;
                }

                SlotFor(i, ring, out var container, out int order);
                if (container == null) continue;

                if (rt.parent != container) rt.SetParent(container, false);
                rt.localScale = Vector3.one;
                placements.Add(new Placement(rt, container, order));
            }

            // ── Pass 2: sibling order ──
            // MUST be a separate pass, ascending. SetSiblingIndex(k) only lands correctly once every
            // sibling below k is already settled and the container holds all its children — and the
            // bottom/left legs are ORDER-REVERSED, so interleaving parent+order in one pass
            // scrambles them (a seat asking for index 3 in a container that currently holds 1 child
            // gets clamped to 0, and every later insert shuffles it again).
            placements.Sort((a, b) => a.Order.CompareTo(b.Order));
            foreach (var p in placements)
                p.Rt.SetSiblingIndex(Mathf.Min(p.Order, p.Container.childCount - 1));
        }

        /// <summary>
        /// Which container seat <paramref name="i"/> belongs to, and its order within that container.
        ///
        /// Walks the ring CLOCKWISE from the top-left — across the top, down the right, back across
        /// the bottom, up the left — so roster adjacency equals table adjacency.
        ///
        /// The clockwise walk runs right→left along the bottom and bottom→top up the left side, but
        /// layout groups always lay out left→right / top→bottom. Those two legs are therefore
        /// ORDER-REVERSED so the on-screen ring still reads as one continuous clockwise loop.
        /// </summary>
        private void SlotFor(int i, RingDistribution ring, out RectTransform container, out int order)
        {
            if (i < ring.Top)
            {
                container = topRow; order = i;                      // left → right
                return;
            }
            i -= ring.Top;

            if (i < ring.Right)
            {
                container = rightColumn; order = i;                 // top → bottom
                return;
            }
            i -= ring.Right;

            if (i < ring.Bottom)
            {
                container = bottomRow; order = ring.Bottom - 1 - i; // walk right→left; reversed
                return;
            }
            i -= ring.Bottom;

            container = leftColumn; order = ring.Left - 1 - i;      // walk bottom→top; reversed
        }
    }
}
