/**
 * The build version is shown on the screens people reach BEFORE anything goes wrong, because that
 * is the only moment they will be looking at a calm screen — and "which build?" is the first
 * question asked once something does.
 *
 * ⚠ These assert the SHAPE, never a specific number. A test that hard-codes 1.0.0 fails on the next
 * release and teaches everyone to edit tests during a version bump, which is how a version bump
 * starts hiding real breakage.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { JoinScreen } from './JoinScreen';
import { MirrorJoinScreen } from './MirrorJoinScreen';
import { useGameStore } from '../store/gameStore';
import { APP_VERSION, APP_VERSION_LABEL } from '../version';

/** "v1.2.3", or "vdev" where Vite's define did not run. */
const VERSION_SHAPE = /^v(\d+\.\d+\.\d+|dev)$/;

beforeEach(() => {
  useGameStore.getState().reset();
});

describe('build version', () => {
  it('is a semver string or the explicit dev fallback — never empty or undefined', () => {
    expect(APP_VERSION_LABEL).toMatch(VERSION_SHAPE);
    expect(APP_VERSION).not.toBe('');
  });

  it('appears on the player join screen', () => {
    render(<JoinScreen />);
    expect(screen.getByText(VERSION_SHAPE)).toBeInTheDocument();
  });

  it('appears on the display join screen', () => {
    render(<MirrorJoinScreen />);
    expect(screen.getByText(VERSION_SHAPE)).toBeInTheDocument();
  });

  it('is baked in by the build, not left as the fallback', () => {
    // Guards the wiring itself: if vite.config.ts lost its `define`, or version.ts stopped reading
    // it, every screen would quietly read "vdev" and the stamp would be worthless exactly when it
    // matters. Skipped only in an environment where define genuinely cannot run.
    expect(APP_VERSION).not.toBe('dev');
  });
});
