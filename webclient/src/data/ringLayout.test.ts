/**
 * Ring geometry — every row of the LOCKED table in docs/phase-7-host-seat-design.md §1.
 *
 * A silent divergence from Unity's HostTableView would seat the same player in a different chair on
 * each screen. That is not a cosmetic difference: the mirror exists so a player who cannot see the
 * host TV sees the SAME table, and a reshuffled ring makes the two rooms disagree about who is
 * sitting next to whom — which matters in a game about who passed you a card.
 */

import { describe, it, expect } from 'vitest';
import { distribute, ringSlots, verticalSeatUnit } from './ringLayout';

describe('distribute — the locked table', () => {
  // top / right / bottom / left
  const TABLE: Record<number, [number, number, number, number]> = {
    4: [1, 1, 1, 1],
    5: [1, 1, 2, 1],
    6: [2, 1, 2, 1],
    7: [2, 1, 3, 1],
    8: [3, 1, 3, 1],
    9: [2, 2, 3, 2],
    10: [3, 2, 3, 2],
    11: [3, 2, 4, 2],
    12: [4, 2, 4, 2],
  };

  for (const [nStr, [top, right, bottom, left]] of Object.entries(TABLE)) {
    const n = Number(nStr);
    it(`${n} players -> ${top}/${right}/${bottom}/${left}`, () => {
      expect(distribute(n)).toEqual({ top, right, bottom, left });
    });
  }

  it('always seats everyone exactly once', () => {
    for (let n = 4; n <= 12; n++) {
      const r = distribute(n);
      expect(r.top + r.right + r.bottom + r.left, `n=${n}`).toBe(n);
    }
  });

  it('keeps left and right equal — asymmetric sides read as broken', () => {
    for (let n = 4; n <= 12; n++) {
      const r = distribute(n);
      expect(r.left, `n=${n}`).toBe(r.right);
    }
  });

  it('puts the odd extra on the BOTTOM row, nearest the viewer', () => {
    for (let n = 4; n <= 12; n++) {
      const r = distribute(n);
      expect(r.bottom, `n=${n}`).toBeGreaterThanOrEqual(r.top);
    }
  });

  it('caps at the design maximum of 4 per horizontal row and 2 per side', () => {
    for (let n = 4; n <= 12; n++) {
      const r = distribute(n);
      expect(r.top).toBeLessThanOrEqual(4);
      expect(r.bottom).toBeLessThanOrEqual(4);
      expect(r.right).toBeLessThanOrEqual(2);
    }
  });

  it('degrades safely below the 4-player floor', () => {
    expect(distribute(0)).toEqual({ top: 0, right: 0, bottom: 0, left: 0 });
    const two = distribute(2);
    expect(two.top + two.right + two.bottom + two.left).toBe(2);
    expect(two.right).toBe(0); // nothing to wrap around
  });
});

describe('ringSlots — clockwise order', () => {
  it('seats every player exactly once, with no holes', () => {
    for (let n = 4; n <= 12; n++) {
      const s = ringSlots(n);
      const all = [...s.top, ...s.right, ...s.bottom, ...s.left];
      expect(all, `n=${n}`).toHaveLength(n);
      expect(new Set(all).size, `n=${n}`).toBe(n);
      expect(all.every((v) => typeof v === 'number'), `n=${n} has holes`).toBe(true);
    }
  });

  it('REVERSES the bottom and left legs so the ring reads clockwise', () => {
    // 12 players: top 0-3, right 4-5, bottom 6-9, left 10-11.
    const s = ringSlots(12);
    expect(s.top).toEqual([0, 1, 2, 3]);
    expect(s.right).toEqual([4, 5]);
    // Bottom is walked right→left, so the lowest player index sits RIGHTMOST.
    expect(s.bottom).toEqual([9, 8, 7, 6]);
    // Left is walked bottom→top.
    expect(s.left).toEqual([11, 10]);
  });

  it('is continuous around the ring at the 8->9 side step', () => {
    // The one inherent discontinuity: sides go 1->2 and the top row shortens 3->2.
    expect(distribute(8).top).toBe(3);
    expect(distribute(9).top).toBe(2);
    for (const n of [8, 9]) {
      const s = ringSlots(n);
      expect([...s.top, ...s.right, ...s.bottom, ...s.left].sort((a, b) => a - b))
        .toEqual(Array.from({ length: n }, (_, i) => i));
    }
  });
});

describe('verticalSeatUnit — the ring must fit the screen', () => {
  // Real numbers, measured in a 1600x900 browser on a 12-seat board: a full-size seat is 228px and
  // the band the two horizontal rows leave over is 349px. Two seats want 463px, which is exactly
  // the overlap that put the side seats on top of the top and bottom rows.
  const SEAT_PX = 228;
  const BAND_PX = 349;
  const GAP_PX = 7.2; // 0.8vh at 900px

  it('does not shrink a leg that already fits', () => {
    expect(verticalSeatUnit(1, SEAT_PX, BAND_PX, GAP_PX)).toBe(1);
    expect(verticalSeatUnit(0, SEAT_PX, BAND_PX, GAP_PX)).toBe(1);
    expect(verticalSeatUnit(2, SEAT_PX, 900, GAP_PX)).toBe(1);
  });

  it('shrinks two side seats to fit the band exactly', () => {
    const u = verticalSeatUnit(2, SEAT_PX, BAND_PX, GAP_PX);
    expect(2 * SEAT_PX * u + GAP_PX).toBeLessThanOrEqual(BAND_PX);
    expect(u).toBeGreaterThan(0.5);
  });

  it('renders full size before anything has been measured', () => {
    // First paint: refs are still null, so both measurements come back 0.
    expect(verticalSeatUnit(2, 0, 0, GAP_PX)).toBe(1);
  });

  it('never shrinks past half size', () => {
    // A band far too small stops at the floor rather than rendering an unreadable seat.
    expect(verticalSeatUnit(2, SEAT_PX, 40, GAP_PX)).toBe(0.5);
  });
});
