/**
 * Idle screen — the default in-game view when it is not this player's turn
 * and no secret phase is active. Shows the player's private info and the
 * public board. All data is this player's own (private_state) or public
 * (game_state_update); never another player's secrets.
 */

import { useGameStore } from '../store/gameStore';
import { TryalCardList } from '../components/TryalCardList';
import { HandList } from '../components/HandList';
import { BoardSummary } from '../components/BoardSummary';
import { RoleIndicator } from '../components/RoleIndicator';
import { FellowWitchBanner } from '../components/FellowWitchBanner';
import { CharacterCard } from '../components/CharacterCard';

export function IdleScreen() {
  const { tryals, hand } = useGameStore((s) => s.privateState);
  const { players, whoseTurn } = useGameStore((s) => s.publicBoard);
  const playerId = useGameStore((s) => s.session.playerId);
  const displayName = useGameStore((s) => s.session.displayName);

  return (
    <div className="flex min-h-dvh flex-col gap-6 bg-ink px-5 py-6">
      <header className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-parchment">{displayName}</h2>
        <RoleIndicator />
      </header>

      <FellowWitchBanner />

      {/* Town Hall identity is public; this is the player's reminder of their own card. */}
      <CharacterCard />

      <section className="flex flex-col gap-2">
        <h3 className="text-sm uppercase tracking-wider text-parchment/60">
          Your Tryal Cards
        </h3>
        <TryalCardList tryals={tryals} />
      </section>

      <section className="flex flex-col gap-2">
        <h3 className="text-sm uppercase tracking-wider text-parchment/60">
          Your Hand
        </h3>
        <HandList hand={hand} />
      </section>

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
