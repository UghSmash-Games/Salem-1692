# Deployment Runbook (Phase 11)

`deployment-options.md` decided **where** (Fly.io for the relay, Netlify for the web client). This is
**how**, in order, with the failure modes that are easy to hit and hard to diagnose.

Three things ship, and they must agree on two URLs:

```
Unity host  ──ws──►  Fly.io relay  ◄──ws──  phones + mirrors (Netlify)
                     ▲ needs the Netlify origin (CORS_ORIGIN)
                     └ both clients need the Fly URL
```

Get either URL wrong and the failure is silent-ish: the server runs fine and simply refuses every
browser. Both are called out below.

---

## Current deployment (live 2026-08-30)

| Piece | Value |
|---|---|
| Relay | `https://salem-1692-relay.fly.dev` — Fly app `salem-1692-relay`, org Ughsmash Games, region **dfw**, 1 machine |
| Web client | `https://salem-1692.netlify.app` — Netlify site `salem-1692`, deploying branch **dev** |
| Unity host | `-server wss://salem-1692-relay.fly.dev` |

**Verified end to end from the deployed site:**
- `/`, `/join`, `/display` and an unknown path all return **200** — the SPA rewrite is live
- the deployed bundle contains `salem-1692-relay.fly.dev` and **no** localhost fallback, so
  `VITE_SERVER_URL` was genuinely baked in at build time
- a WebSocket from the Netlify origin completes the socket.io handshake (real engine.io `sid`
  returned) in ~170ms, and a `fetch` of `/health` from that origin is readable — CORS is right
- the relay's boot log reads `CORS origin: https://salem-1692.netlify.app` with no warning

⚠ **A Netlify site can be created with access control ON.** Every path returned **401** with a
"Login Redirect" page pointing at `app.netlify.com/edge-access` — before routing is evaluated, so
even `/` failed and it looked like a catastrophic build problem. It is a site setting, not config:
**Site configuration → Access & security → Visitor access → Public.** Check this first if the whole
site 401s.

---

## 0. Before you start

- **Deploying ends every game in progress.** Rooms live in memory (`server/src/rooms.js`), so a
  restart drops all of them. The relay shuts down cleanly (SIGTERM → sockets closed, phones get
  `room_closed` and discard their stored seats) but nothing is preserved. **Deploy between sessions.**
- Install the [Fly CLI](https://fly.io/docs/flyctl/install/) and run `fly auth login`.
- A Netlify account connected to this GitHub repo.

---

## 1. Relay server → Fly.io

```bash
cd server
fly apps create <your-app-name>          # app names are globally unique across all of Fly
fly deploy --config fly.toml --app <your-app-name> --ha=false
```

`fly apps create` rather than `fly launch`, because launch is interactive and offers to overwrite the
`fly.toml` in the repo, which is already configured. Set `app` in
[`fly.toml`](../server/fly.toml) to the name you chose.

🔴 **`--ha=false` IS REQUIRED, on every deploy.** `fly deploy` defaults to high availability and
creates **two** machines. Socket.io rooms are per-process, so the second machine would serve phones a
room the host is not in — they would join successfully and see nothing. `fly.toml` cannot express
this; it is a deploy-time flag. Confirm with `fly status`: exactly one machine.

⚠ **Regions get deprecated.** `den` (Denver) is retired — Fly builds the image, pushes it, and only
then refuses to create the machine, so this failure arrives late and looks like a deploy bug rather
than a config one. `fly platform regions` lists what is live; as of this writing the US options are
`iad`, `ord`, `dfw`, `lax`, `sjc`, `ewr`.

Check it:

```bash
fly logs                                    # expect "Salem 1692 server listening on port 8080"
curl https://<your-app>.fly.dev/health      # {"status":"ok"}
```

⚠️ **`CORS_ORIGIN` is not set yet, and cannot be** — it is the Netlify URL, which does not exist
until step 2. The server logs a warning at boot saying every browser will be refused. That is
expected here; step 3 fixes it.

**Two settings in `fly.toml` that look like waste and are not:**
- `auto_stop_machines = false` — scale-to-zero would discard the in-memory rooms of a game in
  progress. A cold start costs a slow request in a normal web app; here it costs the session.
- One machine only. Socket.io rooms are per-process, so a second instance means a phone can land on
  a different machine than its host and never see the game. Horizontal scaling needs the Redis
  adapter first (see the end of this document).

---

## 2. Web client → Netlify

Connect the repo in the Netlify UI and let it read [`netlify.toml`](../netlify.toml) from the REPO
ROOT (base `webclient`, build `npm run build`, publish `dist`) — you should not have to type any
of those into the UI. If Netlify asks you for a build command or publish directory, it did not find
the file: check that it is at the root and not inside `webclient/`, because Netlify reads a
subdirectory copy only after a base directory has already been set by hand.

Then set the one required variable —
**Site configuration → Environment variables**:

```
VITE_SERVER_URL = https://<your-app>.fly.dev
```

⚠️ **`VITE_*` variables are baked in at BUILD time, not read at runtime.** Changing this later
requires a redeploy, not a restart. Use `https://`, not `wss://` — socket.io derives the WebSocket
URL from the origin itself.

Deploy, then confirm the rewrite is live — this is the single most important check on the page:

```bash
curl -o /dev/null -w "%{http_code}\n" https://<site>.netlify.app/join     # must be 200, NOT 404
```

**Why it matters more than it looks.** `/join` and `/display` are client-side routes. Without the
`netlify.toml` rewrite, Netlify looks for a *file* called `join`, finds none, and 404s — breaking the
two URLs printed on the host screen for players to type. It also breaks **reconnection**: a phone
that reloads would hit a 404 instead of reclaiming its seat. None of this shows up in development,
because Vite's dev server rewrites for you.

---

## 3. Point the relay at the client

```bash
fly secrets set CORS_ORIGIN=https://<site>.netlify.app
```

This restarts the machine. Verify the boot warning is gone:

```bash
fly logs   # "listening on port 8080 (CORS origin: https://<site>.netlify.app)"
```

**Symptom if this is wrong:** the page loads, the join form works, and the connection never
establishes — often with nothing but a CORS error in the browser console. No exact origin match
(scheme, host, no trailing slash) means no connection. A custom domain later is another
`fly secrets set`.

---

## 4. Unity host → point it at the relay

The `NetworkManager.serverUrl` Inspector field is only a **fallback**; a built host reads, in order:

1. `-server wss://<your-app>.fly.dev` on the command line
2. the `SALEM_SERVER_URL` environment variable
3. the Inspector value baked into the scene

```bash
Salem1692.exe -server wss://<your-app>.fly.dev
```

Use **`wss://`**, not `ws://` — Fly terminates TLS and a plaintext socket is refused. The host logs a
warning if you point `ws://` at anything non-local, because that failure otherwise surfaces as a bare
"connection failed" with nothing naming the scheme.

For **Editor** play against production, set the Inspector field or the environment variable. The
override exists so the same build can move between dev and prod without a rebuild.

Build: **File → Build Settings → Windows standalone**. A shortcut with the `-server` argument in its
Target field is the tidiest way to ship it to a non-technical host operator.

---

## 5. Verify end to end

In this order — each step rules out a whole layer:

1. `curl https://<app>.fly.dev/health` → relay is up
2. `curl -o /dev/null -w "%{http_code}" https://<site>.netlify.app/join` → **200**, rewrite works
3. Unity host connects and shows a room code → the host↔relay leg and TLS are right
4. One phone joins over **cellular data, not wifi** → the genuinely-remote path, which is the whole
   point of deploying. A phone on the same LAN can pass while the internet path is broken.
5. `/display` on a second device → mirror parity over the internet
6. **Lock the phone, wait, unlock it** → reconnection under real network conditions, which is the
   feature most likely to behave differently in the wild than on a LAN

---

## Known limits (not blockers — things to know before they surprise you)

| Limit | Consequence | When it matters |
|---|---|---|
| Rooms are in memory | A deploy or crash ends every live game | Deploy between sessions; there is no session recovery |
| Single machine | No horizontal scaling | ~50 concurrent games on shared-cpu-1x per the options doc; needs the socket.io Redis adapter before a second instance can exist |
| No rate limiting | `create_room` is unauthenticated | Abuse would show up as room churn; add a per-IP cap if the URL ever goes public |
| 4-letter room codes | 331,776 combinations, no lockout | A determined stranger could brute-force into a *running* game. Fine for private play; revisit before any public listing |

None of these blocks the rollout in `deployment-options.md`. They are the honest edges of a relay
built for a living-room game, written down so the first production surprise is not a discovery.
