# Salem 1692 — Digital Game Development Guide
### From Solo AI Alpha → Jackbox-Style Multiplayer Release

---

## Overview

The alpha has proven the core game loop works: card draw/turn flow, conspiracy logic, night phase, AI decisions, and win conditions are all functional. The remaining work falls into four major pillars:

1. **Multiplayer Architecture** — Jackbox-style host + phone clients + mirror screens
2. **Private Information Layer** — Tryal cards, secret roles, and identity-masking during secret phases
3. **Full Ruleset Completion** — Town Hall characters, special card interactions, edge cases
4. **Polish & Release** — UI, UX, sound, and deployment

---

## Phase 0 — Claude Code Setup

Configure Claude Code before writing any code. These files give Claude persistent context it cannot infer from the codebase alone, and define specialized subagents for the tasks you'll repeat most.

> **Working style note:** Technical expertise on this project is mixed — Claude Code handles implementation, with a knowledgeable reviewer checking work after each phase. Every phase in this guide ends with a **Reviewer Checkpoint** listing exactly what they should verify before you proceed. Do not start the next phase until the reviewer has signed off on the current one.

### Step 0.0 — GitHub & Branch Workflow

The GitHub repository is the single source of truth for the project. All work — including the Claude Code config files created in this phase — lives in the repo and is committed to git.

**Branch strategy:** Create a new branch for each phase. This gives the reviewer a clean, isolated pull request to check before anything merges to `main`.

```bash
# Clone the repo if you haven't already
git clone https://github.com/your-org/your-repo.git
cd your-repo

# Always check what branch you're on before starting work
git status

# Create a branch for Phase 0
git checkout -b phase-0-claude-code-setup
```

Every file created in this phase gets committed to `phase-0-claude-code-setup`. When the phase is done, open a pull request against `main` and ask your reviewer to check it before merging.

**Folder structure:** Your Unity alpha is already in the repo. You need two additional placeholder folders alongside it. If your Unity project is at the repo root, move it into a `unity/` subfolder first — this keeps all three systems (Unity, server, web client) at the same level:

```bash
# Only do this if Unity files are at the repo root, not already in a subfolder
mkdir unity
git mv Assets unity/
git mv ProjectSettings unity/
# Move any other Unity folders (Packages, UserSettings, etc.)
```

Then create the placeholders:
```bash
mkdir -p server webclient docs
```

Your repo structure should now look like:
```
your-repo/
  unity/          ← Unity alpha project
  server/         ← empty for now
  webclient/      ← empty for now
  docs/           ← for rulebook PDF and this guide
  CLAUDE.md       ← created in Step 0.1
  .claude/        ← created in Steps 0.2 and 0.3
```

Copy your rulebook PDF and this development guide into `docs/`:
```bash
cp /path/to/Salem_Rulebook.pdf docs/
cp /path/to/Salem_1692_Development_Guide.md docs/
```

### Step 0.1 — CLAUDE.md

Place `CLAUDE.md` at the project root and check it into git so the whole team benefits. Claude reads it at the start of every session.

The Salem 1692 `CLAUDE.md` should include:

- **Architecture summary** — the three-repo structure (Unity, Node server, web client)
- **Build and run commands** — exactly as you'd run them in a terminal
- **The three client roles** (`host`, `mirror`, `player`) and how the server enforces them
- **The `acting` flag pattern** — the identity masking system; explicitly flagged as a critical invariant not to break
- **Private state isolation rule** — enforced at server dispatch layer, not UI layer
- **Socket event names** — the canonical list so Claude uses the right names in code
- **Game rules gotchas** — the non-obvious edge cases that trip up implementations
- **Branch naming and PR conventions**

> Use `@path/to/file` imports inside CLAUDE.md to pull in the development guide and rulebook PDF as reference material Claude can fetch on demand.

**What NOT to include:** File-by-file codebase descriptions, standard language conventions, anything Claude can read from code directly. Keep it short — a bloated CLAUDE.md causes Claude to ignore your most important rules.

Run `/init` inside Claude Code to generate a starter CLAUDE.md from your project structure, then replace its content with the Salem-specific version.

### Step 0.2 — Subagents

Subagents run in their own isolated context windows and report back summaries. They keep your main session clean while doing heavy file reading or specialized analysis. Define them in `.claude/agents/` at the project root.

Create these four subagents for this project:

**`rules-verifier`** — Validates any game logic implementation against the rulebook. Invoke with:
```
use the rules-verifier subagent to check my conspiracy resolution implementation
```
Use after implementing any card effect, win condition, character ability, or night phase resolution. Returns a PASS/FAIL with specific line references.

**`privacy-auditor`** — Audits server socket emissions to ensure no private player data leaks to the wrong client. Invoke with:
```
use the privacy-auditor subagent to audit the new night phase broadcast code
```
Use after adding any new socket event or state broadcast. This is the most critical correctness property of the game — the `acting` flag, tryal cards, and player roles must never reach the wrong client.

**`sync-checker`** — Verifies that host and mirror screens use the `phase_resolve` timestamp pattern rather than animating on message receipt. Invoke with:
```
use the sync-checker subagent to verify the elimination reveal animation is properly synchronized
```
Use after implementing any timed animation or phase transition.

**`character-implementer`** — Implements Town Hall character abilities one at a time, including all rulebook edge cases and interactions. Invoke with:
```
use the character-implementer subagent to implement Tituba's ability
```
This agent reads the rulebook, follows the priority order from the guide, writes tests, and runs them before reporting done.

### Step 0.3 — Skills

Skills encode repeatable workflows that Claude invokes on demand. Place each one in `.claude/skills/[name]/SKILL.md`. Unlike subagents, skills don't run in isolated contexts — they load into the current session when the task matches, or when you invoke them directly with `/skill-name`.

Create these four skills for this project:

| Skill | Invoke when... |
|---|---|
| `/add-socket-event` | Adding any new socket event between server and clients |
| `/implement-secret-phase` | Implementing dawn, night witch vote, or constable save |
| `/reveal-tryal` | Implementing any code path that flips a tryal card face-up |
| `/add-character` | Implementing a Town Hall character ability |

Each skill is called out inline throughout the guide at the exact step where it applies.

### Step 0.4 — Workflow Recommendations

**Use plan mode before implementing new phases.** For anything touching the socket protocol or game state, enter plan mode (`Shift+Tab` to toggle), let Claude read the relevant files, then review the plan before switching to implementation mode. This prevents Claude from building in the wrong direction on multi-file changes.

**Use `/clear` between phases.** The server, web client, and Unity systems are largely independent. Clear context when switching between them so earlier file reads don't pollute the working context.

**Use subagents for investigation, not your main session.** When you need to understand how an existing system works before building on it, use:
```
use a subagent to investigate how the current AI night phase logic works and summarize it
```
The subagent reads the files; your main context stays clean for implementation.

**Delegate character abilities to the subagent.** There are 12+ characters, each with edge cases and interactions. Rather than implementing them in your main session, use the `character-implementer` subagent for each one. It handles the rulebook cross-referencing and test writing without filling your main context.

**After two failed corrections, `/clear` and rewrite the prompt.** If Claude keeps missing the `acting: false` silent discard behavior or the `phase_resolve` timestamp pattern, those rules aren't landing. Clear the session, and start with a more explicit prompt that quotes the constraint directly.

---

### ✅ Phase 0 Reviewer Checkpoint

Before merging the `phase-0-claude-code-setup` branch and moving to Phase 1, your reviewer should confirm:

- [ ] `CLAUDE.md` is at the repo root and contains the three client roles, `acting` flag rule, private state isolation rule, and socket event name table
- [ ] `.claude/agents/` contains all four subagent files with valid frontmatter (`rules-verifier`, `privacy-auditor`, `sync-checker`, `character-implementer`)
- [ ] `.claude/skills/` contains all four skill folders, each with a `SKILL.md` (`add-socket-event`, `implement-secret-phase`, `reveal-tryal`, `add-character`)
- [ ] `docs/` contains the rulebook PDF and development guide
- [ ] `unity/`, `server/`, `webclient/` folders exist at the repo root
- [ ] Running `claude` from the repo root and asking *"what does our CLAUDE.md say about the acting flag?"* returns the correct identity masking description
- [ ] All changes are committed to `phase-0-claude-code-setup` and a pull request is open against `main`

**Reviewer note:** No code has been written yet — this phase is purely configuration. The main thing to verify is that CLAUDE.md is tight and accurate. If any rule in it is vague or missing, fix it now before Claude starts building on top of it.

---

## Phase 1 — Architecture & Infrastructure

This is the most consequential phase. Every decision here affects everything that follows.

### Step 1.1 — Choose Your Network Stack

Jackbox-style means one Unity host screen (TV/PC), mirror screens in other rooms, and players connecting via their phone browsers. Unity should not be the web server. The recommended stack:

```
Unity Host (Game Logic) ←→ Node.js + Socket.io Server ←→ Player Phone Clients (React or plain HTML)
                                      ↕
                               Mirror Screen Clients (passive display)
```

- **Node.js + Socket.io** handles room codes, message routing, and state relay
- **Unity** runs all authoritative game logic and sends state snapshots to the server
- **Player phone clients** are lightweight — they display private info and send player actions back
- **Mirror screen clients** receive only public state and render identically to the host screen

> **Why not full Unity Netcode or Photon?** Those are peer-to-peer or relay models built for symmetric clients. Jackbox-style is asymmetric: one authoritative host, many thin clients. Socket.io fits this perfectly.

### Step 1.2 — Three Client Roles

Every connection to the server must declare one of three roles at join time:

| Role | Description | Receives |
|---|---|---|
| `host` | Unity game logic screen (Room 1) | All events; sends authoritative state |
| `mirror` | Passive display screen (Room 2+) | Public state only; read-only |
| `player` | Phone client in any room | Private state + masked prompts |

The server enforces these roles. A `mirror` client that sends a `player_action` message is silently ignored.

### Step 1.3 — Define the Message Protocol

> 🛠 **Skill: `/add-socket-event`** — Use this skill every time you add a new socket event from this point forward. It covers all four required touch points: protocol definition, server dispatch with role enforcement, Unity handler, and web client handler. Define the initial events below manually to establish the pattern, then invoke the skill for every event added during later phases.

Before writing any networking code, define every message type. The `acting` flag (Step 1.3b) is the key addition for identity masking.

**Server → Player Client:**
| Message | Payload |
|---|---|
| `game_state_update` | Public board state (accusations, eliminated players, whose turn) |
| `private_state` | That player's tryal cards, hand of cards |
| `secret_phase_prompt` | Prompt UI definition + **`acting: true/false`** flag |
| `action_request` | What input is needed from this player on their turn |
| `phase_resolve` | Timestamp for synchronized reveal countdown across all screens |
| `elimination_result` | Who was eliminated or saved |
| `game_over` | Winner (witches/townspeople), reveal all tryal cards |

**Server → Host + All Mirror Screens:**
| Message | Payload |
|---|---|
| `game_state_update` | Full public board state |
| `phase_resolve` | Synchronized reveal countdown timestamp |
| `elimination_result` | Who was eliminated or saved |

**Player Client → Server:**
| Message | Payload |
|---|---|
| `player_action` | Card played, target player ID |
| `secret_phase_submit` | Selection made (processed only if `acting: true`) |
| `confess` | Tryal card index to reveal |

### Step 1.3b — The `acting` Flag (Identity Masking)

This is the core of the masking system. During any secret phase, the server sends a `secret_phase_prompt` to **every** player — not just witches or the constable. The payload is identical in structure for all players. The only difference is a server-side flag:

```json
// Witch receives:
{ "prompt": "night_vote", "targets": ["Alice", "Bob", "Carlos"], "acting": true }

// Non-witch receives:
{ "prompt": "night_vote", "targets": ["Alice", "Bob", "Carlos"], "acting": false }
```

The phone client renders the exact same UI regardless of `acting`. When a player submits, the client sends `secret_phase_submit`. The server processes the submission only if that player's `acting` flag is `true`. All other submissions are silently discarded — but the submitting player's phone shows the same confirmation animation regardless.

**Result:** From the outside, every phone lights up with the same screen. Every player taps something. No one can tell who is acting and who is not.

> 🔎 **Subagent: `privacy-auditor`** — Once you have implemented the server-side dispatch for `secret_phase_prompt` and `private_state`, run this subagent before proceeding. The acting flag and tryal card dispatch are the most sensitive data paths in the entire project. Catching a leak here is far cheaper than finding it later across multiple phases.
>
> ```
> use the privacy-auditor subagent to audit the secret_phase_prompt and private_state dispatch code
> ```

### Step 1.4 — Room Code System

- On game start, Unity generates a 4-letter room code (e.g. `MAST`)
- Code is sent to Node server, which opens a Socket.io room
- **Players** navigate to `yourgame.com/join`, enter the code and a display name → role: `player`
- **Mirror screens** navigate to `yourgame.com/display`, enter the code → role: `mirror`
- Server relays all join events to Unity; Unity assigns player slots (mirrors are not assigned slots)
- Display the room code and display URL prominently on the host screen during lobby

### Step 1.5 — Unity WebSocket Client

Use `NativeWebSocket` or `WebSocketSharp` in Unity to connect to your Node server. Unity becomes a `host` client of its own server — it sends authoritative state, the server fans it out to all other clients.

```
Unity  →  [game_state_update]       →  Node Server  →  [broadcast to players + mirrors]
Unity  ←  [secret_phase_submit]     ←  Node Server  ←  [from one player, if acting:true]
Unity  ←  [player_action]           ←  Node Server  ←  [from active player on their turn]
```

---

### ✅ Phase 1 Reviewer Checkpoint

Before merging and moving to Phase 2, your reviewer should confirm:

- [ ] Node server starts without errors (`cd server && npm run dev`)
- [ ] A room code is generated and a Socket.io room opens correctly
- [ ] All three client roles (`host`, `mirror`, `player`) are enforced server-side — a `mirror` socket sending `player_action` is silently ignored
- [ ] Unity connects to the server via WebSocket and the connection is stable
- [ ] Player and mirror join flows work end-to-end (phone at `/join`, mirror at `/display`)
- [ ] `docs/protocol.md` exists and lists all events defined so far
- [ ] No private player data appears in any broadcast to `host` or `mirror` role clients
- [ ] All changes committed to a `phase-1-architecture` branch with a pull request open

**Reviewer note:** The role enforcement in Step 1.2 is the most important thing to verify here. Test it manually: connect a socket with role `mirror`, try emitting `player_action`, and confirm the server ignores it. This can't be caught by reading code alone.

---

## Phase 2 — Player Phone Client

The phone client handles three distinct states: idle display, turn interaction, and secret phase masking.

### Step 2.1 — Private Information Screen (Idle State)

When it is not a player's turn and no secret phase is active, their phone shows:
- Their tryal cards (face-up, visible only to them)
- Their current hand of cards (names only, not playable yet)
- The public board summary (accusations on each player, eliminated players)
- A subtle role indicator: witch / townsperson / constable

**Never** send another player's tryal cards to a client that shouldn't see them. This is enforced at the server level, not the UI level.

> 🔎 **Subagent: `privacy-auditor`** — Run after building the private info screen and its server-side data feed. This is the first time player-specific data is rendered on a client and a good checkpoint before building additional screens on top of the same data layer.
>
> ```
> use the privacy-auditor subagent to audit the private_state server dispatch and player phone data feed
> ```

### Step 2.2 — Action Prompt Screens (Active Turn)

When it is a player's turn, the phone switches to an interactive prompt:

- **Turn choice** — "Draw 2 cards" vs "Play cards"
- **Card selection** — scrollable hand, tap to select a card to play
- **Target selection** — list of valid targets (enforce "cannot target yourself" here, server-side)
- **Confess prompt** — shown to all players during night confess window; tap a tryal card to reveal for immunity

### Step 2.3 — Secret Phase Masking Screens

> 🛠 **Skill: `/implement-secret-phase`** — Use this skill when building each masking screen. It encodes the full `acting` flag pattern: identical payload to all players, silent server-side discard for non-acting submissions, and consistent confirmation timing. The skill also covers the parallel witch vote + constable save pattern and explicit tests for the non-acting path. Invoke it for dawn, night witch vote, and constable save screens.

During dawn and night phases, every player receives a `secret_phase_prompt`. The phone client renders one of the following identical UIs for all players regardless of role:

**Dawn — Black Cat Placement:**
All players see a list of player names with a "Place the black cat" header. Witches' selections are processed. Non-witches' selections animate a confirmation and are discarded.

**Night — Witch Vote:**
All players see a list of player names with an "Choose a player" header. Witch submissions are tallied for majority vote. All others are discarded.

**Night — Constable Save:**
All players see a list of player names with a "Protect a player" header. Only the constable's submission moves the gavel token. All others are discarded.

> **Timing detail:** After submission, every phone shows the same "waiting for others..." state. This prevents timing attacks — a fast submitter reveals nothing about whether they acted or not.

**Critical UI rule:** The masking screens must use identical layout, button placement, animation timing, and confirmation feedback for acting and non-acting players. Any visual difference — even a loading spinner that appears faster for one group — breaks the illusion.

### Step 2.4 — Spectator / Eliminated Player Screen

When a player is eliminated, their phone switches to a spectator view showing the full public board. They can still see the game proceed. Optionally, let them become a moderator and see additional info (see Phase 7).

---

### ✅ Phase 2 Reviewer Checkpoint

Before merging and moving to Phase 3, your reviewer should confirm:

- [ ] Each player's phone shows only their own tryal cards — opening another player's socket in a browser tab never reveals a different player's private state
- [ ] The secret phase masking screens look **pixel-identical** on a witch's phone and a non-witch's phone — screenshot both and compare
- [ ] A non-witch submitting during a secret phase sees the same confirmation animation as a witch, with no timing difference
- [ ] The server silently discards non-witch submissions — add a temporary server log to confirm the discard fires
- [ ] Eliminated player's phone switches to spectator view correctly
- [ ] All changes committed to a `phase-2-phone-client` branch with a pull request open

**Reviewer note:** The masking screen comparison is the critical test. Have two people join a test game on separate phones — one as a witch, one as a townsperson — and compare their screens during the night phase side by side. Any visual or timing difference is a bug, not a cosmetic issue.

---

## Phase 3 — Mirror Screen System

The mirror screen allows a second room (household, location) to watch the same host display in sync.

### Step 3.1 — What the Mirror Receives

Mirror screens subscribe to the server's public broadcast channel. They receive:
- All `game_state_update` events
- All `phase_resolve` countdown timestamps
- All `elimination_result` events
- Night/dawn phase start and end signals (for overlays and animations)

Mirror screens **never** receive private player state, the `acting` flag, or any player phone prompts.

> 🔎 **Subagent: `privacy-auditor`** — Run after implementing the mirror broadcast layer. The mirror is the most likely place for a private state leak — it receives broad game state updates and a subtle filter bug could expose tryal cards or role data to a passive display screen.
>
> ```
> use the privacy-auditor subagent to audit all server broadcasts that reach mirror role clients
> ```

### Step 3.2 — Mirror Join Flow

The mirror screen is a browser page, not a Unity build. Build it as a separate route in your web client (`/display`). On load:
1. User enters the room code
2. Server validates code and assigns role: `mirror`
3. Server immediately sends the current `game_state_update` so the mirror syncs to the current game state
4. Mirror renders identically to the host screen from this point forward

### Step 3.3 — Synchronized Reveals (Latency Handling)

Mirrors in other rooms may lag 200–800ms behind the host depending on internet connections. For most events this is invisible. For dramatic moments (night elimination reveal, tryal flip), use a **server-coordinated countdown**:

1. Server broadcasts `phase_resolve` with a UTC timestamp 3 seconds in the future
2. Both the host (Unity) and all mirror screens receive this timestamp
3. Each screen calculates local delay: `revealAt - Date.now()`
4. All screens trigger the reveal animation at the same wall-clock moment

This keeps the dramatic beat synchronized across rooms even with differing latencies.

> 🔎 **Subagent: `sync-checker`** — Run after implementing the `phase_resolve` timestamp pattern in both Unity and the mirror browser client. This is the first time the pattern exists in code — verify both implementations use the timestamp correctly before it gets copied as a pattern for later reveals.
>
> ```
> use the sync-checker subagent to verify the phase_resolve timestamp is used correctly in Unity and the mirror client
> ```

### Step 3.4 — Players in the Mirror Room

Players in Room 2 join on their phones exactly like players in Room 1 — via `yourgame.com/join` with the same room code. The game has no concept of which room a player's phone is in. The mirror screen in Room 2 is purely a display supplement; it has no bearing on game logic or player assignment.

---

### ✅ Phase 3 Reviewer Checkpoint

Before merging and moving to Phase 4, your reviewer should confirm:

- [ ] A browser at `/display` with the room code connects as a `mirror` client and receives game state updates
- [ ] The mirror screen displays identically to the host Unity screen
- [ ] The mirror screen receives **no** private player data — tryal cards, role, and `acting` flag never appear in mirror socket payloads (check with browser dev tools Network tab)
- [ ] A reveal animation triggered on the host screen fires within 100ms on the mirror screen — test with two screens side by side
- [ ] Players joining from "Room 2" phones connect to the same game as Room 1 players with no issues
- [ ] All changes committed to a `phase-3-mirror-screen` branch with a pull request open

**Reviewer note:** The network tab check is essential — look at the raw WebSocket frames in the browser dev tools on the mirror screen and confirm there is no `tryals`, `role`, or `acting` field anywhere in the payloads it receives.

---

## Phase 4 — Night & Dawn Phases (Real Players)

The alpha's AI-driven night phase needs to be rebuilt for real async human input with masking applied throughout.

> 🛠 **Skill: `/implement-secret-phase`** — Use for every secret phase in this section (dawn, night witch vote, constable save). The skill walks through the exact server-side acting flag logic, parallel phase resolution, timeout handling, and the non-acting path tests. Use it before writing any phase handler code.
>
> 🛠 **Skill: `/reveal-tryal`** — Use when implementing step 9 of the night phase flow (the `phase_resolve` timestamp and elimination reveal animation). The skill covers the correct sequence: win condition check before broadcast, synchronized timestamp pattern for host + mirrors, and multiple-witch-card announcement logic.

### Step 4.1 — Dawn Phase Flow

1. Unity signals server: `dawn_start`
2. Server sends `secret_phase_prompt` with `acting: true` to all witches, `acting: false` to all others
3. All players see the identical black cat placement screen
4. Witches select a target; if multiple witches, first submission wins (or use consensus — see note)
5. All phones show "waiting..." confirmation after submission
6. Server relays final placement to Unity; Unity updates board and broadcasts `game_state_update`
7. Server signals `dawn_end`; all phones return to idle

> **Multiple witches during dawn:** Either first-submission-wins (simpler) or require all witches to agree (more interesting but slower). First-submission-wins is recommended for pacing.

### Step 4.2 — Night Phase Flow

1. Unity signals `night_start`; host screen and all mirrors show atmospheric overlay
2. Server sends `secret_phase_prompt` (witch vote variant) to all players with appropriate `acting` flags
3. Server sends `secret_phase_prompt` (constable save variant) to all players simultaneously — same masking applies
4. Witches vote; majority wins, ties broken randomly. Constable selects save target. Both run in parallel.
5. After both resolve (or timeout), server sends `confess_window_open` to all players
6. All players see the confess prompt (this one is genuinely identical — anyone can confess)
7. Hourglass timer runs (recommend 15–20 seconds); server collects any confessions
8. Server resolves elimination: check gavel token, confession, asylum card
9. Server broadcasts `phase_resolve` timestamp; host + mirrors animate reveal in sync
10. Server broadcasts `elimination_result`; Unity updates board

### Step 4.3 — Timeout Handling

Not all players will act promptly. Set timeouts for each secret phase window:
- Dawn: 30 seconds
- Night witch vote: 45 seconds
- Night constable save: 30 seconds (runs in parallel with witch vote, same window)
- Confess window: 15–20 seconds (fixed, matches hourglass)

On timeout, the server resolves with whatever input has been received. If no witch submitted, select a random valid target. If no constable submitted, the gavel token is not placed.

> 🔎 **Subagent: `rules-verifier`** — Run after the full night and dawn phase logic is complete. The night phase is the most rule-dense section of the game — parallel resolution, confess window, gavel token, asylum card, and confession immunity all interact. Verify the full sequence against the rulebook before moving to Phase 5.
>
> ```
> use the rules-verifier subagent to validate the night phase resolution logic against the rulebook
> ```
>
> 🔎 **Subagent: `sync-checker`** — Run after implementing the night elimination reveal in Step 4.2. The night reveal is the highest-stakes use of `phase_resolve` — both rooms watching the same elimination must see the animation fire simultaneously.
>
> ```
> use the sync-checker subagent to verify the night elimination reveal animation is correctly synchronized
> ```

---

### ✅ Phase 4 Reviewer Checkpoint

Before merging and moving to Phase 5, your reviewer should confirm:

- [ ] A full game can be played start to finish with real players (no AI) — dawn, day turns, night, and elimination all work
- [ ] Dawn: witches place the black cat with eyes closed; all phones show the same screen during this phase
- [ ] Night: witch vote and constable save resolve in parallel — the constable prompt does not wait for witches to finish
- [ ] Confess window opens after night resolution and the hourglass timer is visible
- [ ] All three immunity paths work: gavel token saves the target, confession saves the confessor, asylum card saves the holder
- [ ] Witch timeout fires correctly and selects a random target
- [ ] Constable timeout fires correctly and no gavel is placed
- [ ] Night elimination reveal animates simultaneously on host and mirror screens
- [ ] All changes committed to a `phase-4-night-dawn` branch with a pull request open

**Reviewer note:** Play a full test game with at least 4 real people across two devices (host screen + phones). The night phase is the most complex sequence in the game — bugs here affect every single game played. Don't approve this phase without a live playtest.

---

## Phase 5 — Full Ruleset Implementation

### Step 5.1 — Card Types (Complete Each Category)

**Red cards (accusation system):**
- Track accusation count per player
- At 7 accusations: the *playing player* chooses which tryal to reveal
- Accusations do not carry over after a tryal is revealed
- Piety card: doubles accusations needed (14 to reveal); if piety is removed and player already has 7+, immediately reveal a tryal (player who removed piety chooses which)

> 🛠 **Skill: `/reveal-tryal`** — Use when implementing the 7-accusation tryal reveal trigger and the piety-removal immediate reveal. The skill covers the correct operation order (win condition check before animation), the `phase_resolve` timestamp pattern, and the multiple-witch-card announcement. Also use it when implementing the confession reveal and the conspiracy step 1 reveal in Step 5.3.

**Blue cards (persistent):**
- Asylum: grants immunity from night elimination
- Stocks: skip that player's next turn, then discard
- Matchmaker: link two players; if one is eliminated, both are eliminated (with exceptions — see Step 5.4)

**Green cards (one-time use):**
- Alibi, Scapegoat, Robbery: enforce the "cannot involve yourself" rule server-side
- Scapegoat/Robbery disabled when only 2 players remain

> 🔎 **Subagent: `rules-verifier`** — Run after implementing each card category (red, blue, green) before moving to the next. Card interactions compound — a bug in how accusations carry over will surface unexpectedly in matchmaker and piety scenarios later.
>
> ```
> use the rules-verifier subagent to validate the red card accusation system implementation
> ```

### Step 5.2 — Town Hall Character Abilities

> 🛠 **Skill: `/add-character`** — Use this skill for every character in the list below, in order. It reads the rulebook edge cases on pages 12–14, follows the existing character pattern, handles ability inheritance, writes tests, and runs the suite before reporting done. Do not implement characters in your main Claude Code session — delegate each one to this skill to keep context clean. The skill also enforces the priority order so later characters' interactions are already in place when you reach them.
>
> 🔎 **Subagent: `character-implementer`** — Use alongside `/add-character` for characters with heavy rulebook cross-referencing (John Proctor, Martha Corey, Thomas Danforth, George Burroughs). The skill runs in your main session and guides the implementation steps; the subagent runs in isolated context and handles the deep rulebook reading without consuming your main session's context window. For simpler characters, the skill alone is sufficient.
>
> ```
> use the character-implementer subagent to implement John Proctor's ability including all inheritance edge cases
> ```

Implement one at a time and test before moving to the next. Priority order:

1. **Tituba** — Can view and rearrange the deck; may draw and rearrange in same turn
2. **Cotton Mather** — Evidence cards count as 3 accusations
3. **Thomas Danforth** — Reduced accusation rate; special math vs George Burroughs
4. **George Burroughs** — 14 accusations per tryal (7 vs Thomas); 16 with piety (12 vs Thomas)
5. **John Proctor** — Gains cards from eliminated players; ability inheritance with Martha Corey
6. **Martha Corey** — Inherits John's ability; Cotton Mather interaction on elimination
7. **Mary Warren** — Unaffected by matchmaker elimination chain
8. **Remaining characters** — implement per rulebook

> For 7 or fewer players, implement the "deal two town hall cards, keep one" draft mechanic in the lobby flow.

### Step 5.3 — Conspiracy Card Edge Cases

> 🛠 **Skill: `/reveal-tryal`** — Conspiracy step 1 triggers a tryal reveal (the player who drew conspiracy reveals one tryal belonging to the black cat owner). Use the skill for this reveal path, paying attention to the special rule that the black cat owner chooses which tryal is revealed if they drew conspiracy themselves.

Verify these against the alpha's existing logic:
- Black cat owner who draws conspiracy **chooses** which of their tryals is revealed in step 1
- Previously-revealed tryals are NOT moved — only face-down cards shift
- Player who gains a witch card is aligned with witches immediately
- Player who loses their only witch card remains a witch for the rest of the game
- Conspiracy resolves fully before night begins, even if it's the last card drawn

### Step 5.4 — Witch & Matchmaker Edge Cases

**Witch cards:**
- A player can have more than one witch card
- If one witch card is revealed, player announces they have at least one more — NOT eliminated
- Eliminated only when the last witch card is revealed

**Matchmaker:**
- A player cannot receive a second matchmaker card if they already have one
- If one linked player is eliminated at night, both are eliminated — even if the other confessed or was saved
- If matched with Mary Warren when she is eliminated, the matched player is still eliminated (Mary is unaffected)
- If matchmaker would cause both teams to lose simultaneously, only the intended target is eliminated

### Step 5.5 — Win Condition Edge Cases

- Townspeople win by revealing all witch tryal cards — witches do not need to be eliminated
- Witches win by eliminating all non-witches OR if the last townsperson becomes a witch
- In the latter case, that final player **loses** — they are not a winner
- Validate win conditions after: every tryal reveal, every elimination, every conspiracy resolution

> 🔎 **Subagent: `rules-verifier`** — Run after implementing win conditions and all edge cases in Steps 5.3–5.5. Win condition bugs are the hardest to catch in normal play because they only surface in specific game states. The subagent will check your implementation against the full set of documented edge cases systematically.
>
> ```
> use the rules-verifier subagent to validate win conditions, conspiracy edge cases, and witch/matchmaker interactions
> ```

---

### ✅ Phase 5 Reviewer Checkpoint

Before merging and moving to Phase 6, your reviewer should confirm:

- [ ] All red/blue/green card effects work correctly, tested one category at a time
- [ ] Accusation counter resets to 0 after each tryal reveal — does not carry over
- [ ] Piety doubles accusation requirement; removing piety with 7+ accusations immediately reveals a tryal
- [ ] Matchmaker chain fires correctly even when the second player was saved that night
- [ ] All 12+ Town Hall character abilities are implemented and tested in priority order
- [ ] Martha Corey / John Proctor inheritance works in both directions
- [ ] Thomas Danforth vs George Burroughs accusation math is correct with and without piety
- [ ] Win condition fires after tryal reveal, elimination, AND conspiracy — not just at end of turn
- [ ] Last townsperson becoming a witch via conspiracy causes that player to lose (not win)
- [ ] All changes committed to a `phase-5-full-ruleset` branch with a pull request open

**Reviewer note:** This is the longest phase and the most likely to accumulate small bugs. Review character implementations one at a time against the rulebook pages 12–14 rather than as a batch. Pay special attention to the win condition after conspiracy — it fires mid-phase and is easy to miss.

---

## Phase 6 — 2-3 Player Ghost Mode

Implement after core multiplayer is stable. Self-contained variant:

- Add 1–2 ghost player slots (automated, no human behind them)
- Each ghost turn: discard one card from top of deck; if black card, resolve it
- Conspiracy: player to the right chooses which tryal passes to a ghost
- Being "framed" at night loses 2 tryals instead of elimination; player to target's left chooses which 2
- Constable may save themselves in this mode
- Win: triggered when any player or ghost with a witch card is eliminated, OR all players become witches

> 🔎 **Subagent: `rules-verifier`** — Run after implementing ghost mode. The win conditions and night phase behavior differ meaningfully from the standard game — framing loses tryals instead of eliminating, and the constable self-save is unique to this mode. Verify the full variant against the rulebook before treating it as complete.
>
> ```
> use the rules-verifier subagent to validate the 2-3 player ghost mode rules including win conditions and night phase differences
> ```

---

### ✅ Phase 6 Reviewer Checkpoint

Before merging and moving to Phase 7, your reviewer should confirm:

- [ ] 2-player game works with 2 ghost slots; 3-player game works with 1 ghost slot
- [ ] Ghost turns correctly discard the top card and resolve black cards immediately
- [ ] Being "framed" at night loses exactly 2 tryals instead of eliminating the player
- [ ] Constable can save themselves (unlike standard rules)
- [ ] Win condition fires when any player/ghost with a witch card is eliminated
- [ ] All changes committed to a `phase-6-ghost-mode` branch with a pull request open

**Reviewer note:** Play a full 2-player and 3-player test game. Ghost mode has different enough win conditions that it can feel like a separate game — verify it is actually fun and the ghost automation doesn't feel broken before moving on.

---

## Phase 7 — Host Screen UI

The host screen is what both rooms watch. It must communicate game state clearly at a glance with no private information visible.

### Key UI Elements:
- **Player roster** — names, accusation counters, blue cards in front of them, eliminated status
- **Deck + discard** — card count visible; current active card animated when played
- **Night/Dawn overlay** — full-screen atmospheric animation while secret phases run (masks all activity)
- **Accusation board** — per-player progress toward 7 accusations
- **Tryal reveal animation** — dramatic flip, synchronized via `phase_resolve` timestamp

> 🔎 **Subagent: `sync-checker`** — Run after polishing the tryal reveal and any other timed animations on the host screen. Polish work often involves tweaking animation timing, which can accidentally break the `phase_resolve` timestamp pattern if delays are hardcoded instead of calculated from the server timestamp.
>
> ```
> use the sync-checker subagent to verify all host screen animations still use the phase_resolve timestamp after polish changes
> ```
- **Room code + display URL** — visible during lobby for both player phones and mirror screens
- **Active player indicator** — clear highlight on whose turn it is

### Avoid:
- Showing any private information (hands, tryal cards) on the host or mirror screens
- Any visual difference between the host screen and mirror that could reveal information

---

### ✅ Phase 7 Reviewer Checkpoint

Before merging and moving to Phase 8, your reviewer should confirm:

- [ ] All game state is readable at a glance on the host screen — accusation counts, eliminated players, whose turn, blue cards in play
- [ ] Night/dawn overlay covers the full screen with no player data visible during secret phases
- [ ] Tryal reveal animation is dramatic and synchronized with the mirror screen
- [ ] Room code and display URL are visible during lobby
- [ ] No private information (hands, tryal cards) appears anywhere on host or mirror screens
- [ ] All changes committed to a `phase-7-host-ui` branch with a pull request open

---

## Phase 8 — Moderator Mode

Optional dedicated facilitator view for larger groups or first-time players:

- Moderator joins via `yourgame.com/mod` with the room code + a moderator passcode set at lobby creation
- Sees: all tryal cards for all players, witch identities, constable identity, deck order
- Receives moderator script prompts from the rulebook on screen
- Can manually trigger dawn/night phases
- Does not have a player slot or tryal cards

> 🔎 **Subagent: `privacy-auditor`** — Run after implementing the moderator data feed. The moderator sees everything — all tryal cards, witch identities, constable identity, deck order. This is intentional, but the broadcast must be gated exclusively to the moderator socket. A misconfigured emit could accidentally send this payload to a mirror or player client.
>
> ```
> use the privacy-auditor subagent to audit the moderator data feed and confirm it cannot reach player or mirror clients
> ```

---

### ✅ Phase 8 Reviewer Checkpoint

Before merging and moving to Phase 9, your reviewer should confirm:

- [ ] Moderator screen shows all tryal cards, witch identities, and constable identity
- [ ] Moderator script prompts display correctly and match the rulebook
- [ ] Moderator data feed is completely isolated — opening the moderator URL without the passcode shows nothing
- [ ] A player or mirror client cannot receive moderator payload data under any circumstances (check with network tab)
- [ ] All changes committed to a `phase-8-moderator` branch with a pull request open

---

## Phase 9 — Polish

### Sound Design
- Ambient colonial town atmosphere during turns
- Distinct audio cues for: card draw, accusation placed, tryal revealed, night begins, elimination, witch win, townspeople win
- **Masking audio:** During night/dawn, the host screen plays atmospheric audio automatically (the rulebook recommends stomping or creepy music). Build this as an automatic layer — no one has to remember to turn it on.

### Accessibility
- Colorblind-safe accusation indicators (icons + color, never color alone)
- Large text option for phone clients
- Configurable timer lengths in lobby (for players who need more time)

### Lobby & Pregame
- Character assignment screen on phones with biography and ability description
- "Rules" quick-reference tab always accessible on phone client
- Both the player join URL and the mirror display URL shown on host screen during lobby
- Optional: tutorial game vs AI using the existing alpha before a real match

---

### ✅ Phase 9 Reviewer Checkpoint

Before merging and moving to Phase 10, your reviewer should confirm:

- [ ] Masking audio plays automatically on the host screen at dawn and night — no manual trigger needed
- [ ] All audio cues fire at the correct game moments
- [ ] Colorblind mode works — all accusation indicators are readable without color
- [ ] Character biography and ability description display correctly on phone during lobby
- [ ] Rules quick-reference is accessible from the phone at any point during the game
- [ ] All changes committed to a `phase-9-polish` branch with a pull request open

---

## Phase 10 — Testing Checklist

- [ ] Room 1 host + Room 2 mirror display same state within 100ms of each other
- [ ] Mirror screen correctly syncs when joining mid-game
- [ ] Non-witch submits during night vote — confirm server discards silently, phone shows confirmation anyway
- [ ] All phones show identical masking screen layout during dawn and night
- [ ] Witch timeout — server picks random target after 45 seconds
- [ ] Constable timeout — gavel token not placed, game continues
- [ ] 4-player game: 1 witch, 1 constable, 2 townspeople — verify all win paths
- [ ] Conspiracy causes last townsperson to become a witch — verify they lose
- [ ] Constable is also a witch — verify evil constable path
- [ ] Player with 2 witch cards has one revealed — verify they announce and continue
- [ ] Matchmaker: one linked player saved by constable, other eliminated — verify both eliminated
- [ ] Piety removed when player has 7+ accusations — verify immediate tryal reveal
- [ ] Thomas Danforth vs George Burroughs with piety — verify 12 vs 16 accusation counts
- [ ] Scapegoat/Robbery with 2 players remaining — verify cards are disabled
- [ ] Tituba rearranges deck with night card — verify night triggers correctly on draw
- [ ] 2-3 player: ghost turn discards a conspiracy card — verify full resolution
- [ ] All client connections drop simultaneously — verify host can pause and wait for reconnects
- [ ] Player joins with same name as existing player — verify lobby handles gracefully

### ✅ Phase 10 Reviewer Checkpoint

Phase 10 is itself a checkpoint — every item in the checklist above should be tested and signed off by your reviewer before moving to deployment. This is the last gate before the game goes live.

**Reviewer note:** Run through the full checklist with real players, not simulated tests alone. Several of these scenarios (masked timing, mirror sync, connection drops) only reveal themselves in real network conditions.

---

## Phase 11 — Deployment

### Hosting
- **Node server**: Deploy to Railway, Fly.io, or Render — all have free tiers suitable for a small player base
- **Web client** (player phones + mirror screens): Deploy to Vercel or Netlify (free, fast, global CDN)
- **Unity host**: Distribute as a downloadable build (itch.io is ideal) OR as Unity WebGL hosted alongside the web client

### Room Scaling
- Socket.io rooms handle session isolation — one room per game, cleaned up on game end
- A single Node instance comfortably handles 20–50 simultaneous games at launch
- Add Redis adapter if you need to scale horizontally later

### Analytics (Optional)
- Track: average game length, witch win rate by player count, most-revealed tryal card
- Use this to balance optional house rules and inform future design decisions

---

## Recommended Build Order Summary

| Phase | Goal | Skills | Subagents | Effort |
|---|---|---|---|---|
| 0 — Claude Code Setup | CLAUDE.md + subagents + skills + workflow | — | — | Low |
| 1 — Architecture | Node server + Unity WebSocket + room codes | `/add-socket-event` | `privacy-auditor` (Step 1.3b) | High |
| 2 — Phone Client | Private info + action prompts + masking screens | `/implement-secret-phase` | `privacy-auditor` (Step 2.1) | High |
| 3 — Mirror Screen | Passive display + synchronized reveals | `/add-socket-event` | `privacy-auditor` (Step 3.1), `sync-checker` (Step 3.3) | Medium |
| 4 — Night/Dawn | Real player async voting with full masking | `/implement-secret-phase`, `/reveal-tryal` | `rules-verifier`, `sync-checker` (Step 4.3) | Medium |
| 5 — Full Ruleset | Characters, red/blue cards, edge cases | `/reveal-tryal`, `/add-character`, `/add-socket-event` | `rules-verifier` (Steps 5.1, 5.5), `character-implementer` (Step 5.2) | High |
| 6 — Ghost Mode | 2-3 player variant | `/reveal-tryal` | `rules-verifier` | Medium |
| 7 — Host Screen UI | Polish the TV display | — | `sync-checker` | Medium |
| 8 — Moderator Mode | Optional dedicated facilitator view | `/add-socket-event` | `privacy-auditor` | Low |
| 9 — Polish | Sound, masking audio, accessibility, lobby | — | — | Medium |
| 10 — Testing | Full scenario coverage including mirror sync | — | — | High |
| 11 — Deploy | Server + client hosting | — | — | Low |

---

*Guide written for Salem 1692 by Facade Games. Digital adaptation reference only.*
