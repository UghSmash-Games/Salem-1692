/**
 * Defense-in-depth: the mirror must never attach a listener for a private
 * event. We mock the socket, mount the hook, and assert every registered
 * event is in the allowed set — and that the three private events are absent.
 */

import { describe, it, expect, vi } from 'vitest';
import { renderHook } from '@testing-library/react';

const registered: string[] = [];

vi.mock('../socket/socketClient', () => ({
  socket: {
    on: (event: string) => registered.push(event),
    off: () => {},
  },
  connect: () => {},
}));

import { useMirrorSocket, MIRROR_ALLOWED_EVENTS } from './useMirrorSocket';

describe('useMirrorSocket event registration', () => {
  it('registers only events in the allowed public set', () => {
    registered.length = 0;
    renderHook(() => useMirrorSocket());

    for (const event of registered) {
      expect(MIRROR_ALLOWED_EVENTS).toContain(event);
    }
  });

  it('never registers a private event listener', () => {
    registered.length = 0;
    renderHook(() => useMirrorSocket());

    // Every host → ONE-player event. Each is listed explicitly rather than relying on the
    // allow-list subset check above, so that adding a new per-player prompt to the allow-list by
    // mistake still fails here. Kept in step with protocol.md's "routed to exactly one player
    // socket" list.
    for (const priv of [
      'private_state',
      'secret_phase_prompt',
      'action_request',
      'deck_rearrange_request',
      'card_pick_request',
      'confirm_request',
      'target_request',
      'tryal_pick_request',
    ]) {
      expect(registered).not.toContain(priv);
    }
  });
});
