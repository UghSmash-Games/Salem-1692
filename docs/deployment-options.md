# Deployment Options — Salem 1692

Reference for Phase 11 (Deployment). Covers hosting the Node.js relay server and the
webclient (phone/mirror). The Unity host has no ongoing hosting cost — it is a one-time
distributable build someone downloads and runs locally.

**Goal:** Salem 1692 is intended as a commercial product. Pricing below is chosen with
that in mind, not just for hobby/testing use.

**Staged rollout plan:** start on free tiers during development/testing, then move to
paid/commercial-permitted tiers before any money changes hands. Vercel's free tier
specifically requires this staging (see below); Netlify's does not.

---

## Server (the relay) — Fly.io recommended

The server is a lightweight Node.js + Socket.io relay: no game logic, no database, just
message-passing between the Unity host and connected clients. This profile — a
long-running, stateful WebSocket process — is exactly what Fly.io is built for.

| Platform | Realistic monthly cost | Notes |
|---|---|---|
| **Fly.io** ✅ | **~$2–4/month** (shared-cpu-1x, 256–512MB) | Purpose-built for long-running WebSocket servers. Billed per-second/metered — no free tier as of 2026 (2-hour/7-day trial only), but the cheapest fit for this workload by a clear margin. |
| Render | $7/month (Starter, always-on) | Free tier exists but **spins down after 15 min of inactivity** with a 30–60s cold start on reconnect — not viable for a live game join. Predictable flat pricing is the appeal if cost isn't the deciding factor. |
| Railway | $5/month (Hobby minimum) | General-purpose PaaS, simple "push code, get a URL" workflow. Real free tier was removed in 2023; current "Free" plan is $1/month with very limited resources. One tracked reliability incident (Dec 2025 EU region outage). |

**Recommendation: Fly.io.** Cheapest and architecturally the best match for a persistent
WebSocket relay. Setup is slightly more hands-on than Railway (CLI + `fly.toml` config)
but well worth it for a production commercial deployment.

---

## Web client (phone + mirror) — Netlify vs. Vercel

The webclient is a plain React SPA (Vite build) with no framework-specific hosting
dependency — no Next.js, no edge functions, no ISR. Both Netlify and Vercel are
equally valid technical fits; the deciding factor is commercial-use licensing.

| Platform | Free tier | Commercial use on free tier? | Paid tier |
|---|---|---|---|
| **Netlify** ✅ | 300 credits/month (~15GB bandwidth, ~20 deploys) | **Allowed, explicitly** | Pro: $20/month **flat per organization**, unlimited seats, 3,000 credits (scalable to 20,000) |
| Vercel | 100GB bandwidth, 1M requests | **Not allowed** — Hobby is personal/non-commercial only, defined broadly (covers any financial gain by anyone involved in building it) | Pro: $20/month **per seat** |

**Recommendation: Netlify.** The commercial-use permission on its free tier means you
can develop and soft-launch on Netlify Free without a mandatory "convert before revenue"
step — you only need to upgrade when you outgrow the credit allowance, not on a
licensing deadline. Vercel's Hobby tier legally requires migrating to Pro the moment
any money changes hands, which is exactly the staging step Netlify avoids.

Netlify's flat $20/month-per-org (vs. Vercel's $20/month-per-seat) is also cheaper the
moment a second person needs deploy access.

**Caveat:** Netlify's free-tier credit *rates* have changed a few times over the past
year (each credit now buys less bandwidth/compute than it used to) — described by one
tracker as "volatile." Not a blocker, just worth re-checking current allowances at
deploy time rather than assuming these numbers hold indefinitely.

---

## Full cost picture

| Stage | Server | Web client | Total |
|---|---|---|---|
| **Development / testing** | Fly.io (~$2–4/mo) | Netlify Free ($0) | **~$2–4/month** |
| **Commercial launch** | Fly.io (~$2–4/mo) | Netlify Pro ($20/mo flat) | **~$22–24/month** |

Netlify Free can technically be used commercially with no forced upgrade — the Pro
upgrade above is about outgrowing the credit allowance (traffic/bandwidth), not a
licensing requirement, unlike the Vercel equivalent.

---

## Unity host build

No ongoing hosting cost. Build via **File → Build Settings → Build** (Windows
standalone is the natural target for "a laptop/TV running the host screen"). The build
connects out to the deployed relay server URL — same as Play Mode does today, just
pointed at the production server address instead of `localhost`.

Screen-sharing the host window (Discord, Zoom, etc.) works for remote play with no
extra engineering, since the host is a real application window rather than a
server-rendered display.

---

## Suggested rollout order

1. Deploy the relay server to Fly.io. Confirm the existing test suites pass against it
   and Unity Editor Play Mode can connect (pointed at the new URL instead of
   `localhost:3000`).
2. Deploy the webclient to Netlify (free tier), pointed at the Fly.io server URL. Test
   joining from another device on the same network, then from a genuinely separate
   network (e.g. cellular data) for a true internet test.
3. Build Unity as a standalone application, pointed at the same server URL. Test it
   running as a build, not just in the Editor.
4. Full test: host on one network, players joining from their own homes/networks.
5. Before any commercial launch: move Netlify to Pro (only if the free credit allowance
   is insufficient) and confirm Fly.io resources are sized for real concurrent load.

*Note: pricing across all platforms in this document was current as of mid-to-late 2026
per web research at time of writing. Cloud hosting pricing changes frequently — re-verify
current terms before committing to a platform at actual deployment time.*
