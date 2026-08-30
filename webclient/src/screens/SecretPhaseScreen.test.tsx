/**
 * The masking guarantee, enforced as a test (CLAUDE.md "Masking definition").
 *
 * Two axes:
 *  1. The screen must NOT branch on prompt.acting — identical DOM for acting vs
 *     non-acting given the same private state.
 *  2. The CONTROL STRUCTURE (target buttons + Confirm + the ally-tally region)
 *     must be identical for a witch vs a non-witch; only the PRIVATE ally content
 *     (fellow-witch banner + fellow tentative lines) may differ. This test fails
 *     if a witch's screen ever becomes structurally distinguishable.
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { SecretPhaseScreen } from './SecretPhaseScreen';
import { useGameStore } from '../store/gameStore';
import type { SecretPhasePromptPayload, PrivateStatePayload } from '../socket/types';

const submitSpy = vi.fn();
vi.mock('../socket/socketClient', () => ({
  sendSecretPhaseSubmit: (payload: { selection: string; confirmed: boolean }) =>
    submitSpy(payload),
}));

const TARGETS = ['Alice', 'Bob', 'Carlos'];
const PROMPT_BASE: Omit<SecretPhasePromptPayload, 'acting'> = {
  prompt: 'night_vote',
  targets: TARGETS,
};

function renderWith(acting: boolean) {
  useGameStore.getState().reset();
  useGameStore.getState().applySecretPhasePrompt({ ...PROMPT_BASE, acting });
  return render(<SecretPhaseScreen />);
}

function renderAs(acting: boolean, priv: Partial<PrivateStatePayload>) {
  useGameStore.getState().reset();
  useGameStore.getState().applySecretPhasePrompt({ ...PROMPT_BASE, acting });
  useGameStore.getState().applyPrivateState({
    playerId: 'p0',
    tryals: [],
    hand: [],
    role: 'townsperson',
    ...priv,
  });
  return render(<SecretPhaseScreen />);
}

const buttonLabels = (container: HTMLElement) =>
  within(container)
    .getAllByRole('button')
    .map((b) => b.textContent);

describe('SecretPhaseScreen masking', () => {
  beforeEach(() => {
    submitSpy.mockClear();
    useGameStore.getState().reset();
  });

  it('renders identical DOM for acting and non-acting players (same private state)', () => {
    const acting = renderWith(true);
    const actingHtml = acting.container.innerHTML;
    acting.unmount();

    const nonActing = renderWith(false);
    expect(nonActing.container.innerHTML).toBe(actingHtml);
  });

  it('control structure is identical for a witch vs a non-witch — only private ally data differs', () => {
    const witch = renderAs(true, {
      role: 'witch',
      fellowWitches: ['Carole'],
      witchVotes: [{ witch: 'Carole', target: 'Bob' }],
    });
    const witchButtons = buttonLabels(witch.container);
    expect(witch.getByTestId('fellow-witches')).toBeInTheDocument();
    expect(witch.getByTestId('ally-tally')).toHaveTextContent('Carole → Bob');
    witch.unmount();

    const town = renderAs(false, { role: 'townsperson', fellowWitches: [], witchVotes: [] });
    const townButtons = buttonLabels(town.container);
    expect(town.queryByTestId('fellow-witches')).not.toBeInTheDocument();
    expect(town.getByTestId('ally-tally')).not.toHaveTextContent('Carole');

    // The control structure (target buttons + Confirm) must match exactly.
    // Both have an ally-tally region; only its private content differs.
    expect(witchButtons).toEqual(townButtons);
    expect(witchButtons).toContain('Confirm');
  });

  it('a tap sends a TENTATIVE submit; Confirm sends the final', () => {
    renderWith(false);
    fireEvent.click(screen.getByRole('button', { name: 'Bob' }));
    expect(submitSpy).toHaveBeenCalledWith({ selection: 'Bob', confirmed: false });

    submitSpy.mockClear();
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
    expect(submitSpy).toHaveBeenCalledWith({ selection: 'Bob', confirmed: true });
  });

  it('stays on the prompt after a tentative tap; waiting appears only after Confirm', () => {
    renderWith(true);
    fireEvent.click(screen.getByRole('button', { name: 'Alice' }));
    expect(screen.queryByTestId('waiting-for-others')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
    expect(screen.getByTestId('waiting-for-others')).toBeInTheDocument();
  });

  it('header reflects prompt type only, never the acting flag', () => {
    renderWith(true);
    expect(screen.getByText('Choose a player')).toBeInTheDocument();
  });

  it('shows the player\'s own role indicator (private overlay) on the secret-phase screen', () => {
    const witch = renderAs(true, { role: 'witch', isWitch: true });
    expect(witch.getByTestId('role-indicator')).toHaveTextContent('Witch');
    witch.unmount();

    const town = renderAs(false, { role: 'townsperson' });
    expect(town.getByTestId('role-indicator')).toHaveTextContent('Townsperson');
  });

  it('blocks a constable from confirming a self-protect (own device only)', () => {
    useGameStore.getState().reset();
    useGameStore.getState().beginJoin('Alice');
    useGameStore.getState().applySecretPhasePrompt({
      prompt: 'constable_save',
      targets: ['Alice', 'Bob'],
      acting: true,
    });
    useGameStore.getState().applyPrivateState({
      playerId: 'p0',
      tryals: [],
      hand: [],
      role: 'constable',
      isConstable: true,
    });
    render(<SecretPhaseScreen />);

    fireEvent.click(screen.getByRole('button', { name: 'Alice' })); // self
    expect(screen.getByRole('alert')).toHaveTextContent("protect yourself");
    expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled();

    submitSpy.mockClear();
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
    expect(submitSpy).not.toHaveBeenCalled(); // confirm is blocked

    fireEvent.click(screen.getByRole('button', { name: 'Bob' })); // another player
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Confirm' })).not.toBeDisabled();
  });

  it('does NOT block a non-constable picking themselves (no self-protect rule for them)', () => {
    useGameStore.getState().reset();
    useGameStore.getState().beginJoin('Alice');
    useGameStore.getState().applySecretPhasePrompt({
      prompt: 'constable_save',
      targets: ['Alice', 'Bob'],
      acting: false,
    });
    useGameStore.getState().applyPrivateState({
      playerId: 'p0',
      tryals: [],
      hand: [],
      role: 'townsperson',
      isConstable: false,
    });
    render(<SecretPhaseScreen />);

    fireEvent.click(screen.getByRole('button', { name: 'Alice' }));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Confirm' })).not.toBeDisabled();
  });
});

describe('SecretPhaseScreen confess window', () => {
  // Three tryals; the middle one is already face-up, so it must NOT be offered.
  const TRYALS = [
    { label: 'Not a Witch', faceUp: false }, // index 0
    { label: 'Constable', faceUp: true }, // index 1 (revealed — not offered)
    { label: 'Witch', faceUp: false }, // index 2
  ];

  function renderConfess(acting: boolean, canFakeConfess = false) {
    useGameStore.getState().reset();
    useGameStore.getState().beginJoin('Alice');
    useGameStore.getState().applySecretPhasePrompt({ prompt: 'confess', targets: [], acting, canFakeConfess });
    useGameStore.getState().applyPrivateState({
      playerId: 'p0',
      tryals: TRYALS,
      hand: [],
      role: 'townsperson',
    });
    return render(<SecretPhaseScreen />);
  }

  beforeEach(() => {
    submitSpy.mockClear();
    useGameStore.getState().reset();
  });

  it('WITHOUT canFakeConfess: only own face-down tryals + "Don\'t confess" (no Phipps button)', () => {
    renderConfess(true); // canFakeConfess defaults false — the base masked structure for everyone
    const options = within(screen.getByTestId('confess-options'))
      .getAllByRole('button')
      .map((b) => b.textContent);
    // Constable (index 1) is face-up, so excluded; order preserves original index.
    expect(options).toEqual(['Not a Witch', 'Witch', "Don't confess"]);
  });

  it('WITH canFakeConfess (William Phipps): adds the "Confess without revealing" button', () => {
    renderConfess(true, true);
    const options = within(screen.getByTestId('confess-options'))
      .getAllByRole('button')
      .map((b) => b.textContent);
    expect(options).toEqual([
      'Not a Witch',
      'Witch',
      'Confess without revealing',
      "Don't confess",
    ]);
  });

  it('"Confess without revealing" sends the fake sentinel (tentative, then final on Confirm)', () => {
    renderConfess(true, true);
    fireEvent.click(screen.getByRole('button', { name: 'Confess without revealing' }));
    expect(submitSpy).toHaveBeenCalledWith({ selection: 'fake', confirmed: false });

    submitSpy.mockClear();
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
    expect(submitSpy).toHaveBeenCalledWith({ selection: 'fake', confirmed: true });
  });

  it('the BASE confess structure is identical for a witch vs a townsperson (core masking)', () => {
    // Town Hall identity is public, so the Phipps button is host-gated (not tested here). What MUST
    // stay masked is the base confess/skip structure — identical regardless of the secret role.
    useGameStore.getState().reset();
    useGameStore.getState().beginJoin('Alice');
    useGameStore.getState().applySecretPhasePrompt({ prompt: 'confess', targets: [], acting: true });
    useGameStore.getState().applyPrivateState({
      playerId: 'p0', tryals: TRYALS, hand: [], role: 'witch', isWitch: true,
    });
    const witch = render(<SecretPhaseScreen />);
    const witchOptions = within(witch.getByTestId('confess-options'))
      .getAllByRole('button').map((b) => b.textContent);
    witch.unmount();

    useGameStore.getState().reset();
    useGameStore.getState().beginJoin('Alice');
    useGameStore.getState().applySecretPhasePrompt({ prompt: 'confess', targets: [], acting: false });
    useGameStore.getState().applyPrivateState({
      playerId: 'p0', tryals: TRYALS, hand: [], role: 'townsperson',
    });
    const town = render(<SecretPhaseScreen />);
    const townOptions = within(town.getByTestId('confess-options'))
      .getAllByRole('button').map((b) => b.textContent);

    expect(townOptions).toEqual(witchOptions); // base confess structure identical
    expect(townOptions).not.toContain('Confess without revealing'); // neither is Phipps here
  });

  it('tapping a tryal sends a TENTATIVE index; Confirm sends the final index', () => {
    renderConfess(true);
    fireEvent.click(screen.getByRole('button', { name: 'Witch' })); // original index 2
    expect(submitSpy).toHaveBeenCalledWith({ selection: '2', confirmed: false });

    submitSpy.mockClear();
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
    expect(submitSpy).toHaveBeenCalledWith({ selection: '2', confirmed: true });
  });

  it('Don\'t confess sends the skip sentinel (tentative, then final on Confirm)', () => {
    renderConfess(true);
    fireEvent.click(screen.getByRole('button', { name: "Don't confess" }));
    expect(submitSpy).toHaveBeenCalledWith({ selection: 'skip', confirmed: false });

    submitSpy.mockClear();
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
    expect(submitSpy).toHaveBeenCalledWith({ selection: 'skip', confirmed: true });
  });

  it('renders identical DOM for acting and non-acting confessors (same private state)', () => {
    const acting = renderConfess(true);
    const html = acting.container.innerHTML;
    acting.unmount();

    const nonActing = renderConfess(false);
    expect(nonActing.container.innerHTML).toBe(html);
  });
});
