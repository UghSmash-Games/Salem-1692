/**
 * Reconnection — the phone's half.
 *
 * A phone loses its socket constantly in normal play: the screen locks, the browser backgrounds the
 * tab, wifi blips, someone reloads. socket.io restores the transport on its own, but it comes back
 * with a NEW socket.id, so the seat has to be re-presented or the player is a stranger to a host
 * that still holds their tryals under the old id.
 *
 * The socket is mocked as an event bus we can drive: `emit` records outbound messages and `fire`
 * plays server messages back, which lets a whole drop-and-return cycle run without a server.
 *
 * ⚠️ STORAGE IS STUBBED. In this environment `localStorage` is a bare object with no Storage methods
 * (see useTextScale.test.ts) — which is also why seatSession wraps every storage call in try/catch.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook } from '@testing-library/react';

const sent: { event: string; payload: unknown }[] = [];
const handlers = new Map<string, ((data?: unknown) => void)[]>();

/** Play a server → client message into the hook's listeners. */
function fire(event: string, data?: unknown) {
  for (const h of handlers.get(event) ?? []) h(data);
}

vi.mock('../socket/socketClient', async () => {
  const actual = await vi.importActual<typeof import('../socket/events')>('../socket/events');
  return {
    socket: {
      on: (event: string, handler: (data?: unknown) => void) => {
        const list = handlers.get(event) ?? [];
        list.push(handler);
        handlers.set(event, list);
      },
      off: (event: string, handler: (data?: unknown) => void) => {
        handlers.set(event, (handlers.get(event) ?? []).filter((h) => h !== handler));
      },
      emit: (event: string, payload: unknown) => sent.push({ event, payload }),
    },
    connect: () => {},
    rejoinRoom: (payload: unknown) => sent.push({ event: actual.CLIENT_TO_SERVER.REJOIN_ROOM, payload }),
  };
});

import { useGameSocket } from './useGameSocket';
import { useGameStore } from '../store/gameStore';
import { loadSeat, clearSeat, SEAT_KEY } from '../socket/seatSession';

const SEAT = { playerId: 'p2', roomCode: 'JEMW', token: 'a'.repeat(32) };

/** Minimal in-memory Storage — the test environment supplies none. */
function memoryStorage(): Storage {
  const map = new Map<string, string>();
  return {
    getItem: (k: string) => map.get(k) ?? null,
    setItem: (k: string, v: string) => void map.set(k, v),
    removeItem: (k: string) => void map.delete(k),
    clear: () => map.clear(),
    key: () => null,
    length: 0,
  } as Storage;
}

beforeEach(() => {
  vi.stubGlobal('localStorage', memoryStorage());
  sent.length = 0;
  handlers.clear();
  clearSeat();
  useGameStore.getState().reset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('seat persistence', () => {
  it('stores the seat when the server confirms a join', () => {
    renderHook(() => useGameSocket());
    fire('joined', SEAT);

    expect(loadSeat()).toMatchObject({
      playerId: 'p2',
      roomCode: 'JEMW',
      token: SEAT.token,
    });
  });

  it('survives a page reload — the seat outlives the tab', () => {
    // localStorage, not sessionStorage: a phone browser killing a backgrounded tab is one of the
    // exact failures reconnection exists to survive, and sessionStorage dies with the tab.
    renderHook(() => useGameSocket());
    fire('joined', SEAT);

    expect(localStorage.getItem(SEAT_KEY)).toBeTruthy();
  });

  it('ignores a join with no token rather than storing a seat it cannot reclaim', () => {
    renderHook(() => useGameSocket());
    fire('joined', { playerId: 'p2', roomCode: 'JEMW' });

    expect(loadSeat()).toBeNull();
  });
});

describe('reclaiming the seat on connect', () => {
  it('replays the stored seat on EVERY connect, not just the first', () => {
    // The transport reconnect is silent and can happen repeatedly; each one lands on a new
    // socket.id, so each one needs its own reclaim.
    renderHook(() => useGameSocket());
    fire('joined', SEAT);

    fire('connect');
    fire('disconnect');
    fire('connect');

    const rejoins = sent.filter((m) => m.event === 'rejoin_room');
    expect(rejoins).toHaveLength(2);
    expect(rejoins[0].payload).toEqual({
      code: 'JEMW',
      playerId: 'p2',
      token: SEAT.token,
    });
  });

  it('does not try to rejoin when no seat is held', () => {
    renderHook(() => useGameSocket());
    fire('connect');

    expect(sent.filter((m) => m.event === 'rejoin_room')).toHaveLength(0);
  });

  it('sends the token — a playerId alone would prove nothing', () => {
    // playerId is public; it appears in every game_state_update. The token is the authorization.
    renderHook(() => useGameSocket());
    fire('joined', SEAT);
    fire('connect');

    const rejoin = sent.find((m) => m.event === 'rejoin_room');
    expect((rejoin?.payload as { token?: string }).token).toBe(SEAT.token);
  });
});

describe('when a seat can no longer be held', () => {
  it('forgets a stale seat so it stops replaying a dead credential', () => {
    renderHook(() => useGameSocket());
    fire('joined', SEAT);

    fire('error_msg', { message: 'Could not rejoin', code: 'rejoin_failed' });

    expect(loadSeat()).toBeNull();
    expect(useGameStore.getState().session.joinError).toBe('Could not rejoin');

    // And a later connect no longer tries.
    sent.length = 0;
    fire('connect');
    expect(sent.filter((m) => m.event === 'rejoin_room')).toHaveLength(0);
  });

  it('stands down when another device takes the seat, instead of fighting for it', () => {
    // Both phones auto-reclaim on connect, so an evicted device that kept its seat would snatch it
    // back on its next blip and the two would trade the seat back and forth.
    renderHook(() => useGameSocket());
    fire('joined', SEAT);

    fire('error_msg', { message: 'Seat taken over on another device', code: 'seat_taken' });

    expect(loadSeat()).toBeNull();
    expect(useGameStore.getState().session.playerId).toBeNull();   // store reset
    expect(useGameStore.getState().session.joinError).toBe('Seat taken over on another device');

    sent.length = 0;
    fire('connect');
    expect(sent.filter((m) => m.event === 'rejoin_room')).toHaveLength(0);
  });

  it('keeps the seat on an ordinary error that says nothing about the seat', () => {
    // A plain join failure ("Room not found") carries no code and must not evict a live seat.
    renderHook(() => useGameSocket());
    fire('joined', SEAT);

    fire('error_msg', { message: 'Room not found' });

    expect(loadSeat()).not.toBeNull();
  });

  it('forgets the seat when the host closes the room', () => {
    renderHook(() => useGameSocket());
    fire('joined', SEAT);

    fire('room_closed', {});

    expect(loadSeat()).toBeNull();
  });
});

describe('what comes back after a reconnect', () => {
  it('rebuilds private state from what the host re-sends', () => {
    // The phone reconnects with an empty store — it knows neither its tryals nor its hand until the
    // host re-sends them, which is why player_rejoined obliges the host to.
    renderHook(() => useGameSocket());
    fire('joined', SEAT);

    expect(useGameStore.getState().privateState.tryals).toHaveLength(0);

    fire('private_state', {
      playerId: 'p2',
      tryals: [{ label: 'Witch', faceUp: false }],
      hand: ['Accusation'],
    });

    expect(useGameStore.getState().privateState.tryals).toHaveLength(1);
    expect(useGameStore.getState().privateState.hand).toEqual(['Accusation']);
  });
});
