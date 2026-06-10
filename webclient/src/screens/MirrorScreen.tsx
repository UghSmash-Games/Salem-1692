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
import { GameOverScreen } from './GameOverScreen';

export function MirrorScreen() {
  const { players, whoseTurn, phase, deckCount, discardCount } = useGameStore(
    (s) => s.publicBoard,
  );
  const roomCode = useGameStore((s) => s.session.roomCode);
  const gameOver = useGameStore((s) => s.gameOver);

  // Reuse the player game-over screen — it shows only public/revealed data.
  if (gameOver) return <GameOverScreen />;

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
    </div>
  );
}
