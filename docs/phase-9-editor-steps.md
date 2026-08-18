# Phase 9 — Editor Steps

Companion to `phase-7-editor-steps.md`. Same scene: **`Assets/Project/Scenes/Game/Networked_Game.unity`**.
Do NOT change `Sandbox_Testing`.

⚠️ Read the **Troubleshooting** section of `phase-7-editor-steps.md` before starting — every hazard
listed there (zero localScale, nested Canvases, Control-vs-Force-Expand, preferred-vs-flexible) still
applies.

---

## Stage A — Audio

Two components, both self-driving. Neither needs a reference from any other script: they subscribe to
the public state/event feeds themselves, which is what makes the dawn/night bed automatic as the
guide requires.

### A.1 — The GameObject

1. In the Hierarchy, right-click the scene root → `Create Empty`. Name it **`Audio`**.
2. Keep it at the scene root, NOT under `HostDisplayCanvas`. It renders nothing, and parenting audio
   under a Canvas invites the nested-Canvas trap for no benefit.

### A.2 — `HostAudioManager` (cues + day bed)

**One AudioSource per GameObject.** Two sources on the same object are indistinguishable in the
Inspector, and swapping them makes every one-shot cue loop forever. Giving the ambience its own child
removes the ambiguity entirely:

```
Audio                 ← HostAudioManager + AudioSource (cues, auto-added)
├── DayAmbience       ← AudioSource (loop)
└── PhaseAmbience     ← AudioSource (loop) + HostPhaseAmbience
```

1. Select `Audio` → `Add Component` → **`Host Audio Manager`**.
   It has `[RequireComponent(typeof(AudioSource))]`, so Unity adds the **AudioSource** for cues
   automatically. Leave its `Play On Awake` unticked (the script clears it anyway).
2. Right-click `Audio` → `Create Empty` child → name **`DayAmbience`** → `Add Component` →
   **`Audio Source`**. Set **Loop ✔**, **Play On Awake ✘**.
3. On `Host Audio Manager`, wire:

| Field | Value |
|---|---|
| `cueSource` | the AudioSource on `Audio` itself |
| `masterVolume` | `1` |
| `ambienceSource` | the AudioSource on `DayAmbience` |
| `dayAmbience` | looping day bed clip (leave empty until sourced) |
| `ambienceFadeSeconds` | `1.5` |
| `ambienceVolume` | `0.35` |

### A.3 — Cue list

Expand `cues` on `Host Audio Manager` and set **Size** to the number of rows you want. Matching is
**top-to-bottom, first match wins**, so filtered rows MUST sit above their unfiltered fallback.

| # | `kind` | `valueFilter` | `cardNameFilter` | Fires on |
|---|---|---|---|---|
| 0 | `card_played` | *(empty)* | `Accusation` | an accusation placed |
| 1 | `card_played` | *(empty)* | `Evidence` | evidence placed |
| 2 | `card_played` | *(empty)* | `Witness` | a witness called |
| 3 | `card_played` | *(empty)* | *(empty)* | any other card — **must be last of the four** |
| 4 | `cards_drawn` | *(empty)* | *(empty)* | a player takes Draw 2 |
| 5 | `tryal_revealed` | *(empty)* | *(empty)* | a tryal turns |
| 6 | `player_eliminated` | *(empty)* | *(empty)* | someone is hanged |
| 7 | `phase_changed` | `Night` | *(empty)* | night begins |
| 8 | `game_over` | `Witches` | *(empty)* | witches win |
| 9 | `game_over` | `Villagers` | *(empty)* | townspeople win |

🔴 **The filter strings are case-insensitive but must otherwise match exactly.** They are the raw
enum names Unity emits: `GamePhase.ToString()` (`Dawn`/`Day`/`Night`) and `Team.ToString()`
(`Villagers`/`Witches`/`Draw`). It is **`Villagers`, not `Townspeople`** — the latter reads naturally
and matches nothing.

Set each row's `clip` and `volume`.

🔴 **`volume` DOES NOT default to 1 — you must set all ten by hand.** The `Cue` class declares
`= 1f`, but Unity **ignores C# field initializers for array elements created by typing a new Size**
and zero-fills instead. A row left at 0 is silent, which reads as a broken audio system rather than a
muted row. This caught us once already.

⚠ **Row 7's filter goes in `valueFilter`, not `cardNameFilter`** — it is the only phase row, and the
two fields sit adjacent in the Inspector. Left empty it matches EVERY phase change, so the
night sting would also fire at dawn, day and conspiracy.

**An empty clip is silently skipped**, so it is safe to add all ten rows now and fill clips in as you
source them.

### A.4 — `HostPhaseAmbience` (dawn/night bed)

1. Right-click `Audio` → `Create Empty` child. Name it **`PhaseAmbience`** (sibling of `DayAmbience`,
   again so it owns exactly one AudioSource).
2. `Add Component` → **`Audio Source`**: **Loop ✔**, **Play On Awake ✘**.
3. `Add Component` → **`Host Phase Ambience`**, then wire:

| Field | Value |
|---|---|
| `source` | that AudioSource |
| `dawnClip` | dawn bed (leave empty until sourced) |
| `nightClip` | night bed (leave empty until sourced) |
| `volume` | `0.5` |
| `fadeSeconds` | `2` |

No other wiring. It arms itself off the public `phase` string — the same single source of truth
`HostPhaseOverlay` uses, so the cover and the sound cannot disagree about the phase.

### A.5 — Verify

- Play a turn: taking **Draw 2** should fire cue 4, and playing an **Accusation** cue 0.
- Entering **Night**: cue 7 fires once, the day bed fades out over ~1.5s, the night bed fades in over
  ~2s. Leaving night reverses it.
- ⚠ **Dawn and night are SILENT until clips are assigned** — that is a missing asset, not a bug, and
  explicitly **not** a masking failure (see CLAUDE.md: every player is prompted identically, so there
  is no differential movement for audio to cover).
- The log should now show a game-over line at the end. If it does not, the `game_over` `value` fix
  did not take — that entry was previously dropped because the emitter sent a type name.

---

## Stage A2 — Lobby pace control (timer lengths)

Adds one more control to `HostLobbyPanel` (see `phase-7-editor-steps.md` for the rest of that panel).

1. Under `LobbyPanel/Content`, add `GameObject ▸ UI ▸ Button - TextMeshPro`. Name it **`PaceButton`**.
2. Add `Layout Element`: **Preferred Height 48**.
3. Wire on `HostLobbyPanel`: `paceButton` → `PaceButton`, `paceText` → its child `Text (TMP)`.
   Leave `paceFormat` as `PACE: {0} ({1}x)`.

It cycles **Normal (1×) → Relaxed (1.5×) → Extended (2×)** and disables itself once the game starts.

🔴 **One global multiplier, never per-player.** `TimerSettings` scales EVERY player-facing deadline
together — secret-phase windows, the Day idle timer, Tituba's rearrange, and each mid-turn prompt.
Phase 4c fixed a real timing leak by making secret-phase windows a SHARED deadline; a per-player or
per-accessibility-profile timer would reintroduce it. The individual windows are also balanced
against each other (every prompt sits under the Day idle timer), so scaling them together preserves
those relationships where editing one could let a prompt outlive the turn that owns it.

The `revealLeadSeconds` countdown is deliberately **not** scaled — it is a reveal lead-in nobody
interacts with, and it is the value host and mirrors both schedule against.

The pace **locks when the game begins** and resets with each new lobby (`TimerSettings` is static and
would otherwise carry the previous game's pace, and its lock, into the next one).

---

## Stage B — Colourblind audit (findings)

The Phase 9 requirement is "icons + color, never color alone". Audited across the host display and
the web client; **no Editor work is needed** — the fixes were all in webclient code.

**Already safe, and worth knowing why so nobody "fixes" them:**

| Surface | Non-colour carrier |
|---|---|
| Host accusation counter | plain text `ACCUSATIONS n/7` — the Phase-7 decision to retire `HostPip.prefab` removed the only colour-coded indicator |
| Host IN EFFECT rows | the card NAME and rules text; the accent bar is decorative |
| Host seat effect badges | the card name is written on the badge (`label.text = statusCards[i]`) |
| Host HANGED overlay | the word "HANGED", plus crossed bars |
| Host active turn | the ember ring is a SHAPE — present vs absent, not a hue judgement |
| Host tryals | card ART distinguishes Witch / Not a Witch, and the legend labels each |
| Phone board | `⚖ n`, `line-through` for eliminated, a `● turn` label |
| Phone tryals | the label text plus a `(revealed)` suffix |

**Three real findings, all fixed** — every one was a SELECTION state signalled by hue alone, on
screens where the choice is confirmed under a host-owned countdown:

- `PlayerTargetList` (target choice + the secret-phase screens) — added `aria-pressed`, a **✓**, and
  a thicker border. It had no accessible state at all before, so it was invisible to screen readers
  as well.
- `HandList` — added `aria-pressed` and **✓** for the selected card; an unplayable card now shows
  **—** and the words "can't play" rather than only being greyed.
- `TryalPickScreen` — the chosen face-down back shows **✓** instead of **?**, with a heavier border.

Locked by `colourIndependence.test.tsx`, which asserts text and ARIA rather than class names — a
class assertion would still pass if the difference were swapped for another colour.

---

## Known asset gap

The project ships two clips, both `shuffle.wav` (`Assets/Project/Audio/Sound Effects/`). Every cue
above needs sourcing. The system is designed to be wired now and populated later; nothing errors on a
null clip.
