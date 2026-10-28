/**
 * MirrorApp — the /display entry point. Mounts the mirror-only socket hook
 * (which attaches listeners for public events ONLY) and shows the join screen
 * until a room is joined, then the passive mirror display.
 *
 * This is a fully separate tree from the player App; a browser tab is one role
 * for its whole lifetime.
 */

import { useMirrorSocket } from './hooks/useMirrorSocket';
import { useGameStore } from './store/gameStore';
import { MirrorJoinScreen } from './screens/MirrorJoinScreen';
import { MirrorScreen } from './screens/MirrorScreen';

export default function MirrorApp() {
  useMirrorSocket();
  const roomCode = useGameStore((s) => s.session.roomCode);

  return roomCode ? <MirrorScreen /> : <MirrorJoinScreen />;
}
