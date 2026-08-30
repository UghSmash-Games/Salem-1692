import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import App from './App';
import MirrorApp from './MirrorApp';
import './styles/index.css';
import { restoreTextScale } from './hooks/useTextScale';

/**
 * The player phone client serves "/" and "/join" — both render the App state
 * machine, which shows the JoinScreen until the player has joined a room and
 * then derives the in-game screen from server events.
 *
 * "/display" renders the passive mirror screen (public state only), a fully
 * separate component tree that never attaches listeners for private events.
 */
// Apply the saved large-text preference before first paint, so it is not lost on reload and does
// not cause a visible reflow after mount.
restoreTextScale();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<App />} />
        <Route path="/join" element={<App />} />
        <Route path="/display" element={<MirrorApp />} />
        <Route path="*" element={<Navigate to="/join" replace />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
);
