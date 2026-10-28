/**
 * Confess prompt — shown to all players during the night confess window.
 * Tapping a tryal card reveals it for immunity. Anyone may confess, so this
 * prompt is genuinely identical for everyone (no masking concern here).
 */

import { useState } from 'react';
import type { TryalCardView } from '../socket/types';

interface Props {
  tryals: TryalCardView[];
  onConfess: (tryalIndex: number) => void;
}

export function ConfessPrompt({ tryals, onConfess }: Props) {
  const [selected, setSelected] = useState<number | null>(null);

  const faceDown = tryals
    .map((card, index) => ({ card, index }))
    .filter(({ card }) => !card.faceUp);

  return (
    <div className="flex flex-col gap-4" data-testid="confess-prompt">
      <p className="text-sm text-parchment/80">
        Reveal a tryal card to confess for immunity, or wait.
      </p>
      <ul className="flex flex-col gap-2">
        {faceDown.map(({ card, index }) => (
          <li key={index}>
            <button
              type="button"
              onClick={() => setSelected(index)}
              className={[
                'w-full rounded-md border px-4 py-3 text-center transition-colors',
                selected === index
                  ? 'border-ember bg-ember/30 text-parchment'
                  : 'border-parchment/40 bg-ink/40 text-parchment hover:border-ember/60',
              ].join(' ')}
            >
              {card.label}
            </button>
          </li>
        ))}
      </ul>
      <button
        type="button"
        disabled={selected === null}
        onClick={() => selected !== null && onConfess(selected)}
        className="rounded-md bg-ember px-4 py-3 font-semibold text-parchment disabled:opacity-40"
      >
        Confess
      </button>
    </div>
  );
}
