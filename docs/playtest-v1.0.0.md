# Playtest script — v1.0.0

A single session that verifies everything added since the last tested build **and** closes the
Phase 10 items that only a live game can close. Ordered so one sitting covers the most ground.

Record results inline — an unticked box is more useful than a remembered one.

---

## 0. Before you start

🔴 **Make a NEW build.** The build you tested predates four commits, three of which change the host:

| Added since your build | Where you'll see it |
|---|---|
| Lobby URL captions | lobby, above each address |
| Version stamp | bottom-right of every host screen |
| `bundleVersion 1.0.0` | that stamp should read **v1.0.0** |
| Protocol version | invisible unless it refuses someone — §5 |

Build: open `Networked_Game`, **File → Build Profiles → Build**, output to `Builds/Windows/`.
The whole folder travels together; the `.exe` alone will not start.

Stack is live and already verified: relay `1.0.0`, `/join` and `/display` both 200.

**Have ready:** the host PC, 2+ phones, and one device on **cellular data, not wifi** — a phone on
your LAN can pass while the internet path is broken.

---

## 1. Lobby (new this build)

- [ ] Version stamp reads **v1.0.0**, bottom-right, faded
- [ ] Join URL carries **EVERY PLAYER OPENS THIS — PHONE OR TABLET** above it
- [ ] Display URL carries **OPTIONAL SECOND SCREEN — SMART TV OR LAPTOP**
- [ ] Captions are legible from across the room and the **address is still the biggest thing**
- [ ] Both addresses point at `salem-1692.netlify.app`, not a LAN IP

⚠️ If a caption is cut off or overlaps, say so — the labels allow overflow rather than clipping, so
it would show as collision rather than missing text.

**On a phone, open `/display` by mistake on purpose:**
- [ ] It says it is a shared screen and **never your own cards**
- [ ] "Join the game on your phone" takes you to `/join`
- [ ] Footer shows **v1.0.0**

---

## 2. Join, over the real internet

- [ ] One phone joins over **cellular**, not wifi
- [ ] A second phone joins over wifi
- [ ] `/display` on a TV or laptop shows the board
- [ ] **Two players enter the SAME display name** → the board disambiguates them ("Name (2)")
      *(Phase 10 item 18)*

---

## 3. Play a full game

Covers Phase 10 items 1–4 and the reconnection edges.

- [ ] Host and mirror stay in step; a reveal fires on both at the same moment *(items 1, 2)*
- [ ] During the night vote, a witch phone and a non-witch phone look **identical** — screens AND
      the moment each becomes interactive *(items 3, 4)*
- [ ] **Lock a phone mid-game, wait, unlock it** → back in its own seat with board and cards
- [ ] **Lock a phone while it is holding a prompt** (target or confess) → the prompt **reappears**
      *(the one reconnection path never confirmed live — `ResendPendingPrompt`)*

**Timeouts** — the slow ones, worth doing once:
- [ ] Witch does nothing for 45s → a random target is chosen *(item 5)*
- [ ] Constable does nothing → no gavel placed, game continues *(item 6)*

⚠️ The window is **not** shortened by everyone else finishing — the phase waits for every connected
human by design. The full 45s is correct behaviour, not a hang. Use the lobby pace control to
shorten it if that is tedious.

**If a Tituba is in play:**
- [ ] Rearrange the deck, put **Night** on top, end turn, draw → night triggers *(item 15)*

---

## 4. End of game (new this build)

- [ ] A winner panel appears on the TV naming the winning side
- [ ] **NEW GAME** returns to the lobby with a **different room code**
- [ ] Every phone lands back on the join screen by itself
- [ ] **Press Escape after restarting** → the menu still opens
      *(this is the bug that was fixed — the menu used to be dead for the rest of the session)*
- [ ] Mid-game, **Escape** offers KEEP PLAYING / RESTART / QUIT
- [ ] In the lobby, **Escape** offers only KEEP PLAYING / QUIT (no RESTART)
- [ ] **QUIT** closes the application

---

## 5. Protocol gate (optional, 2 minutes)

Already verified against the live relay, so only worth repeating if you want to see it:

- [ ] With the host running, open the browser console on a joined phone and reload — it should
      rejoin normally (protocol 1 on both sides)

The refusal path cannot be triggered by hand until a future build bumps the number. When that
happens, an older host will show the joining player: *"This game's host is running an older version
of Salem. The host needs to update."*

---

## 6. Harness items — needs 3 Inspector fields first

These five Phase 10 items cannot be reached by normal play. Wire `TestManager` once (one minute) and
they become two-minute checks each. **TEMP — Phase 10 seat setup:**

| Field | Value |
|---|---|
| `Debug Tryal Templates` | size 3, in `GameSetup`'s order: 0 Constable, 1 Witch, 2 Not a Witch |
| `Debug Accusation Card` | Accusation 1 (Red Cards) |
| `Debug Status Card` | Piety 1 (Blue Cards) |

Then right-click the component header in Play mode:

- [ ] **Item 10** — spec `W,W,N`, Reveal index 0 → announcement, **not** eliminated; reveal index 1 → eliminated
- [ ] **Item 9** — spec `W,C` → that phone shows BOTH roles; gavel works until the constable tryal turns
- [ ] **Item 12** — Add Piety, Add Accusations 7, then Curse the Piety away → immediate reveal, remover chooses
- [ ] **Item 8** — all seats `W` except one `N`; give the `N` seat a witch tryal → witches win and **that player is excluded**
- [ ] **Item 7** — seats `W` / `C` / `N` / `N`, run each ending; **Dump Win State** before and after

⚠️ `IsWitch` is sticky by rulebook, so re-dealing a row does **not** clear a witch role — restart for
a clean seat.

Also worth one run while the harness is wired:
- [ ] **Black Cat drawn into hand** — Curse it off a holder, exhaust the deck so it re-forms, draw
      it, play it on someone, then draw Conspiracy and confirm step 1 reveals a tryal of the **new**
      holder *(the owner-decided rules change, compile-verified only)*

---

## Recording what you find

For anything that fails, the useful detail is **which tier complained**: the Unity console, the
phone's browser console, or `fly logs`. A symptom plus its tier is usually enough to find the cause;
a symptom alone often is not.

Update `docs/phase-10-test-plan.md` as items pass — and per the rule at the top of that file, only
promote an item to ✅ **playtested** with a date and a note on what was actually seen.
