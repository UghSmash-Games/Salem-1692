# Salem 1692 Socket Protocol

All communication between Unity (host), phone browsers (players), and passive displays (mirrors) flows through the Node.js + Socket.io relay server. The server enforces roles but does not validate game logic — Unity is authoritative.

---

## Room Management Events

### `create_room`
- **Direction:** client → server
- **Sender role:** host (Unity only)
- **Payload:** _(none)_
- **Response:** `room_created` → host

### `room_created`
- **Direction:** server → host
- **Payload:** `{ code: string }`

### `join_room`
- **Direction:** client → server
- **Sender role:** player
- **Payload:** `{ code: string, displayName: string }`
- **Response:** `joined` → player
- **Side effect:** `player_joined` → host

### `join_mirror`
- **Direction:** client → server
- **Sender role:** mirror
- **Payload:** `{ code: string }`
- **Response:** `joined` → mirror

### `joined`
- **Direction:** server → player or mirror
- **Payload (player):** `{ playerId: string, roomCode: string }`
- **Payload (mirror):** `{ roomCode: string }`

### `player_joined`
- **Direction:** server → host
- **Payload:** `{ playerId: string, displayName: string }`

### `player_left`
- **Direction:** server → host
- **Payload:** `{ playerId: string }`

### `room_closed`
- **Direction:** server → all clients in room
- **Payload:** _(none)_
- **Trigger:** host disconnects

---

## Game State Events (Host → Clients via Server)

The host (Unity) emits these events. The server routes them to the correct recipients based on event type. The server never originates game state — it only relays.

### `game_state_update`
- **Direction:** host → server → all players + all mirrors
- **Recipients:** all players, all mirrors
- **Payload:** `{ ... }` _(public board state defined by Unity: accusations, eliminated players, whose turn, blue cards in play)_

### `private_state`
- **Direction:** host → server → **one specific player**
- **Recipients:** single player socket matching `playerId`
- **NEVER sent to:** host, mirrors, other players
- **Payload:** `{ playerId: string, tryals: [...], hand: [...] }`

### `secret_phase_prompt`
- **Direction:** host → server → **each player individually**
- **Recipients:** each player receives only their own copy
- **NEVER sent to:** host, mirrors
- **Host sends:** `{ prompts: [{ playerId: string, prompt: string, targets: [...], acting: boolean }, ...] }`
- **Server delivers to each player:** `{ prompt: string, targets: [...], acting: boolean }`
- **Note:** The server unpacks the batch and sends each player only their entry. No player sees another player's `acting` flag.

### `action_request`
- **Direction:** host → server → **one specific player**
- **Recipients:** single player socket matching `playerId`
- **Payload:** `{ playerId: string, actions: [...] }`

### `phase_resolve`
- **Direction:** host → server → **all clients in room**
- **Recipients:** host (echo back for sync), all players, all mirrors
- **Payload:** `{ revealAt: number }` _(UTC timestamp, typically 3 seconds in the future)_
- **Note:** All screens calculate local delay as `revealAt - Date.now()` and trigger reveal animations at the same wall-clock moment.

### `elimination_result`
- **Direction:** host → server → **all clients in room**
- **Recipients:** all players, all mirrors
- **Payload:** `{ playerId: string, eliminated: boolean, savedBy: string|null }`

### `game_over`
- **Direction:** host → server → **all clients in room**
- **Recipients:** all players, all mirrors
- **Payload:** `{ winner: "witches"|"townspeople", tryals: { ... } }`

---

## Player Action Events (Player → Host via Server)

Players emit these events from their phone clients. The server validates the sender has `role === 'player'` and forwards to the host socket with the sender's `playerId` attached. Events from non-player roles are **silently ignored** — no error is emitted.

### `player_action`
- **Direction:** player → server → host
- **Sender role:** player ONLY
- **Client sends:** `{ card: string, targetPlayerId: string }`
- **Server forwards to host:** `{ playerId: string, card: string, targetPlayerId: string }`

### `secret_phase_submit`
- **Direction:** player → server → host
- **Sender role:** player ONLY
- **Client sends:** `{ selection: string }`
- **Server forwards to host:** `{ playerId: string, selection: string }`
- **Note:** The server forwards all submissions. Unity (host) decides whether to process based on the player's `acting` flag — the server does not filter by `acting`.

### `confess`
- **Direction:** player → server → host
- **Sender role:** player ONLY
- **Client sends:** `{ tryalIndex: number }`
- **Server forwards to host:** `{ playerId: string, tryalIndex: number }`

---

## Role Enforcement Summary

| Event | `host` can send? | `player` can send? | `mirror` can send? |
|---|---|---|---|
| `create_room` | ✅ | ❌ | ❌ |
| `join_room` | ❌ | ✅ | ❌ |
| `join_mirror` | ❌ | ❌ | ✅ |
| `game_state_update` | ✅ (originates) | ❌ | ❌ |
| `private_state` | ✅ (originates) | ❌ | ❌ |
| `secret_phase_prompt` | ✅ (originates) | ❌ | ❌ |
| `action_request` | ✅ (originates) | ❌ | ❌ |
| `phase_resolve` | ✅ (originates) | ❌ | ❌ |
| `elimination_result` | ✅ (originates) | ❌ | ❌ |
| `game_over` | ✅ (originates) | ❌ | ❌ |
| `player_action` | ❌ | ✅ | ❌ |
| `secret_phase_submit` | ❌ | ✅ | ❌ |
| `confess` | ❌ | ✅ | ❌ |

---

## Privacy Rules

These rules are enforced at the server dispatch layer:

1. **`private_state`** is routed to exactly one player socket. It must never appear in any broadcast.
2. **`secret_phase_prompt`** is unpacked per-player. Each player receives only their own `acting` flag. Mirrors and the host never receive this event.
3. **`action_request`** is routed to exactly one player socket.
4. Mirrors receive only: `game_state_update`, `phase_resolve`, `elimination_result`, `game_over`, `room_closed`.
5. The server attaches `playerId` to all player → host messages so Unity can identify the sender without trusting client-provided IDs.
