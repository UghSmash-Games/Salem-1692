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

    expect(registered).not.toContain('private_state');
    expect(registered).not.toContain('secret_phase_prompt');
    expect(registered).not.toContain('action_request');
  });
});
