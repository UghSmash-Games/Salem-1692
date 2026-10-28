/**
 * Spectator screen — shown after this player is eliminated. Read-only view of
 * the public board so they can keep watching the game proceed.
 */

import { useGameStore } from '../store/gameStore';
import { BoardSummary } from '../components/BoardSummary';

export function SpectatorScreen() {
  const { players, whoseTurn, phase } = useGameStore((s) => s.publicBoard);
  const playerId = useGameStore((s) => s.session.playerId);

  return (
    <div className="flex min-h-dvh flex-col gap-6 bg-ink px-5 py-6">
      <header className="flex flex-col items-center gap-1">
        <h2 className="text-2xl font-semibold text-ember">Eliminated</h2>
        <p className="text-sm text-parchment/60">
          You may watch the rest of the game.
        </p>
      </header>

      {phase && (
        <p className="text-center text-xs uppercase tracking-widest text-parchment/50">
          {phase}
        </p>
      )}

      <section className="flex flex-col gap-2">
        <h3 className="text-sm uppercase tracking-wider text-parchment/60">
          The Town
        </h3>
        <BoardSummary
          players={players}
          whoseTurn={whoseTurn}
          myPlayerId={playerId}
        />
      </section>
    </div>
  );
}
