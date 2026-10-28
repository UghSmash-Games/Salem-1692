/**
 * The seat a phone is holding, persisted across socket drops and page loads.
 *
 * 🔴 WHY THIS EXISTS. A phone loses its socket constantly in normal play — the screen locks, the
 * browser backgrounds the tab, wifi blips, someone reloads. Before reconnection existed the seat was
 * gone for good: `join_room` always mints a fresh `playerId`, so the returning player became a
 * stranger to a host that still held their tryals under the old id. Replaying this record on every
 * `connect` is what puts them back in their own chair.
 *
 * 🔴 THE TOKEN IS A CREDENTIAL. It is the only thing that authorizes a rejoin, because `playerId` is
 * public — it appears in every `game_state_update`. Never render it, never log it, never put it in a
 * URL (a query string leaks into history, referrers and server logs), and never send it anywhere but
 * `rejoin_room` on our own server.
 *
 * ⚠️ localStorage, NOT sessionStorage. sessionStorage dies with the tab, and a phone browser killing
 * a backgrounded tab is one of the exact failures this is here to survive. The trade-off is that the
 * record outlives the game, which is why a stale or rejected seat is cleared eagerly (`clearSeat`)
 * rather than left to rot: an expired token would otherwise make every later join attempt look like
 * a failed reconnection.
 */

const SEAT_KEY = 'salem.seat';

export interface SeatSession {
  roomCode: string;
  playerId: string;
  token: string;
  displayName: string | null;
}

function isSeat(value: unknown): value is SeatSession {
  if (!value || typeof value !== 'object') return false;
  const v = value as Record<string, unknown>;
  return (
    typeof v.roomCode === 'string' &&
    typeof v.playerId === 'string' &&
    typeof v.token === 'string' &&
    v.roomCode.length > 0 &&
    v.playerId.length > 0 &&
    v.token.length > 0
  );
}

/**
 * The stored seat, or null if there isn't a usable one.
 *
 * Storage access is wrapped: Safari's private mode and some embedded browsers throw on
 * localStorage rather than returning null, and a phone that cannot persist should still be able to
 * play a game — it just cannot reconnect into its seat.
 */
export function loadSeat(): SeatSession | null {
  try {
    const raw = localStorage.getItem(SEAT_KEY);
    if (!raw) return null;
    const parsed: unknown = JSON.parse(raw);
    return isSeat(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

export function saveSeat(seat: SeatSession): void {
  try {
    localStorage.setItem(SEAT_KEY, JSON.stringify(seat));
  } catch {
    // Non-fatal: see loadSeat.
  }
}

export function clearSeat(): void {
  try {
    localStorage.removeItem(SEAT_KEY);
  } catch {
    // Non-fatal: see loadSeat.
  }
}

export { SEAT_KEY };
