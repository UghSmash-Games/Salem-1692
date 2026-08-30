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
import { DeckRearrangeScreen } from './screens/DeckRearrangeScreen';
import { CardPickScreen } from './screens/CardPickScreen';
import { ConfirmScreen } from './screens/ConfirmScreen';
import { TargetScreen } from './screens/TargetScreen';
import { TryalPickScreen } from './screens/TryalPickScreen';
import { SpectatorScreen } from './screens/SpectatorScreen';
import { GameOverScreen } from './screens/GameOverScreen';
import { PublicRevealToast } from './components/PublicRevealToast';
import { RulesSheet } from './components/RulesSheet';

function CurrentScreen() {
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
    case 'deck_rearrange':
      return <DeckRearrangeScreen />;
    case 'card_pick':
      return <CardPickScreen />;
    case 'confirm':
      return <ConfirmScreen />;
    case 'target':
      return <TargetScreen />;
    case 'tryal_pick':
      return <TryalPickScreen />;
    case 'spectator':
      return <SpectatorScreen />;
    case 'game_over':
      return <GameOverScreen />;
  }
}

export default function App() {
  useGameSocket();

  // The toast is a fixed-position overlay mounted once, so it surfaces on every
  // phone screen (public_reveal is public data already routed to players).
  return (
    <>
      <CurrentScreen />
      <PublicRevealToast />
      {/* Always reachable, never a screen: it overlays whatever prompt is up and closes itself
          when the screen changes so it can't sit over a host-owned countdown. */}
      <RulesSheet />
    </>
  );
}
