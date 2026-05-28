Please verify:

 server/ has src/index.js, rooms.js, dispatch.js and all three pass npm test (34 tests) — run cd server && npm test
 docs/protocol.md exists and defines all socket events
 unity/Assets/Project/Scripts/Networking/ has SocketIOClient.cs, NetworkManager.cs, NetworkMessages.cs, NetworkConnectionTest.cs
 Start npm run dev, enter Play mode in Unity — console shows Engine.io handshake and a room code
 Role enforcement: dispatch.js line by line — a mirror socket sending player_action must be silently ignored, private_state must only go to the target player
 NetworkConnectionTest.cs has a comment noting it should be deleted before Phase 2
 All changes committed to phase-1-architecture with PR open against Cris-New

Reviewer note: The GameStateUpdateMsg in NetworkMessages.cs is intentionally minimal — the full board state schema is defined in Phase 4. The four bugs found in the Unity alpha (asylum, matchmaker cascade, confess window, dawn phase) are documented and will be fixed in Phase 4.
