PR: Phase 3 — Mirror Screen (→ dev)
A passive /display route that a second room opens to watch the game in sync. Web client only — no server or Unity changes (the server already supported the mirror role from Phase 1).
What's included:

/display route with MirrorJoinScreen (code-only) and MirrorScreen (public board)
useMirrorSocket — registers only public events; has no listener for any private event (defense in depth)
useSynchronizedReveal — reveal fires on the phase_resolve timestamp, never on message receipt
NightDawnOverlay, RevealOverlay, DeckSummary components
mock-host extended with n/d/x commands to test the mirror

Please verify:

 cd webclient && npm test — 23 tests pass
 Two /display tabs side by side: press x in mock-host, confirm both reveal in unison
 Mirror DevTools → Network → WS Messages: press p/v in mock-host, confirm no tryals, role, or acting ever reaches the mirror
 useMirrorSocket has no listener for private_state, secret_phase_prompt, or action_request

Reviewer note: sync-checker subagent passed — reveal timing is driven entirely by the revealAt timestamp. The one real-world limitation is cross-device clock skew (out of scope; assumes devices have reasonably synced clocks, per the guide).
