/**
 * Game over screen — shows the winning faction and reveals all tryal cards.
 */

import { useGameStore } from '../store/gameStore';

export function GameOverScreen() {
  const gameOver = useGameStore((s) => s.gameOver);
  const players = useGameStore((s) => s.publicBoard.players);

  if (!gameOver) return null;

  const winnerLabel =
    gameOver.winner === 'witches' ? 'The Witches Win' : 'The Townspeople Win';

  const nameFor = (playerId: string) =>
    players.find((p) => p.playerId === playerId)?.displayName ?? playerId;

  return (
    <div className="flex min-h-dvh flex-col gap-6 bg-ink px-5 py-8">
      <h2
        className={`text-center text-3xl font-bold ${
          gameOver.winner === 'witches' ? 'text-ember' : 'text-candle'
        }`}
      >
        {winnerLabel}
      </h2>

      <section className="flex flex-col gap-3">
        <h3 className="text-sm uppercase tracking-wider text-parchment/60">
          Revealed Tryal Cards
        </h3>
        {Object.entries(gameOver.tryals).map(([playerId, tryals]) => (
          <div key={playerId} className="flex flex-col gap-1">
            <span className="font-medium text-parchment">
              {nameFor(playerId)}
            </span>
            <div className="flex flex-wrap gap-2">
              {tryals.map((card, i) => (
                <span
                  key={i}
                  className="rounded border border-parchment/30 bg-ink/40 px-2 py-1 text-xs text-parchment"
                >
                  {card.label}
                </span>
              ))}
            </div>
          </div>
        ))}
      </section>
    </div>
  );
}
