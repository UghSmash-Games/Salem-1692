/**
 * Colour-independence — the Phase 9 accessibility requirement: "icons + color, never color alone".
 *
 * These assert the NON-COLOUR carrier for every state whose meaning would otherwise rest on hue.
 * They deliberately test text/attributes rather than class names, because a class assertion would
 * pass even if the only difference were a colour swap.
 *
 * The states covered are the ones confirmed under a host-owned countdown (target choice, tryal pick)
 * or that change what a player can legally do (unplayable cards) — where "which did I pick?" being
 * ambiguous costs a real submission.
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PlayerTargetList } from './PlayerTargetList';
import { HandList } from './HandList';

describe('colour-independent state', () => {
  it('a selected target is marked, not merely tinted', () => {
    render(
      <PlayerTargetList targets={['Alice', 'Bob']} selected="Bob" onSelect={() => {}} />,
    );

    const bob = screen.getByRole('button', { name: /Bob/ });
    const alice = screen.getByRole('button', { name: /Alice/ });

    expect(bob).toHaveAttribute('aria-pressed', 'true');
    expect(alice).toHaveAttribute('aria-pressed', 'false');
    expect(bob).toHaveTextContent('✓');
    expect(alice).not.toHaveTextContent('✓');
  });

  it('a selected hand card is marked', () => {
    render(
      <HandList hand={['Alibi', 'Curse']} selectable selectedIndex={1} onSelect={() => {}} />,
    );

    const curse = screen.getByRole('button', { name: /Curse/ });
    expect(curse).toHaveAttribute('aria-pressed', 'true');
    expect(curse).toHaveTextContent('✓');
  });

  it('an unplayable card SAYS so rather than just being greyed', () => {
    // Greying is a hue+lightness cue that reads as "just styled" to plenty of players; the host
    // computes this list for a rules reason, so the reason should be legible.
    render(
      <HandList
        hand={['Robbery', 'Alibi']}
        selectable
        selectedIndex={null}
        onSelect={() => {}}
        disabledCards={['Robbery']}
      />,
    );

    const robbery = screen.getByRole('button', { name: /Robbery/ });
    expect(robbery).toBeDisabled();
    expect(robbery).toHaveTextContent(/can.t play/i);

    const alibi = screen.getByRole('button', { name: /Alibi/ });
    expect(alibi).not.toBeDisabled();
    expect(alibi).not.toHaveTextContent(/can.t play/i);
  });

  it('does not mark anything when nothing is selected', () => {
    render(
      <PlayerTargetList targets={['Alice', 'Bob']} selected={null} onSelect={() => {}} />,
    );
    for (const b of screen.getAllByRole('button')) {
      expect(b).toHaveAttribute('aria-pressed', 'false');
      expect(b).not.toHaveTextContent('✓');
    }
  });
});
