/**
 * App — mounts the single socket listener hook and renders the screen chosen
 * by the derived selector. The phone client is a state machine driven entirely
 * by server events; this component just picks which screen is current.
 */

import { useGameSocket } from './hooks/useGameSocket';
import { useCurrentScreen } from './store/selectors';
import { JoinScreen } from './screens/JoinScreen';
import { IdleScreen } from './screens/IdleScreen';
import { ActionScreen } from './screens/ActionScreen';
import { SecretPhaseScreen } from './screens/SecretPhaseScreen';
import { SpectatorScreen } from './screens/SpectatorScreen';
import { GameOverScreen } from './screens/GameOverScreen';

export default function App() {
  useGameSocket();
  const screen = useCurrentScreen();

  switch (screen) {
    case 'join':
      return <JoinScreen />;
    case 'idle':
      return <IdleScreen />;
    case 'action':
      return <ActionScreen />;
    case 'secret_phase':
      return <SecretPhaseScreen />;
    case 'spectator':
      return <SpectatorScreen />;
    case 'game_over':
      return <GameOverScreen />;
  }
}
