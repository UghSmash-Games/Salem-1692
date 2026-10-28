/**
 * The rectangular ring — a port of Unity's HostTableView.Distribute + SlotFor.
 *
 * 🔴 THE LOCKED RULE (docs/phase-7-host-seat-design.md §1). Both screens must place the same player
 * in the same seat, or the two rooms are looking at different tables:
 *
 *   s      = clamp(ceil((n - 6) / 2), 1, 2)   // per side; LEFT ALWAYS == RIGHT
 *   H      = n - 2s                           // across both horizontal rows
 *   top    = floor(H / 2)
 *   bottom = ceil(H / 2)                      // the odd extra goes to the BOTTOM row
 *
 * Kept a PURE FUNCTION for the same reason the C# is: it can then be reasoned about and tested
 * independently of any rendering. Every row of the locked table is asserted in ringLayout.test.ts.
 *
 * ⚠️ ORDER MATTERS AS MUCH AS COUNT. The ring reads clockwise from the top-left, so the BOTTOM and
 * LEFT legs are walked in REVERSE. Matching the counts but not the order would seat everyone in the
 * right leg and the wrong chair.
 */

export interface RingDistribution {
  top: number;
  right: number;
  bottom: number;
  left: number;
}

/** Seats per leg for `n` players. */
export function distribute(n: number): RingDistribution {
  if (n <= 0) return { top: 0, right: 0, bottom: 0, left: 0 };

  // Below the 4-player floor there is nothing to wrap around — keep everyone on the horizontal rows
  // so the arithmetic can never go negative.
  const s = n >= 4 ? Math.min(2, Math.max(1, Math.ceil((n - 6) / 2))) : 0;

  const h = n - 2 * s;
  const top = Math.floor(h / 2);
  const bottom = h - top; // === ceil(h/2)

  return { top, right: s, bottom, left: s };
}

export interface RingSlots {
  /** Player indices in RENDER order for each leg. */
  top: number[];
  right: number[];
  bottom: number[];
  left: number[];
}

/**
 * Which player index sits where, in the order each leg should be rendered.
 *
 * Mirrors SlotFor: top runs left→right, right runs top→bottom, then bottom is walked right→left and
 * left bottom→top so the sequence travels clockwise around the ring.
 */
export function ringSlots(n: number): RingSlots {
  const ring = distribute(n);
  const slots: RingSlots = {
    top: new Array(ring.top),
    right: new Array(ring.right),
    bottom: new Array(ring.bottom),
    left: new Array(ring.left),
  };

  for (let player = 0; player < n; player++) {
    let i = player;

    if (i < ring.top) {
      slots.top[i] = player;
      continue;
    }
    i -= ring.top;

    if (i < ring.right) {
      slots.right[i] = player;
      continue;
    }
    i -= ring.right;

    if (i < ring.bottom) {
      slots.bottom[ring.bottom - 1 - i] = player; // reversed
      continue;
    }
    i -= ring.bottom;

    if (i < ring.left) {
      slots.left[ring.left - 1 - i] = player; // reversed
    }
  }

  return slots;
}

/** Gap between two seats on a vertical leg, in vh. ⚠ Must stay equal to the literal `gap-[0.8vh]`
 *  class on the vertical leg in MirrorScreen — Tailwind scans source text, so the class cannot be
 *  built from this constant. */
export const SEAT_GAP_VH = 0.8;

/**
 * How much to shrink the seats on a VERTICAL leg so the ring fits the screen, given MEASURED
 * pixels: 1 = full size, smaller = shrink.
 *
 * 🐛 Fixes a real overlap: the board is a fixed 100vh with no scrolling, and at 9-12 players the
 * sides hold 2 seats. Four legs then want four seat-heights of vertical space before the header and
 * gaps — more than exists — so the side seats spilled out of the middle band and drew over the top
 * and bottom rows.
 *
 * The SIDES are what give: the horizontal rows have width to spare, while the middle band's height
 * is only ever what those rows leave over. Unity resolves it the same way (the side columns are
 * layout groups that compress their children), so a shorter side seat is parity, not a divergence.
 *
 * ⚠️ MEASURED, NOT ESTIMATED — deliberately. The first attempt hard-coded the seat height in vh by
 * adding up MirrorSeat's own metrics, and was wrong by 1.4vh (the name/character/stats text block is
 * taller than the portrait beside it), which still overlapped by 11px at 900px tall. Any such
 * constant also silently rots the next time a line of seat text changes. The caller reads the real
 * seat and the real band off the DOM instead; this stays a pure function so it can be reasoned about
 * and tested without a layout engine.
 *
 * There is no feedback loop to worry about: the horizontal rows set the band height and are always
 * rendered at full size, and the side seats are `shrink-0` inside a `min-h-0` flex band, so changing
 * this value cannot change either input.
 *
 * @param seats        seats on this vertical leg
 * @param seatPx       height of a full-size seat (measured on a horizontal row)
 * @param bandPx       height available to the vertical leg
 * @param gapPx        gap between two seats on the leg
 */
export function verticalSeatUnit(seats: number, seatPx: number, bandPx: number, gapPx: number): number {
  // Nothing to fit, or nothing measured yet (first paint) — render full size.
  if (seats <= 1 || seatPx <= 0 || bandPx <= 0) return 1;

  const perSeat = (bandPx - (seats - 1) * gapPx) / seats;

  // Floor at half size: below that a seat stops being readable across a room, and a board that
  // cannot fit legibly is a layout problem to solve, not one to hide by shrinking further.
  return Math.min(1, Math.max(0.5, perSeat / seatPx));
}
