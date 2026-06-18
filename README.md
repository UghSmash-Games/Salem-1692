PR: Phase 4a — Networked Player Foundation (→ dev)
Converts Day turns to run over the network. The biggest architectural change so far: the game no longer assumes a single local player. Players join from phones, the host spawns/registers them, and turn input comes from the network. Night/dawn, masking, reveals, and timeouts are deferred to 4b/4c.
What's included:

NetworkGameCoordinator (lobby + join handling), NetworkStateBroadcaster (public game_state_update + per-player private_state)
IPlayerInput abstraction with LocalUIInput (existing behavior) and NetworkInput (phone-driven turns) — same game coroutines drive either
playerId ↔ Player registry; dropped the single-local-player assumption (gated behind GameMode.Local/Networked)
Player/AIPlayer prefabs + Networked_Game scene
Idle-timer fix (resets on action, ends turn vs forces draw correctly, no coroutine leak via turnId)
Webclient ActionScreen sends target playerId and supports End Turn / multi-card play

Please verify:

 Local mode unchanged — Sandbox_Testing still plays a full local+AI day loop
 Networked: 4 phones join Networked_Game, each gets only its own private_state (verify in WS frames — no cross-leak)
 Day turns work: draw-2, multi-card play on targets, End Turn; turns advance correctly
 privacy-auditor passed on NetworkStateBroadcaster (game_state_update is public-only)

Reviewer notes:

The [ContextMenu] "TEST — Start Game" on NetworkGameCoordinator is an intentional temporary trigger until the lobby Start-button UI is built — leave it for now.
NativeWebSocket package was added (manifest.json) — reviewer will need it to pull. It auto-installs on project open.
CLAUDE.md "Known Bugs" was corrected during 4a — 3 of 4 documented alpha bugs were already fixed; the corrections are noted with evidence.
