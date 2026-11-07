# Versioning & Release

One version number, stamped onto all three tiers from a single file, and visible in each of them.

## The number

`VERSION` at the repo root is the **only** place a version is authored. Semver,
`MAJOR.MINOR.PATCH`:

| Bump | When |
|---|---|
| **PATCH** | fixes and copy changes that cannot break an existing host build |
| **MINOR** | new features; any protocol ADDITION (a new event, a new optional field) |
| **MAJOR** | anything that makes an older host build fail against the live relay or web client — a removed or renamed event, a changed payload shape, a changed rule the two tiers must agree on |

🔴 **The MAJOR rule is the one that matters here, and it is not theoretical.** Salem's three pieces
update on *different schedules*:

| Tier | How it updates | How stale can it get? |
|---|---|---|
| Web client (Netlify) | automatically, on every push to `dev` | never — always newest |
| Relay (Fly.io) | only when someone runs `fly deploy` | days or weeks |
| **Unity host (Steam)** | **only when a player downloads a new build** | **months** |

So a host from last spring can talk to a relay and a phone client from this morning. That is normal
operation, not an error case — which is why every tier shows its own version rather than assuming
they match.

## Stamping a version

```bash
node scripts/set-version.mjs 1.1.0     # set VERSION and apply it everywhere
node scripts/set-version.mjs           # re-apply the current VERSION
node scripts/set-version.mjs --check   # report drift, change nothing
```

It writes three places:

- `webclient/package.json` → baked into the bundle by Vite's `define` → `src/version.ts`
- `server/package.json` → reported by `GET /health`
- `ProjectSettings.asset` `bundleVersion` → `Application.version` in Unity

⚠️ **Close the Unity Editor first.** It holds `ProjectSettings.asset` in memory and rewrites it on
exit, so an edit made underneath a running Editor is silently reverted. The script refuses that tier
while Unity is running rather than appearing to succeed — if it stops with that message, the two JS
tiers were still updated, so re-run it after closing Unity.

## Where the version shows

| Tier | Where |
|---|---|
| Unity host | faded `v1.0.0`, bottom-right of every host screen (`HostVersionStamp`) |
| Phones / displays | footer of `/join` and `/display` |
| Relay | `curl https://salem-1692-relay.fly.dev/health` → `{"status":"ok","version":"1.0.0"}` |

The host stamp is **not** mirrored, deliberately: it carries no game information, so it sits outside
the host/mirror parity rule. The web client shows *its own* version instead — and when those two
numbers differ, that difference is the thing worth seeing.

## Releasing

```bash
node scripts/set-version.mjs 1.1.0
node scripts/set-version.mjs --check        # all three agree
cd server && npx jest --runInBand
cd ../webclient && node ./node_modules/vitest/vitest.mjs run --pool=threads --poolOptions.threads.singleThread
git commit -am "Release v1.1.0"
git tag -a v1.1.0 -m "v1.1.0"
git push && git push --tags
```

Then, in whatever order suits the change:

1. **Relay** — `cd server && fly deploy --config fly.toml --app salem-1692-relay --ha=false`
   (the `--ha=false` is required on every deploy; see the runbook for why)
2. **Web client** — deploys itself from `dev`; confirm the footer shows the new version
3. **Unity host** — build, then upload to Steam

⚠️ **Deploying the relay ends every game in progress** (rooms are in memory). Release between
sessions, never during one.

### What the tag buys you

`git tag` is what makes a build reproducible after the fact. A Steam build that someone reports a bug
in is identifiable only if its version maps to a commit — `git checkout v1.0.0` has to give you the
source that produced it. That is also why `VERSION` is committed rather than generated: the tag and
the file agree, and both are in the history.

## The compatibility check (built)

A separate `protocolVersion` integer — currently **1** — travels with every join. The relay records
the HOST's at `create_room` and refuses any phone or mirror that disagrees, naming which side is
behind. Full contract in [protocol.md](protocol.md) → *Protocol version*.

**It is deliberately not the semver above.** `VERSION` describes a release; `protocolVersion`
describes the wire, and moves only when an older build genuinely cannot talk to a newer one. Most
releases will never touch it.

🔴 **Bumping it breaks every host build already in players' hands, on purpose.** That is the whole
point: a stale host that connects fine and then mis-renders a seat or drops a prompt is far worse
than one that says "the host needs to update". Bump it only alongside a MAJOR version, and only when
an old host truly cannot work against the new contract. Changing all three constants
(`server/src/protocol.js`, `webclient/src/socket/protocol.ts`,
`Assets/Project/Scripts/Networking/Protocol.cs`) is the deliberate friction that makes you stop and
consider that.

⚠️ **A missing `protocolVersion` counts as 1**, so builds shipped before the check keep working. That
allowance is for protocol 1 only — do not extend it.
