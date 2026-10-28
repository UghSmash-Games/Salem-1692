/**
 * RulesSheet — the always-available rules quick-reference on the phone.
 *
 * 🔴 DELIBERATELY NOT A SCREEN. It is not in the `Screen` union and the selector knows nothing about
 * it. The phone's screens are MUTUALLY EXCLUSIVE and several are host-blocking with countdowns
 * (secret phase, target, tryal pick, card pick, confirm) — making the rules a screen would mean
 * either outranking a live prompt or being unreachable while one is up. It is an overlay mounted
 * once in App, exactly like PublicRevealToast, so it is reachable from every screen without
 * consuming the one underneath.
 *
 * ⚠️ IT CLOSES ITSELF WHEN THE SCREEN CHANGES. Prompts arrive unannounced and several are on a
 * host-owned deadline; a sheet left open over a countdown would quietly cost the player their
 * submission. Getting out of the way is the safe default — the player can always reopen it.
 *
 * Layered at z-40, BELOW RevealOverlay (z-50), so a synchronized reveal always wins the screen.
 *
 * Content is static reference copy — it lives here as a const, not in the store, because it is not
 * game state and must never be fed from the wire.
 */

import { useEffect, useState } from 'react';
import { useCurrentScreen } from '../store/selectors';
import { CharacterCard } from './CharacterCard';
import {
  applyTextScale,
  getTextScale,
  TEXT_SCALES,
  TEXT_SCALE_LABELS,
  type TextScale,
} from '../hooks/useTextScale';

interface Section {
  title: string;
  lines: string[];
}

/**
 * Kept deliberately short — this is a reminder for someone mid-turn, not the rulebook.
 *
 * Verified against docs/character-spec.md and CLAUDE.md. Two details that are easy to get wrong and
 * are stated correctly here: Alibi is a POINT budget (so it can strip one Evidence but never a
 * Witness), and Stocks is a GREEN one-use card (the dev guide lists it as blue; the card asset is
 * Type 0 = Green). There is also no majority/parity win — the witch condition is stated as written.
 */
const SECTIONS: Section[] = [
  {
    title: 'Your turn',
    lines: [
      'Either draw 2 cards, which ends your turn, or play cards from your hand.',
      'Some characters may do something extra before drawing.',
    ],
  },
  {
    title: 'Accusations (red)',
    lines: [
      'Accusation = 1 · Evidence = 3 · Witness = 7.',
      'At 7 points against a player, whoever placed the last card chooses which of that player’s face-down Tryal cards is turned over.',
      'The red cards are then discarded — accusations do not carry over.',
      'Piety doubles the total needed. George Burroughs needs 8. Thomas Danforth needs one fewer.',
    ],
  },
  {
    title: 'Cards kept in front of you (blue)',
    lines: [
      'Asylum — you cannot be eliminated during the night.',
      'Piety — doubles the accusations needed against you.',
      'Matchmaker — two players are linked; if one is eliminated, so is the other.',
      'Black Cat — when Conspiracy is drawn, this holder turns a Tryal card.',
    ],
  },
  {
    title: 'One-use cards (green)',
    lines: [
      'Alibi — removes up to 3 accusation POINTS from another player, so it can strip one Evidence, but never a Witness.',
      'Curse — discards one blue card from another player.',
      'Scapegoat — moves the cards in front of one player to another.',
      'Robbery — one player’s hand passes to another.',
      'Arson — burns a player’s hand.',
      'Stocks — skips a player’s next turn.',
    ],
  },
  {
    title: 'Night',
    lines: [
      'Every phone shows the same prompt and everyone taps — no one is singled out.',
      'The witches choose someone to eliminate.',
      'The constable may place the gavel to protect one player.',
      'Anyone may confess: turn one of your own Tryal cards face-up and you are safe that night.',
    ],
  },
  {
    title: 'Conspiracy',
    lines: [
      'The player who drew it turns one Tryal card of the Black Cat holder.',
      'Then everyone at once takes a face-down Tryal from the player on their left.',
      'Shuffle your own Tryals and lay them back face-down.',
    ],
  },
  {
    title: 'Winning',
    lines: [
      'Townspeople win the moment every “Witch” Tryal card has been revealed.',
      'Witches win when every living player is a witch.',
      'If you ever receive a Witch card you are a witch from then on — losing it later does not change that.',
    ],
  },
];

export function RulesSheet() {
  const screen = useCurrentScreen();
  const [open, setOpen] = useState(false);
  const [textScale, setTextScale] = useState<TextScale>(getTextScale);

  const chooseTextScale = (scale: TextScale) => {
    setTextScale(scale);
    applyTextScale(scale);
  };

  // Close on any screen change — a prompt may have arrived on a deadline behind this sheet.
  useEffect(() => {
    setOpen(false);
  }, [screen]);

  // Nothing to reference before joining a game.
  if (screen === 'join') return null;

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label="Open rules reference"
        aria-expanded={false}
        data-testid="rules-open"
        className="fixed right-3 top-3 z-40 rounded-full border border-parchment/40 bg-ink/80 px-3 py-1 text-sm text-parchment/80"
      >
        Rules
      </button>
    );
  }

  return (
    <div className="fixed inset-0 z-40 flex flex-col bg-ink/95" data-testid="rules-sheet">
      <header className="flex items-center justify-between border-b border-parchment/20 px-5 py-3">
        <h2 className="text-lg font-semibold text-parchment">Rules</h2>
        <button
          type="button"
          onClick={() => setOpen(false)}
          aria-label="Close rules reference"
          aria-expanded
          data-testid="rules-close"
          className="rounded-md border border-parchment/40 px-3 py-1 text-sm text-parchment/80"
        >
          Close
        </button>
      </header>

      <div className="flex-1 overflow-y-auto px-5 py-4">
        {/* The player's own character, reachable mid-game without leaving the current prompt —
            a player who forgets what their card does should not have to wait for their turn. */}
        <div className="mb-6">
          <CharacterCard />
        </div>

        {/* Settings live here rather than behind a second floating control — the rules affordance is
            already reachable from every screen, and a phone has no room for two. */}
        <section className="mb-6" data-testid="rules-settings">
          <h3 className="mb-2 text-sm uppercase tracking-wider text-candle">Text size</h3>
          <div className="flex gap-2">
            {TEXT_SCALES.map((s) => (
              <button
                key={s}
                type="button"
                onClick={() => chooseTextScale(s)}
                aria-pressed={textScale === s}
                data-testid={`text-scale-${s}`}
                className={`flex-1 rounded-md border px-3 py-2 text-sm ${
                  textScale === s
                    ? 'border-candle text-candle'
                    : 'border-parchment/40 text-parchment/80'
                }`}
              >
                {TEXT_SCALE_LABELS[s]}
              </button>
            ))}
          </div>
        </section>

        {SECTIONS.map((s) => (
          <section key={s.title} className="mb-5" data-testid="rules-section">
            <h3 className="mb-1 text-sm uppercase tracking-wider text-candle">{s.title}</h3>
            <ul className="flex flex-col gap-1">
              {s.lines.map((line, i) => (
                <li key={i} className="text-sm leading-snug text-parchment/80">
                  {line}
                </li>
              ))}
            </ul>
          </section>
        ))}
      </div>
    </div>
  );
}
