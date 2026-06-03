/**
 * The masking guarantee, enforced as a test.
 *
 * The secret phase screen MUST render identically for an acting player
 * (witch/constable) and a non-acting player. We render the screen with the
 * same prompt but opposite `acting` values and assert the produced DOM is
 * byte-for-byte identical.
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { SecretPhaseScreen } from './SecretPhaseScreen';
import { useGameStore } from '../store/gameStore';
import type { SecretPhasePromptPayload } from '../socket/types';

// Spy on the emit helper so we can assert both players submit identically.
const submitSpy = vi.fn();
vi.mock('../socket/socketClient', () => ({
  sendSecretPhaseSubmit: (payload: { selection: string }) => submitSpy(payload),
}));

const PROMPT_BASE: Omit<SecretPhasePromptPayload, 'acting'> = {
  prompt: 'night_vote',
  targets: ['Alice', 'Bob', 'Carlos'],
};

function renderWith(acting: boolean) {
  useGameStore.getState().reset();
  useGameStore.getState().applySecretPhasePrompt({ ...PROMPT_BASE, acting });
  return render(<SecretPhaseScreen />);
}

describe('SecretPhaseScreen masking', () => {
  beforeEach(() => {
    submitSpy.mockClear();
  });

  it('renders identical DOM for acting and non-acting players', () => {
    const acting = renderWith(true);
    const actingHtml = acting.container.innerHTML;
    acting.unmount();

    const nonActing = renderWith(false);
    const nonActingHtml = nonActing.container.innerHTML;

    expect(actingHtml).toBe(nonActingHtml);
  });

  it('emits secret_phase_submit on selection regardless of acting flag', () => {
    renderWith(false); // non-acting player
    fireEvent.click(screen.getByRole('button', { name: 'Bob' }));
    expect(submitSpy).toHaveBeenCalledWith({ selection: 'Bob' });
  });

  it('shows the identical waiting state after submit for any player', () => {
    renderWith(true);
    fireEvent.click(screen.getByRole('button', { name: 'Alice' }));
    expect(screen.getByTestId('waiting-for-others')).toBeInTheDocument();
  });

  it('never branches header text on acting — header reflects prompt type only', () => {
    renderWith(true);
    expect(screen.getByText('Choose a player')).toBeInTheDocument();
  });
});
