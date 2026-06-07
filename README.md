What's included:

webclient/ — Vite + React + TypeScript + Tailwind + Zustand
Six screens (Join, Idle, Action, SecretPhase, Spectator, GameOver) plus reusable components
socket/ layer with typed payloads mirroring docs/protocol.md
devtools/mock-host.mjs — fake Unity host for testing without the real game

Please verify:

 cd webclient && npm install && npm test — 14 tests pass
 npm run build produces a clean production build
 The masking test: run server + webclient + mock-host, join two browser tabs, trigger a night vote, confirm witch (acting:true) and non-witch (acting:false) screens are pixel-identical
 In browser dev tools Network tab (WS frames), confirm no player receives another player's tryal cards or acting flag

Reviewer note: If the page loads blank, clear the browser's service workers and site data (Application tab → clear) — a stale service worker from a previous project on port 5173 can intercept the page. Incognito avoids this entirely.
Heads up: This branch was built on top of phase-1-on-latest which is still in review. If Phase 1 needs changes, they'll merge forward into this branch.
