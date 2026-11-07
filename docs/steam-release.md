# Steam release

Uploading a build to the **main** app and the **Playtest** app with SteamPipe.

The scripts live in `steam/` and are committed, so an upload is a repeatable command rather than a
remembered sequence of clicks.

---

## One-time setup

**1. The IDs are filled in** and committed:

| File | App | Depot |
|---|---|---|
| `steam/app_build_main.vdf` | **5409770** (main) | 5409771 |
| `steam/app_build_playtest.vdf` | **5526150** (Playtest) | 5526151 |

🔴 **The Playtest is a separate app**, created by Steam when you enable Playtest on the store page —
it has its own App ID and its own depot. Putting the main App ID in the playtest script would ship a
playtest build to everyone who owns the game.

**2. steamcmd** — already present, inside the Steamworks SDK:

```
D:\steamworks_sdk_165\sdk\tools\ContentBuilder\builder\steamcmd.exe
```

Verified it launches and self-updates.

**3. A builder account.** Use a Steamworks account with *Edit App Metadata* / *Publish* rights, not
your personal account. Steam Guard will prompt on first login from a new machine; log in once
interactively so the token is cached:

```
steamcmd +login <builder-account> +quit
```

---

## Uploading

Build first (**File → Build Profiles → Build** into `D:\Test Builds`), then:

**PowerShell** (the default terminal — note the leading `&`, which is how PowerShell runs a quoted
executable path):

```powershell
& "D:\steamworks_sdk_165\sdk\tools\ContentBuilder\builder\steamcmd.exe" +login ughsmash_games `
    +run_app_build "D:\GitHub\Salem-1692\steam\app_build_playtest.vdf" +quit

& "D:\steamworks_sdk_165\sdk\tools\ContentBuilder\builder\steamcmd.exe" +login ughsmash_games `
    +run_app_build "D:\GitHub\Salem-1692\steam\app_build_main.vdf" +quit
```

**cmd.exe**, if you prefer it:

```bat
"D:\steamworks_sdk_165\sdk\tools\ContentBuilder\builder\steamcmd.exe" +login ughsmash_games ^
    +run_app_build "D:\GitHub\Salem-1692\steam\app_build_playtest.vdf" +quit
```

⚠️ `set SC=...` and `%SC%` are **cmd syntax** and silently do nothing in PowerShell — the variable
is never set and `%SC%` is passed through as a literal. Hence the full paths above.

Both point at the **same** content root — `D:\Test Builds` — because the playtest and the main
game are the same binary, just different Steam apps.

⚠️ **The content root is OUTSIDE the repo.** `D:\Test Builds` is whatever was last built
there, so a git checkout alone does not reproduce a store build. Check the version stamp in the
corner of the host screen matches the tag you are shipping, before uploading.

⚠️ **Paths in a VDF are relative to steamcmd, not to the VDF.** Absolute paths, as above, avoid the
most common SteamPipe mistake: an upload that succeeds against the wrong folder.

### Then set it live — deliberately, in the web UI

Both scripts have `"setlive" ""` **on purpose**. A value there pushes the build live the instant the
upload finishes. Instead:

**Steamworks → your app → Builds → select the build → set the branch → Publish.**

That extra click is the only thing between a bad build and everyone's library.

---

## What is excluded, and why it matters

Unity emits a folder next to every build literally named
`Salem 1692_BurstDebugInformation_DoNotShip`. The depot config excludes it, along with `*.pdb` and
`*_BackUpThisFolder_ButDontShipItWithYourGame*`.

Verified against the actual build in `D:\Test Builds`: the exclusion matches that folder, and what
remains is exactly `Salem 1692.exe`, `Salem 1692_Data`, `UnityPlayer.dll`, `UnityCrashHandler64.exe`,
`MonoBleedingEdge` and `D3D12` — 111 MB.

⚠️ **The whole folder ships together.** The `.exe` alone will not start; `Salem 1692_Data` is not
optional. SteamPipe handles this correctly by uploading the content root recursively — this matters
when someone zips a build by hand instead.

---

## Steamworks app configuration (once)

- **Launch option** — executable `Salem 1692.exe`, OS Windows. Without one, Steam installs the depot
  and has nothing to run.
- **Playtest** — enable it on the main app's store page; Steam creates the Playtest app and its
  depot. Access is granted per-user from the Playtest tab.
- The game needs **no Steamworks SDK integration**: no achievements, no overlay hooks, no Steam
  networking. It is a standalone binary that happens to be distributed through Steam, and the host
  reaches the relay over plain HTTPS/WSS.

---

## Version discipline

The build's version comes from `VERSION` via `scripts/set-version.mjs`, shows bottom-right on the
host screen, and should match the git tag:

```bash
node scripts/set-version.mjs 1.0.1
# build, test
git tag -a v1.0.1 -m "v1.0.1" && git push && git push --tags
```

Update `desc` in both VDFs to match, so the Steamworks build list maps one-to-one to a tag. A bug
report that says "v1.0.1" should lead to exactly one commit.

🔴 **A host build is the one tier players control the update of.** If a release bumps
`protocolVersion` (see protocol.md), every older host build stops being able to host — intentionally.
Ship those releases to the Playtest branch first and confirm the refusal message reads sensibly
before promoting to main.

---

## Checklist per release

- [ ] `node scripts/set-version.mjs <version>` — all three tiers agree
- [ ] Tests green: server `npx jest --runInBand`, webclient vitest single-thread
- [ ] Unity build into `D:\Test Builds`, launched once and verified
- [ ] `desc` updated in both VDFs
- [ ] Upload to **Playtest** first, set live, play one game
- [ ] Upload to **main**, set live
- [ ] Relay deployed if it changed (`fly deploy … --ha=false`), between sessions
- [ ] `git tag` pushed
