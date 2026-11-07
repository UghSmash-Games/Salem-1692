/**
 * The display URL is one word away from the join URL, so people arrive here by mistake. A phone that
 * joins as a display gets a spectator board with no controls — which reads as a broken game rather
 * than a wrong turn, because nothing on screen says this was the wrong address.
 *
 * These lock the two things that prevent that: the screen says what it is BEFORE the code field, and
 * it offers a way back to the join page.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MirrorJoinScreen } from './MirrorJoinScreen';
import { useGameStore } from '../store/gameStore';

beforeEach(() => {
  useGameStore.getState().reset();
});

describe('MirrorJoinScreen guidance', () => {
  it('says this screen is a shared display, not a controller', () => {
    render(<MirrorJoinScreen />);
    expect(screen.getByText(/shared screen for a TV/i)).toBeInTheDocument();
  });

  it('promises it never shows your own cards — the thing a player would be looking for', () => {
    render(<MirrorJoinScreen />);
    expect(screen.getByText(/never your own cards/i)).toBeInTheDocument();
  });

  it('offers a route back to the join page for someone who meant to play', () => {
    render(<MirrorJoinScreen />);
    const link = screen.getByRole('link', { name: /join the game/i });
    expect(link).toHaveAttribute('href', '/join');
  });

  it('still asks for a room code — the guidance must not replace the actual form', () => {
    render(<MirrorJoinScreen />);
    expect(screen.getByPlaceholderText('ABCD')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /watch game/i })).toBeInTheDocument();
  });
});
