/**
 * Action screen — shown when the host sends an action_request for this player.
 *
 * Flow: choose to draw or play. If playing, pick a card from hand, then pick a
 * target (self excluded), then emit player_action. The confess path emits confess.
 */

import { useMemo, useState } from 'react';
import { useGameStore } from '../store/gameStore';
import { sendPlayerAction, sendConfess } from '../socket/socketClient';
import { HandList } from '../components/HandList';
import { PlayerTargetList } from '../components/PlayerTargetList';
import { ConfessPrompt } from '../components/ConfessPrompt';
import { FellowWitchBanner } from '../components/FellowWitchBanner';
import { RoleIndicator } from '../components/RoleIndicator';

type Step = 'choose' | 'select_card' | 'select_target' | 'confess';

export function ActionScreen() {
  const actions = useGameStore((s) => s.actionRequest?.actions ?? []);
  const { hand, tryals } = useGameStore((s) => s.privateState);
  const { players } = useGameStore((s) => s.publicBoard);
  const myPlayerId = useGameStore((s) => s.session.playerId);

  const [step, setStep] = useState<Step>('choose');
  const [cardIndex, setCardIndex] = useState<number | null>(null);
  const [target, setTarget] = useState<string | null>(null);

  // Valid targets exclude self and eliminated players.
  const targets = useMemo(
    () =>
      players
        .filter((p) => p.playerId !== myPlayerId && !p.eliminated)
        .map((p) => p.displayName),
    [players, myPlayerId],
  );

  const canDraw = actions.includes('draw');
  const canPlay = actions.includes('play');
  const canConfess = actions.includes('confess');
  const canEnd = actions.includes('end');
  // Tituba: once/game, before drawing, rearrange the deck. Sends player_action
  // {card:'tituba'} → host runs RunTitubaRearrange → deck_rearrange_request → the
  // DeckRearrangeScreen. She still draws/plays this same turn afterward.
  const canTituba = actions.includes('tituba');
  // Samuel Parris: twice/game, draw up to 2 from the discard pile INSTEAD of the deck. Sends
  // player_action {card:'parris'} → host runs RunParrisDiscardPick → card_pick_request → the
  // CardPickScreen (with a Done button). TURN-ENDING, like Draw 2 (not a loop-back like Tituba).
  const canParris = actions.includes('parris');

  const submitPlay = () => {
    if (cardIndex === null || target === null) return;
    // `target` is a display name (what PlayerTargetList shows); the host expects
    // the public playerId. Map it back before sending.
    const targetPlayer = players.find(
      (p) => p.displayName === target && p.playerId !== myPlayerId && !p.eliminated,
    );
    sendPlayerAction({
      card: hand[cardIndex],
      targetPlayerId: targetPlayer?.playerId ?? '',
    });
    resetLocal();
  };

  const submitEnd = () => {
    // Signal the host the player is done playing cards this turn.
    sendPlayerAction({ card: 'end', targetPlayerId: '' });
    resetLocal();
  };

  const submitDraw = () => {
    // "Draw 2 cards" is communicated as a player_action with no target.
    sendPlayerAction({ card: 'draw', targetPlayerId: '' });
    resetLocal();
  };

  const resetLocal = () => {
    setStep('choose');
    setCardIndex(null);
    setTarget(null);
  };

  return (
    <div className="flex min-h-dvh flex-col gap-5 bg-ink px-5 py-6">
      <FellowWitchBanner />
      <header className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-parchment">Your Turn</h2>
        <RoleIndicator />
      </header>

      {step === 'choose' && (
        <div className="flex flex-col gap-3">
          {canTituba && (
            <button
              type="button"
              onClick={() => sendPlayerAction({ card: 'tituba', targetPlayerId: '' })}
              className="rounded-md border border-candle bg-candle/20 px-4 py-3 text-lg font-semibold text-candle"
            >
              Rearrange the Deck
            </button>
          )}
          {canParris && (
            <button
              type="button"
              onClick={() => sendPlayerAction({ card: 'parris', targetPlayerId: '' })}
              className="rounded-md border border-candle bg-candle/20 px-4 py-3 text-lg font-semibold text-candle"
            >
              Draw from Discard
            </button>
          )}
          {canDraw && (
            <button
              type="button"
              onClick={submitDraw}
              className="rounded-md bg-moss px-4 py-3 text-lg font-semibold text-parchment"
            >
              Draw 2 Cards
            </button>
          )}
          {canPlay && (
            <button
              type="button"
              onClick={() => setStep('select_card')}
              className="rounded-md bg-candle px-4 py-3 text-lg font-semibold text-ink"
            >
              Play a Card
            </button>
          )}
          {canConfess && (
            <button
              type="button"
              onClick={() => setStep('confess')}
              className="rounded-md border border-ember px-4 py-3 text-lg font-semibold text-ember"
            >
              Confess
            </button>
          )}
          {canEnd && (
            <button
              type="button"
              onClick={submitEnd}
              className="rounded-md border border-parchment/40 px-4 py-3 text-lg font-semibold text-parchment"
            >
              End Turn
            </button>
          )}
        </div>
      )}

      {step === 'select_card' && (
        <div className="flex flex-col gap-4">
          <h3 className="text-sm uppercase tracking-wider text-parchment/60">
            Choose a card
          </h3>
          <HandList
            hand={hand}
            selectable
            selectedIndex={cardIndex}
            onSelect={setCardIndex}
          />
          <div className="flex gap-3">
            <button
              type="button"
              onClick={resetLocal}
              className="flex-1 rounded-md border border-parchment/40 px-4 py-3 text-parchment"
            >
              Back
            </button>
            <button
              type="button"
              disabled={cardIndex === null}
              onClick={() => setStep('select_target')}
              className="flex-1 rounded-md bg-candle px-4 py-3 font-semibold text-ink disabled:opacity-40"
            >
              Next
            </button>
          </div>
        </div>
      )}

      {step === 'select_target' && (
        <div className="flex flex-col gap-4">
          <h3 className="text-sm uppercase tracking-wider text-parchment/60">
            Choose a target
          </h3>
          <PlayerTargetList
            targets={targets}
            selected={target}
            onSelect={setTarget}
          />
          <div className="flex gap-3">
            <button
              type="button"
              onClick={() => setStep('select_card')}
              className="flex-1 rounded-md border border-parchment/40 px-4 py-3 text-parchment"
            >
              Back
            </button>
            <button
              type="button"
              disabled={target === null}
              onClick={submitPlay}
              className="flex-1 rounded-md bg-ember px-4 py-3 font-semibold text-parchment disabled:opacity-40"
            >
              Play
            </button>
          </div>
        </div>
      )}

      {step === 'confess' && (
        <ConfessPrompt
          tryals={tryals}
          onConfess={(tryalIndex) => {
            sendConfess({ tryalIndex });
            resetLocal();
          }}
        />
      )}
    </div>
  );
}
