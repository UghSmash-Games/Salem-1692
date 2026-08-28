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

### `rejoin_room`
- **Direction:** client → server
- **Sender role:** an un-joined socket (the reconnecting phone has a NEW socket after any drop)
- **Payload:** `{ code: string, playerId: string, token: string }`
- **Response:** `joined` → player (same shape as a fresh join), or `error_msg` on failure
- **Side effect:** `player_rejoined` → host
- **Note:** Reclaims a seat the player already holds. A phone loses its socket constantly in normal
  play — the screen locks, the browser backgrounds the tab, wifi blips — and without this the seat
  was gone for good: `join_room` always mints a fresh `playerId`, so the returning player became a
  stranger to a host that still held their tryals under the old id.
- 🔴 **`token` IS THE AUTHORIZATION.** `playerId` is public (it appears in every `game_state_update`),
  so it can never be what proves ownership of a seat. The token is a 32-hex-character secret minted
  server-side at join, sent to that one socket in `joined`, and never broadcast, never given to the
  host, and never included in any public payload. Without it, any player could reclaim any seat and
  receive that seat's `private_state` — the tryal cards and role of another player. ⛔ Do not "fix" a
  failed rejoin by falling back to matching on `displayName`: names are public and duplicable.
- **Failure is deliberately uninformative:** an unknown room, an unknown seat, and a bad token all
  return the same `error_msg` — `{ message: "Could not rejoin", code: "rejoin_failed" }` — so the
  event cannot be used to enumerate which seats exist. The evicted socket in a takeover instead gets
  `{ message: "Seat taken over on another device", code: "seat_taken" }`. `code` is a MACHINE code:
  the evicted phone must drop its stored seat, and recognising that by matching on prose would break
  the moment the copy changed.
- **Newest socket wins.** If the seat is still held by a live socket (a second tab, or a drop the
  server has not noticed yet), the seat rebinds to the newcomer and the previous socket is removed
  from the room — one socket per seat, always, or `private_state` would fan out to two devices.

### `joined`
- **Direction:** server → player or mirror
- **Payload (player):** `{ playerId: string, roomCode: string, token: string }`
- **Payload (mirror):** `{ roomCode: string }`
- **Note:** `token` is the seat secret described under `rejoin_room` — player payload only. The phone
  stores it and replays it on every reconnect. It is NOT sent to the host or any mirror.

### `player_rejoined`
- **Direction:** server → host
- **Payload:** `{ playerId: string, displayName: string }`
- **Note:** The seat is live again on a new socket. The host marks it connected and **re-sends that
  player's state** — the phone reconnected with an empty store and knows nothing: not its tryals, not
  its hand, and not the prompt it may be blocking the game on. Carries no token; the host never sees
  one.

### `player_joined`
- **Direction:** server → host
- **Payload:** `{ playerId: string, displayName: string }`

### `player_left`
- **Direction:** server → host
- **Payload:** `{ playerId: string }`
- **Note:** The socket dropped — it does NOT mean the seat is gone. The relay RESERVES the seat
  (entry kept, `socketId` null) because it cannot know whether a game is running; the **host** decides
  what a departure means. `NetworkGameCoordinator` frees the chair only in the lobby, and mid-game
  holds it (it owns tryal cards, a hand, a turn-order slot and possibly a witch identity) with
  `Player.IsConnected = false`, which drops that seat from the secret-phase wait set so a dropped
  phone cannot stall a phase to its timeout.

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
- **Payload:** `{ playerId: string, actions: [...], unplayableCards: string[] }`
- **Note:** `unplayableCards` are card NAMES in this player's hand that cannot legally be played right
  now (currently Robbery/Scapegoat with fewer than 3 players alive — rulebook p13). The host computes
  this in the same place it computes the `actions` array, and the phone greys those cards out. The
  host **also** refuses the play if a client sends one anyway (host-gated eligibility + server-side
  enforcement — the same two-layer pattern as the Tituba/Parris action buttons).

### `deck_rearrange_request`
- **Direction:** host → server → **one specific player**
- **Recipients:** single player socket matching `playerId` (the Tituba holder)
- **NEVER sent to:** host, mirrors, other players
- **Payload:** `{ playerId: string, cards: string[], seconds: number }`
- **Note:** `cards` is the full deck's card labels in top→bottom order — a large piece of
  private information (Tituba's ability). It is routed to exactly one socket, never broadcast.
  `seconds` is the rearrange window (60) the phone renders as a countdown. The public
  `game_state_update` continues to expose only `deckCount`, never card identities.

### `card_pick_request`
- **Direction:** host → server → **one specific player**
- **Recipients:** single player socket matching `playerId` (a John Proctor / Martha drafter, a Samuel
  Parris discard-pick, or a Curse card-choice)
- **NEVER sent to:** host, mirrors, other players
- **Payload:** `{ playerId: string, cards: string[], pickNumber: number, totalPicks: number, seconds: number, allowDone?: boolean, reason?: string }`
- **Note:** `cards` is a private card pool (an eliminated player's hand, the filtered discard pile, or a
  victim's blue cards) — routed to exactly one socket, never broadcast. `pickNumber`/`totalPicks` are
  display hints ("pick N of up to 3"); `seconds` is the pick window the phone renders as a countdown.
  `allowDone` (Parris "up to N") shows a Done button that submits index -1. `reason` is a machine code the
  phone maps to copy — `"proctor_draft"`/`"parris_discard"` (taking a card) vs `"curse_discard"`
  (discarding an opponent's blue card, incl. the Black Cat as an option). This is NOT a masked secret
  phase — the pick's existence is public; only the card identities are private (same class as
  `deck_rearrange_request`). The public `game_state_update` never exposes hand/blue-card contents.

### `target_request`
- **Direction:** host → server → **one specific player**
- **Recipients:** single player socket matching `playerId`
- **NEVER sent to:** host, mirrors, other players
- **Payload:** `{ playerId: string, prompt: string, targets: string[], seconds: number }`
- **Note:** Asks ONE player to pick another **player** — the sub-target of a two-target card
  (Robbery's recipient, Scapegoat's destination). `prompt` is a machine code (e.g.
  `"robbery_recipient"`, `"scapegoat_recipient"`) the phone maps to copy. `targets` are the eligible
  **PUBLIC player ids** (the host computes eligibility — never self, never the victim, never
  eliminated); the phone resolves them to display names from its existing `game_state_update` board,
  which avoids duplicate-display-name ambiguity. `seconds` is the window the phone shows as a
  countdown. The host **re-verifies** the answer against the same eligibility rule — the client is
  never trusted. If the player declines or the window expires, the card is **not** played and **not**
  consumed. This is NOT a masked secret phase; it is the acting player's own choice, routed to one
  socket as their private decision UI.

### `tryal_pick_request`
- **Direction:** host → server → **one specific player**
- **Recipients:** single player socket matching `playerId` (the chooser)
- **NEVER sent to:** host, mirrors, other players
- **Payload:** `{ playerId: string, targetPlayerId: string, count: number, seconds: number, reason: string }`
- **Note:** Asks a player **which face-down tryal** to act on. `reason` is a machine code —
  `"accusation_reveal"` (threshold crossed) | `"piety_loss_reveal"` (Curse stripped Piety at or over
  the base) | `"conspiracy_reveal"` (step 1, the drawer choosing on the black-cat holder) |
  `"conspiracy_pass"` (step 2). The first three are the same rulebook idea: *the player who caused
  the reveal chooses which card turns.*
- **`"conspiracy_pass"` is the one SIMULTANEOUS use** (rulebook p6: *"All players simultaneously
  choose a face-down tryal card from the player on their left"*). The host sends one of these to
  EVERY alive player in the same frame — each still routed to that player's own socket — on a single
  shared window, and **moves no card until every answer is in or the window expires**. Resolving
  picks as they arrive would let a player take from a neighbour whose row had already changed, and
  would leak order of play. Here the card is TAKEN, not turned: it stays face-down, and its identity
  reaches only the RECEIVER via `private_state`. It is NOT a masked secret phase — every player picks,
  so there is no `acting` subset that submission timing could expose.
- 🔴 **`count` IS THE WHOLE PAYLOAD about those cards.** No labels, and **no slot positions**. The
  chooser picks blind among identical backs, exactly as at a physical table, and the answer is an
  ORDINAL into that face-down subset — the host alone maps ordinal → real `TryalCards` index.
  ⛔ **Never add a `labels` field and never send real indices.** `AddTryalCardAndNotify` **appends**,
  so a real index would let a Conspiracy giver pin a card they just passed to an exact slot and narrow
  the rest by elimination — the same reasoning that keeps `revealedTryals` position-free. (Conspiracy
  step 3 re-shuffles every face-down row, which is what makes even an ordinal safe; if that shuffle is
  ever removed, revisit this.)
- This is NOT a masked secret phase — the reveal's existence is public and only one player is asked.
  It is routed to one socket because it is that player's private decision UI.

### `confirm_request`
- **Direction:** host → server → **one specific player**
- **Recipients:** single player socket matching `playerId`
- **NEVER sent to:** host, mirrors, other players
- **Payload:** `{ playerId: string, prompt: string, items: string[], count: number, seconds: number }`
- **Note:** A generic yes/no confirmation for a character's OWN optional ("may") choice — currently
  Abigail Williams' *"you may discard all accusations in front of you"*. `prompt` is a machine code
  (e.g. `"abigail_discard"`) the phone maps to copy; `items` are context card labels (her red cards)
  and `count` the numeric context (her accusation total — needed because values differ: Evidence 3,
  Witness 7); `seconds` is the window the phone renders as a countdown.
  This is NOT a masked secret phase — Town Hall identity is PUBLIC, so a holder-only prompt leaks
  nothing (same class as `action_request`'s Tituba/Parris buttons). It is routed to exactly one
  socket only because it is that player's private decision UI, not because the fact is secret.

### `phase_resolve`
- **Direction:** host → server → **all clients in room**
- **Recipients:** host (echo back for sync), all players, all mirrors
- **Payload:** `{ revealAt: number }` _(UTC timestamp, typically 3 seconds in the future)_
- **Note:** All screens calculate local delay as `revealAt - Date.now()` and trigger reveal animations at the same wall-clock moment.

### `public_reveal`
- **Direction:** host → server → **all players + all mirrors**
- **Recipients:** all players, all mirrors (NOT echoed to host — the host renders from its own model)
- **Payload:** `{ playerId: string, cards: string[], reason: string }`
- **Note:** A genuinely PUBLIC, one-shot announcement that a player is showing specific cards to
  the whole table (e.g. Giles Corey: "IF YOU DRAW TWO RED CARDS, SHOW THE OTHER PLAYERS…").
  `playerId` is the actor's public id; `cards` are the shown card labels (names only, e.g.
  `["Evidence","Witness"]`); `reason` is a machine code for the trigger (e.g. `"giles_corey"`)
  the client uses to phrase the toast. Carries NO private data — same visibility class as
  `game_state_update.statusCards` (public card names) and `elimination_result`. This is NOT a
  masked secret phase; the reveal's existence and content are public by the card rules.

### `game_event`
- **Direction:** host → server → **all players + all mirrors**
- **Recipients:** all players, all mirrors (NOT echoed to the host — it renders from its own send-event)
- **Payload:** `{ kind: string, actorId: string|null, targetId: string|null, cardName: string|null, value: string|null, atMs: number }`
- **Note:** One entry in the public "What Has Passed" event log.
  `kind` comes from a **CLOSED vocabulary** (Unity `GameEventKind`): `game_started`, `phase_changed`,
  `card_played`, `tryal_revealed`, `player_eliminated`, `double_witch_revealed`,
  `confession_revealed`, `gavel_placed`, `game_over`.
  **This enum IS the privacy mechanism.** The wire carries no prose — only a kind, public player ids,
  a public card name, and a SHORT enumerable `value` (a tryal label, a phase name, an `n/limit`
  counter, a winner). The **renderer** turns that into a sentence. Because there is no kind for a
  secret action, the log cannot describe one — it cannot say "Alice voted for Bob" because no such
  kind exists, not because a call site remembered to be careful.
  ⛔ **NEVER** add a kind for secret-phase content (night votes, constable saves, witch identities,
  black-cat placement, or a confession *before* it resolves), and **NEVER** add a free-text field —
  one would turn every call site into a leak. `confession_revealed` fires only at the synchronized
  `revealAt`, when the flip is public by the rulebook, so the masked confess-window timing is
  preserved. `gavel_placed` likewise fires only at `revealAt` and carries the RECIPIENT in
  `targetId` with `actorId` **null** — the rulebook has the token placed in front of a player before
  eyes open (p11), so who was protected is public, but who protected them is not.
  `atMs` is epoch milliseconds stamped by the host; each client formats it in **its own local time**.
  A preformatted `"19:04"` would bake in the host's timezone and break a mirror in another region —
  same principle as `phase_resolve.revealAt`.

### `elimination_result`
- **Direction:** host → server → **all clients in room**
- **Recipients:** all players, all mirrors
- **Payload:** `{ playerId: string, eliminated: boolean, savedBy: string|null }`
- **Note:** `savedBy` is a **LABEL** — `"constable"` | `"confession"` | `""` — and is set from
  `NightResolver.NightOutcome.SavedByLabel`. It is **NEVER a playerId**.
  ⛔ **Do not "improve" it into one.** Naming the saver would publish the **CONSTABLE'S SECRET
  IDENTITY** on a channel that reaches every player and every mirror — the same class of leak as
  putting a role on `game_state_update`. A confession has no saver to name in any case. Renderers
  must map the label to copy ("saved by the constable"), not look it up as a player.

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

### `deck_rearrange_submit`
- **Direction:** player → server → host
- **Sender role:** player ONLY
- **Client sends:** `{ order: number[], confirmed: boolean }`
- **Server forwards to host:** `{ playerId: string, order: number[], confirmed: boolean }`
- **Note:** `order` is a permutation of the original deck indices (top→bottom). Two-stage like
  `secret_phase_submit`: `confirmed: false` on each in-progress move (tentative — the host
  keeps the latest), `confirmed: true` on Confirm or the phone's countdown expiry. The server
  forwards all submissions; the host owns the authoritative 60s deadline and applies the
  latest order received.

### `target_submit`
- **Direction:** player → server → host
- **Sender role:** player ONLY
- **Client sends:** `{ targetPlayerId: string }`
- **Server forwards to host:** `{ playerId: string, targetPlayerId: string }`
- **Note:** The answer to a `target_request`. Single-stage. The host re-validates the chosen id
  against the eligibility rule it used to build the list, and owns the deadline; no answer → the card
  is not played and not consumed.

### `confirm_submit`
- **Direction:** player → server → host
- **Sender role:** player ONLY
- **Client sends:** `{ confirmed: boolean }`
- **Server forwards to host:** `{ playerId: string, confirmed: boolean }`
- **Note:** The answer to a `confirm_request`. Single-stage (no tentative/confirm two-step — the
  answer IS the confirmation). The host owns the authoritative deadline and defaults to `true`
  (take the beneficial action) if no answer arrives before it expires.

### `tryal_pick_submit`
- **Direction:** player → server → host
- **Sender role:** player ONLY
- **Client sends:** `{ ordinal: number }`
- **Server forwards to host:** `{ playerId: string, ordinal: number }`
- **Note:** The answer to a `tryal_pick_request`. Single-stage. `ordinal` indexes the FACE-DOWN subset
  the host described (`0..count-1`), **never** a raw `TryalCards` index. The host re-validates the
  range and owns the deadline.
  ⚠ **A no-answer does NOT cancel.** Unlike `target_submit` (where a timeout means "don't play the
  card"), the reveal is a MANDATORY rules consequence once triggered, so the host flips a RANDOM
  face-down tryal on timeout. "No response" must not let a player dodge a reveal they caused.

### `card_pick_submit`
- **Direction:** player → server → host
- **Sender role:** player ONLY
- **Client sends:** `{ index: number }`
- **Server forwards to host:** `{ playerId: string, index: number }`
- **Note:** `index` is the chosen card's index into the `cards` pool from `card_pick_request`. One pick
  per request (single-stage — the host issues a fresh request for each of the drafter's up-to-3 picks,
  alternating between John and Martha). The host owns the authoritative pick deadline.

---

## Role Enforcement Summary

| Event | `host` can send? | `player` can send? | `mirror` can send? |
|---|---|---|---|
| `create_room` | ✅ | ❌ | ❌ |
| `join_room` | ❌ | ✅ | ❌ |
| `join_mirror` | ❌ | ❌ | ✅ |
| `rejoin_room` | ❌ | ✅ (its own seat, with the token) | ❌ |
| `player_rejoined` | ✅ (server originates) | ❌ | ❌ |
| `game_state_update` | ✅ (originates) | ❌ | ❌ |
| `private_state` | ✅ (originates) | ❌ | ❌ |
| `secret_phase_prompt` | ✅ (originates) | ❌ | ❌ |
| `action_request` | ✅ (originates) | ❌ | ❌ |
| `deck_rearrange_request` | ✅ (originates) | ❌ | ❌ |
| `card_pick_request` | ✅ (originates) | ❌ | ❌ |
| `target_request` | ✅ (originates) | ❌ | ❌ |
| `tryal_pick_request` | ✅ (originates) | ❌ | ❌ |
| `confirm_request` | ✅ (originates) | ❌ | ❌ |
| `phase_resolve` | ✅ (originates) | ❌ | ❌ |
| `public_reveal` | ✅ (originates) | ❌ | ❌ |
| `game_event` | ✅ (originates) | ❌ | ❌ |
| `elimination_result` | ✅ (originates) | ❌ | ❌ |
| `game_over` | ✅ (originates) | ❌ | ❌ |
| `player_action` | ❌ | ✅ | ❌ |
| `secret_phase_submit` | ❌ | ✅ | ❌ |
| `confess` | ❌ | ✅ | ❌ |
| `deck_rearrange_submit` | ❌ | ✅ | ❌ |
| `card_pick_submit` | ❌ | ✅ | ❌ |
| `target_submit` | ❌ | ✅ | ❌ |
| `tryal_pick_submit` | ❌ | ✅ | ❌ |
| `confirm_submit` | ❌ | ✅ | ❌ |

---

## Privacy Rules

These rules are enforced at the server dispatch layer:

1. **`private_state`** is routed to exactly one player socket. It must never appear in any broadcast.
2. **`secret_phase_prompt`** is unpacked per-player. Each player receives only their own `acting` flag. Mirrors and the host never receive this event.
3. **`action_request`**, **`deck_rearrange_request`**, **`card_pick_request`**, **`confirm_request`**, **`target_request`**, and **`tryal_pick_request`** are each routed to exactly one player socket. The deck card list and the draft-pool hand list never appear in any broadcast.
4. Mirrors receive only: `game_state_update`, `phase_resolve`, `public_reveal`, `game_event`, `elimination_result`, `game_over`, `room_closed`.
5. The server attaches `playerId` to all player → host messages so Unity can identify the sender without trusting client-provided IDs.
6. The seat **token** (`joined`, `rejoin_room`) never appears in a broadcast, never reaches the host or a mirror, and is never logged. It is the only thing standing between a reconnecting phone and someone else's `private_state`.
