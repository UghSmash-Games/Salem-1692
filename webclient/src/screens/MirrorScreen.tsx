/**
 * MirrorScreen — the passive public display for a second room.
 *
 * Renders PUBLIC state only. It imports no private components (no TryalCardList,
 * HandList, or RoleIndicator). Its data comes solely from the public store
 * slices fed by useMirrorSocket. Overlays sit on top for secret phases and
 * synchronized reveals.
 */

import { useGameStore } from '../store/gameStore';
import { BoardSummary } from '../components/BoardSummary';
import { DeckSummary } from '../components/DeckSummary';
import { NightDawnOverlay } from '../components/NightDawnOverlay';
import { RevealOverlay } from '../components/RevealOverlay';
import { PublicRevealToast } from '../components/PublicRevealToast';
import { EventLog } from '../components/EventLog';
import { GameOverScreen } from './GameOverScreen';

export function MirrorScreen() {
  const { players, whoseTurn, phase, deckCount, discardCount } = useGameStore(
    (s) => s.publicBoard,
  );
  const roomCode = useGameStore((s) => s.session.roomCode);
  const gameOver = useGameStore((s) => s.gameOver);
  const reveal = useGameStore((s) => s.reveal);

  // Reuse the player game-over screen — it shows only public/revealed data.
  //
  // ⚠ WAIT FOR THE REVEAL TO FINISH FIRST. At revealAt the host sends elimination_result and
  // game_over back-to-back in the same synchronous block, so switching on `gameOver` alone
  // unmounted <RevealOverlay/> after ~one network hop — the mirror room saw a fifth of a second
  // of the beat while the host room watched the full linger. That is the biggest reveal in the
  // game (the kill that ends it) and the exact desync phase_resolve exists to prevent.
  //
  // RevealOverlay clears `reveal` once its linger elapses, so gating on it keeps both rooms on
  // the same schedule. When a game ends with no reveal in flight (e.g. the last witch tryal
  // turned by accusation), `reveal` is already null and this shows immediately, as before.
  if (gameOver && !reveal) return <GameOverScreen />;

  return (
    <div className="relative flex min-h-dvh flex-col gap-6 bg-ink px-6 py-8">
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-wide text-parchment">
          Salem 1692
        </h1>
        <span className="text-sm uppercase tracking-[0.3em] text-candle">
          {roomCode}
        </span>
      </header>

      <section className="flex flex-col gap-2">
        <h2 className="text-sm uppercase tracking-wider text-parchment/60">
          The Town
        </h2>
        <BoardSummary
          players={players}
          whoseTurn={whoseTurn}
          myPlayerId={null}
        />
      </section>

      <EventLog />

      <section className="mt-auto flex items-end justify-between">
        <DeckSummary deckCount={deckCount} discardCount={discardCount} />
        {phase && (
          <span className="text-xs uppercase tracking-widest text-parchment/50">
            {phase}
          </span>
        )}
      </section>

      {/* Overlays — phase comes from PUBLIC state; reveal from the timestamp. */}
      <NightDawnOverlay phase={phase} />
      <RevealOverlay />
      {/* Non-blocking public announcement (e.g. Giles Corey); sits below RevealOverlay. */}
      <PublicRevealToast />
    </div>
  );
}
