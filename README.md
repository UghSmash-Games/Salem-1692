# Salem 1692 — Running the Game

A Jackbox-style digital adaptation. Three parts run together:

| Part | What it is | Where it runs |
|---|---|---|
| **Unity host** | Authoritative game logic + the TV board | The host PC |
| **Node server** | Socket.io relay (`server/`) | The host PC, port **3000** |
| **Web client** | Player phones + mirror displays (`webclient/`) | Browsers, served on port **5173** |

Unity is *not* a web server. It connects to the Node relay as a client, same as the phones do.

---

## 1. Prerequisites

- **Unity 6000.0.72f1** (the exact version in `ProjectSettings/ProjectVersion.txt`)
- **Node.js** — an **LTS release (20.x or 22.x) is recommended**. Newer odd-numbered versions run the
  game fine but break the test runners; see [Running the tests](#running-the-tests).
- Both devices on the **same Wi-Fi network** for a LAN game (not a guest network — those usually
  isolate clients from each other).

## 2. First-time setup, after cloning

```bash
cd server && npm install
```

```bash
cd webclient && npm install
```

Then open the project in Unity Hub (**Add project from disk** → the repo root, not the `unity/`
folder) and let it import. Open the scene:

**`Assets/Project/Scenes/Game/Networked_Game.unity`**

> ⚠️ `Sandbox_Testing` is a different scene for local/AI development. It keeps the legacy local UI and
> will **not** work for a networked game.

---

## 3. Local host game (everything on one PC)

Use this to test rules, the host board, or the mirror without phones. Three terminals.

**Terminal 1 — server:**

```bash
cd server && npm run dev
```

You should see `Salem 1692 server listening on port 3000`.

**Terminal 2 — web client:**

```bash
cd webclient && npm run dev
```

**Terminal 3 — nothing.** Press **Play** in Unity. The host connects to the server and the lobby
panel shows a four-letter room code.

Now open, in a browser on the same PC:

- Players: `http://localhost:5173/join` — one tab per player
- Mirror display: `http://localhost:5173/display`

Then **start the game** — see [Starting a game](#5-starting-a-game).

### Testing alone

You do not need four browser tabs. In the lobby, tick **Fill empty seats with AI** and set the table
size with the `−` / `+` buttons. One human plus AI to 4 is a complete game.

---

## 4. LAN game (phones + a TV)

The three defaults below are all **localhost-only**, and each one alone will stop phones connecting.
Set all three.

### Step 1 — find the host PC's LAN address

```bash
ipconfig
```

Use the **IPv4 Address** under your active adapter, e.g. `192.168.1.155`. Everything below uses that
as the example — **substitute your own**.

### Step 2 — point the web client at the host PC

Create **`webclient/.env.local`** (git-ignored, machine-specific):

```
VITE_SERVER_URL=http://192.168.1.155:3000
```

Without this the phone loads the page and then tries to reach a server on **its own** localhost.

> Restart Vite after creating or editing this file — `import.meta.env` values are baked in at startup.

### Step 3 — start the server with the LAN origin allowed

```bash
cd server; $env:CORS_ORIGIN="http://192.168.1.155:5173"; npm run dev
```

The server's CORS default is `http://localhost:5173`, so a phone's origin is a *different* origin and
the handshake is refused. This is the failure that survives fixing the other two.

### Step 4 — serve the web client on the network

```bash
cd webclient; npm run dev -- --host
```

Vite prints a **Network:** line — confirm it shows `http://192.168.1.155:5173/`, not just Local.

### Step 5 — set the URL shown on the host screen

In Unity, select **`HostDisplay ▸ LobbyPanel`** and set `HostLobbyPanel ▸ baseUrl` to:

```
192.168.1.155:5173
```

Host and port only — **no `http://`, no trailing slash, no path.** The panel appends `/join` and
`/display` itself.

### Step 6 — Windows Firewall

The first time Node accepts inbound connections Windows prompts. If that prompt was dismissed, both
ports are silently blocked. Allow **node.exe** on **Private** networks for ports **3000** and **5173**.

### Step 7 — connect

Press **Play** in Unity, then from each device:

- **Phones** → `192.168.1.155:5173/join`, enter the room code and a display name
- **Mirror TV** → `192.168.1.155:5173/display`, enter the room code

Both URLs are displayed on the host lobby screen.

---

## 5. Starting a game

Once enough players have joined, press **BEGIN THE TRYALS** on the host lobby screen. It stays
disabled until the table is legal, with the reason underneath ("Waiting for 2 more players").

- **4 players minimum, 12 maximum** — the tryal-card distribution is defined only for that range.
- **Fill empty seats with AI** lets a smaller group start; the stepper sets the total table size.
- **PACE** cycles Normal (1×) / Relaxed (1.5×) / Extended (2×), scaling every timer for players who
  want more time. It locks once the game begins.

> A `TEST — Start Game` option also exists on the `NetworkGameCoordinator` component's context menu
> (right-click its header in the Inspector during Play mode). It predates the button and is kept as a
> fallback.

---

## 6. Troubleshooting

**Phone loads the page but the join button does nothing.**
CORS. Check the server was started with `CORS_ORIGIN` set to the exact origin the phone is using,
including the port.

**Phone can't load the page at all.**
Vite is not on the network. Confirm `--host` and check the **Network:** line. Then the firewall.

**Quick isolation test:** from the phone's browser, open `http://192.168.1.155:3000/health`. It
should return `{"status":"ok"}`. If health works but the game doesn't, the problem is CORS or
`VITE_SERVER_URL`, not the network.

**Room code shows `----` forever.**
The host never reached `room_created`. Check the server is running and Unity's Console for connection
errors.

**Mirror is blank or shows an old layout on a smart TV.**
Smart-TV browsers cache aggressively — load `…/display?v=2` to bypass it. If it's still wrong, open
the same LAN URL on a desktop browser: if that works, the problem is the TV's browser engine, not the
network.

**Everything worked yesterday and now nothing connects.**
Your PC's IP probably changed on a new DHCP lease. It's hardcoded in **three** places, all of which
must agree: `webclient/.env.local`, the `CORS_ORIGIN` env var, and the scene's `baseUrl`. Consider a
DHCP reservation on your router.

**Unity Console: `NullReferenceException` during setup.**
Make sure you opened `Networked_Game`, not `Sandbox_Testing`.

---

## Running the tests

```bash
cd server && npm test
```

```bash
cd webclient && npm test
```

⚠️ **On some machines both suites crash with an out-of-memory / `VirtualAlloc failed` error.** This is
not a code failure: jest and vitest each default to a pool of spawned worker **processes**, and on
affected setups (seen with Node 25.x) every child Node process dies at ~15 MB while the parent is
fine. Run without child processes instead, in **separate shells**:

```bash
cd server && npx jest --runInBand
```

```bash
cd webclient && node ./node_modules/vitest/vitest.mjs run --pool=threads --poolOptions.threads.singleThread
```

Worker *threads* share the parent's memory and work; `--pool=forks` is still a child process and
still fails. An LTS Node generally makes plain `npm test` work.

Green baseline: **server 79**, **webclient 209**.

---

## Repo layout

```
Assets/          Unity game content (scripts, art, prefabs, scenes)
server/          Node.js + Socket.io relay
webclient/       React phone client + mirror display
docs/            Rulebook, protocol spec, character spec, build/editor steps
CLAUDE.md        Architecture rules and invariants — read before changing game logic
```

Key docs: `docs/protocol.md` (socket contract), `docs/character-spec.md` (the 15 Town Hall
characters), `docs/phase-7-editor-steps.md` and `docs/phase-9-editor-steps.md` (Unity scene assembly,
including a hard-won uGUI troubleshooting list).
