# Phase 7 — Editor Steps

All code is complete and both suites are green. This is the Unity-side assembly.

**Scene: `Assets/Project/Scenes/Game/Networked_Game.unity`.** Do NOT change `Sandbox_Testing` — it
keeps the legacy `TableLayoutController`/`PlayerBoardUI` for local/AI dev, where masking is moot.

Design values below come from the locked design (`docs/host-screen-design.pdf` and the exported
prototype in `docs/salem-1692-host-ui-design/`). Palette:

| Name | Hex | Use |
|---|---|---|
| parchment | `E8DCC0` | body text, borders at low alpha |
| bright parchment | `F0E6CD` | headings, stat numbers |
| ember | `E6B268` | ACTIVE TURN ring |
| amber | `C98A3F` | accents, IN EFFECT left border |
| crimson | `A8231B` | ×N badge, HANGED border |
| hanged red | `E0463A` | the HANGED word |
| ground | `17130F` | panel background, badge ring |
| badge text | `F7EFDD` | ×N numerals |
| asylum blue | `2C4A7C` | Asylum effect pill |
| default effect | `7C2B23` | all other effect pills |

---

## Stage 1 — Fonts

The three TTFs are already at `Assets/Project/Art/Fonts/`. Unity imports them on focus; they still
need TMP font **assets**.

1. `Window ▸ TextMeshPro ▸ Font Asset Creator`
2. For each of the three:
   - **Source Font File:** `IMFellEnglishSC-Regular`, then `EBGaramond-Regular`, then `EBGaramond-Italic`
   - **Sampling Point Size:** Auto Sizing
   - **Padding:** 5
   - **Atlas Resolution:** 1024 × 1024
   - **Character Set:** ASCII (extend later only if player names need it)
   - **Render Mode:** SDFAA
   - Generate ▸ **Save as…** into `Assets/Project/Art/Fonts/`
3. ⚠ Use the **static** faces, not `EBGaramond-VariableFont_wght` — TMP's creator handles static
   faces far more reliably.

**Open decision — the monospace face.** The design uses `ui-monospace` for every small uppercase
label (`2 IN HAND · 1/5`, `ACCUSATIONS 1/7`, `WITCHES REVEALED`, log timestamps). That's a browser
system font with no Unity equivalent, and it was not in the download. Options:

- **(a)** Add a mono — *Courier Prime* suits the period and is a free Google font
- **(b)** Substitute EB Garamond with wide character spacing + all-caps on those labels
- **(c)** Use TMP's bundled LiberationSans for them — quickest, least characterful

Nothing else in these steps depends on this; pick when you see it on screen.

---

## Stage 2 — Sprite registry

1. Project window → `Assets/Project/Prefabs/Scriptable Objects/`
2. Right-click → `Create ▸ Card Game ▸ Host Card Sprite Registry`, name it `HostCardSpriteRegistry`
3. **Select that asset**, then run `Tools ▸ Salem ▸ Populate Host Card Sprite Registry`
4. Console should report roughly:
   `Populated "HostCardSpriteRegistry" with ~24 label→sprite entries. Face-down back: Salem_Tryal Cards Back.`

**Expect ~24 entries:** 3 tryals + 3 red + 4 blue + 6 green + 15 Town Hall, minus any lacking art.
Black cards (Night, Conspiracy) are skipped deliberately. The deluxe tryals cannot appear — no
`TryalCard` ScriptableObject references them, and the populator scans SOs, not image files.

5. **Hand-author the IN EFFECT rules text** on the blue entries (Town Hall descriptions auto-fill from
   the card's own rules text). At minimum:
   - `Asylum` → "Recipient cannot be eliminated during the night"
   - `Piety` → "Doubles the accusations needed to reveal a tryal"
   - `Matchmaker` → "If one linked player is eliminated, both are"
   - `Stocks` → "Skips this player's next turn"
   - `Black Cat` → "Its holder reveals a tryal when conspiracy is drawn"
6. **Set the `accent`** on `Asylum` to `2C4A7C`. Leave every other accent at alpha 0 — that means
   "use the caller's default", which is the `7C2B23` red.

⚠ Re-running the populator **preserves** hand-authored `description` and `accent` by label. It is
safe to re-run after adding card art.

---

## Stage 3 — `HostPlayerSeat` prefab (build fresh)

Create at `Assets/Project/Prefabs/HostDisplay/HostPlayerSeat.prefab`.

### 3.1 Root

- Empty UI GameObject, name `HostPlayerSeat`
- `Image` — colour `E8DCC0` at **alpha 10** (the `rgba(...,.04)` background)
- `CanvasGroup` → this is `seatGroup`
- `VerticalLayoutGroup` — Padding L12 R12 T11 B12, **Spacing 9**, Child Alignment Upper Center,
  Control Child Width ✔, Control Child Height ✘, Force Expand Width ✔
- `LayoutElement` — Preferred Height **148**
- Add `HostPlayerSeat.cs`

### 3.2 Row 1 — `PlayedRow`

- Child of root. `HorizontalLayoutGroup` — Spacing 4, Control Width ✔, Control Height ✘,
  Force Expand Width ✔
- `LayoutElement` — Preferred Height 40
- Five children named `Slot0`…`Slot4`, each:
  - `Filled` (child) — `Image`, this is the slot's **`image`**
    - `StackDecor` (child of Filled) — two thin offset `Image`s at `E8DCC0` alpha ~55/35, offset
      +2/−2 and +4/−4 px. This is **`stackedDecor`**
    - `CountBadge` (child of Filled) — `Image` circle `A8231B`, 19×19, anchored bottom-right,
      with a `TMP_Text` child (11px, colour `F7EFDD`, centre). The **TMP_Text** is **`countBadge`**
  - `Empty` (child) — `Image` with a dashed-border sprite (or flat `000000` alpha 45).
    This is **`emptyRoot`**; `Filled` is **`filledRoot`**

### 3.3 Row 2 — `IdentityRow`

- `HorizontalLayoutGroup` — Spacing 10, Control Width ✘, Control Height ✘, Child Alignment Upper Left
- Children:
  - `Portrait` — `Image`, `LayoutElement` Preferred Width ≈ 42, Height ≈ 59 (5:7). → **`portraitImage`**
  - `Names` — `VerticalLayoutGroup`, Spacing 1, `LayoutElement` Flexible Width 1:
    - `PlayerName` — TMP, **IM Fell English SC**, 14px, colour `E8DCC0`, Overflow **Ellipsis**,
      Wrapping off → **`nameText`**
    - `CharacterName` — TMP, EB Garamond, 9px, colour `E8DCC0` alpha ~110 → **`characterNameText`**
    - `Stats` — TMP, mono, 9px, colour `E8DCC0` alpha ~110, character spacing ~12 → **`statsText`**
    - `Accusations` — TMP, same style as Stats → **`accusationText`**

### 3.4 Row 3 — `TryalRow`

- `HorizontalLayoutGroup` — Spacing 4, Control Width ✔, Control Height ✘, Force Expand Width ✔
- `LayoutElement` — Preferred Height 59
- Five `Image` children `Tryal0`…`Tryal4` → the **`tryalSlots`** array, in order

### 3.5 Overlays (children of root, but OUTSIDE the layout flow)

Add `LayoutElement ▸ Ignore Layout ✔` to each, and stretch-anchor them to the root.

- `TurnRing` — `Image`, hollow/outlined sprite, colour `E6B268`, inset −3px.
  Add a `CanvasGroup`. → **`turnHighlight`** (the GameObject) and **`turnHighlightGroup`**
- `EliminatedOverlay` → **`eliminatedOverlay`**:
  - `Image` full-bleed, colour `080605` alpha ~210
  - Two thin `Image` bars, colour `A8231B` alpha ~128, rotated **+24°** and **−24°**
  - `HangedStamp` — TMP "HANGED", IM Fell English SC 30px, colour `E0463A`, rotated −9°,
    on an `Image` plate `140604` alpha ~184 with a crimson border
- `EffectBadges` — `HorizontalLayoutGroup`, Spacing 4, anchored top-left at roughly (10, +8)
  → **`effectBadgeContainer`**

### 3.6 Effect badge prefab

Separate prefab `HostEffectBadge.prefab`:
- Root `Image` — rounded pill, `ContentSizeFitter` Horizontal Preferred → this **root Image** is
  what `effectBadgePrefab` expects
- Child `TMP_Text` — 8px mono, colour `F0E6CD`, character spacing ~12, padding 3/7

### 3.7 Wire `HostPlayerSeat.cs`

Eighteen fields. `sprites` → the Stage-2 asset. `overflowText` may be left empty (it only fires with
more than five *distinct* card types, which is nearly unreachable). `effectBadgeDefault` is already
`7C2B23`.

---

## Stage 4 — Event log entry prefab

`Assets/Project/Prefabs/HostDisplay/HostEventLogEntry.prefab`:
- Root — `HorizontalLayoutGroup`, Spacing 10, Child Alignment Upper Left, Control Width ✔
- `Time` — TMP, mono 9px, colour `E8DCC0` alpha ~77, `LayoutElement` Preferred Width 34 → **`timeText`**
- `Body` — TMP, **EB Garamond** 13px, colour `E8DCC0` alpha ~204, Wrapping on,
  `LayoutElement` Flexible Width 1 → **`bodyText`**
- Add `HostEventLogEntry.cs` and wire both

---

## Stage 5 — Scene assembly (`Networked_Game`)

Under the host Canvas, build:

```
HostDisplay                     ← HostDisplayController
├── Board                       (Horizontal: left pane flex, right rail fixed 322)
│   ├── TablePane               (Vertical)      ← HostTableView
│   │   ├── Header                              ← HostHeader
│   │   ├── TopRow              (Horizontal, Control+ForceExpand Width)   → topRow
│   │   ├── MiddleBand          (Horizontal)
│   │   │   ├── LeftColumn      (Vertical, Control+ForceExpand Height)    → leftColumn
│   │   │   ├── CenterPanel     (Vertical)      ← HostDeckView
│   │   │   └── RightColumn     (Vertical, same as LeftColumn)            → rightColumn
│   │   └── BottomRow           (Horizontal, same as TopRow)              → bottomRow
│   └── EventRail               (Vertical, LayoutElement Preferred Width 322)
│       ├── InEffectPanel
│       └── EventLog            (ScrollRect + Content)   ← HostEventLog
└── SeatPool                    (inactive)                                → seatPool
```

- **MiddleBand flex ratios**, matching the design's `1fr / 2.1fr / 1fr`: LeftColumn and RightColumn
  `LayoutElement ▸ Flexible Width = 1`; CenterPanel `= 2.1`
- **`HostTableView`**: assign `seatPrefab`, `seatPool`, and the four containers
- **`HostEventLog`**: `entryContainer` = the ScrollRect's Content, `entryPrefab` = Stage 4,
  `maxEntries` 14
- **`HostDisplayController`**: assign `table`, `deck`, `header`, `eventLog`
- **`GameEventEmitter`**: add the component to the same GameObject as `NetworkStateBroadcaster`.
  It self-finds `GamePhaseManager` and `GameManager` in `Awake`, but assign them explicitly if they
  live in a scene loaded later

---

## Stage 6 — Legacy leftovers (in `Networked_Game` only)

1. **Disable the `Deck.prefab` instance.** Its Button is wired to a scene `DrawPileUI ▸
   TryDrawTwoCards(CurrentPlayer)` — an **unauthorized input path**: anyone at the host machine can
   force the current player's draw. `HostDeckView` replaces it with live counts and no buttons.
2. **Disable the `DiscardPile.prefab` instance** — static label, superseded.
3. **Disable the `Day_NightIndicator.prefab` instance** — model-fed. The phase is public so it isn't
   a leak, but `HostHeader` shows it and disabling keeps a public-only audit clean.
4. **Disable `TableLayoutController` and its `PlayerBoardUI` instances** in this scene. Leave them
   untouched in `Sandbox_Testing`.

---

## Stage 7 — Verification

1. **Boundary grep** — must return comment lines only:
   ```
   grep -nE "PlayerService|(^|[^\w])Player\b|TryalCard|HandManager|StatusCards" Assets/Project/Scripts/UI/HostDisplay/
   ```
2. **Ring counts.** With `NetworkGameCoordinator.fillWithAI` + `targetPlayerCount`, start at 4, 9,
   and 12 and confirm against the locked table: 4 → 1/1/1/1, 9 → 2/2/2/3, 12 → 4/2/4/2
   (top/right/bottom/left). 9 is the interesting one — it is where the sides step to 2.
3. **Tryal counts are 3 at 10–12 players, 5 at 4–7.** If a seat shows five slots in a 12-player game,
   something is reading a hardcoded 5 rather than `tryalTotal`.
4. **Live networked test** — outstanding since iteration 2 and never yet run. Play through a day
   turn, an accusation, a tryal reveal, and an elimination; watch the log fill.
5. **Confirm the log names PLAYERS, not characters**, and that no entry ever describes a night vote,
   a constable save, or a confession before it resolves.

---

## Troubleshooting — hard-won, in the order to check them

Real failures hit during assembly, cheapest first:

1. **`localScale = (0,0,0)`** — the nastiest, because *every other diagnostic reads healthy*:
   `rect.size` is correct, `activeInHierarchy` is true, `Image.enabled` is true, anchors/sizeDelta are
   right, the sprite is assigned. Scale does not affect `rect.size`, so logs lie to you. It also
   defeats the "tint it magenta" trick — magenta at zero scale is still nothing.
   **Caused by Alt+clicking an anchor preset** on some Unity versions. Check scale FIRST on anything
   invisible-but-apparently-correct, and check it on the object's ancestors too (scale multiplies down).
2. **Nested Canvases** — `GameObject ▸ UI ▸ Image` in Prefab Mode silently wraps each new element in
   its own Canvas + CanvasScaler + GraphicRaycaster when the prefab root has no Canvas. Breaks the
   layout chain and produces 1920×1080 boxes inside a 260px seat. Author with a temporary Canvas on
   the prefab root, delete it at the end.
3. **`ChildForceExpandWidth` defeating a fixed size** — force-expand sets EVERY child's flexible
   weight to at least 1, so a sibling with `preferredWidth` gets stretched anyway. Hit three times:
   `Board` vs `EventRail`, `IdentityRow` vs `Portrait`, `TopRow` vs the seat.
4. **`flexibleWidth: -1` vs `0`** — `-1` means "not set", so the layout GROUP's computed flexible
   value wins. A child deep in the tree with `flexibleWidth: 1` (here, `Names`) propagates all the way
   up and makes the whole seat stretchable. Set an explicit **0** to pin it; −1 will not do it.
5. **`ChildControlHeight` off** — the row's `preferredHeight` is ignored entirely and it keeps a raw
   sizeDelta height.
6. **A field typed as a component can't take a GameObject** — `TMP_Text countBadge` could never be
   wired to the badge's pill; it needed a separate `GameObject badgeRoot`. If a drag "won't take",
   check the field's declared type before assuming user error.
7. **Preferred vs Flexible width — the most consequential distinction in uGUI.**
   Preferred = "my natural size"; Flexible = "my share of the leftover"; −1/unticked = "defer to the
   group". Setting Preferred where you meant Flexible does NOT just resize the element — uGUI derives
   a wrapping text's preferred HEIGHT from its preferred WIDTH, so a bogus width silently produces a
   bogus height. Symptoms seen: `preferredWidth: 1` on a row → `Description` computed its height as
   if wrapped one character per line → a hugely tall box with correct-looking text at the top. And an
   unset preferred width on a fill element → the group reports the child's UNWRAPPED width, blows the
   row's width budget, and squeezes fixed siblings until their text overflows.
   **Recipe for a fill element beside fixed siblings: Preferred Width 0 (ticked) + Flexible Width 1.**
8. **A fixed `preferredHeight` on a container CRUSHES its children.** When content exceeds it, a
   layout group interpolates children from preferred down toward min — so rows overlap rather than
   overflow. If a panel should size to content, REMOVE its LayoutElement; give the neighbour that
   should absorb the slack `flexibleHeight: 1`, and a `minHeight` so it keeps a floor.
9. **A cross-axis bar needs to leave the layout.** An `AccentBar` with `flexibleHeight: 1` in an HLG
   with Force Expand Height off gets its *preferred* height — unset, i.e. **0**, so it is invisible.
   Anchor it out of the flow instead (Ignore Layout + left-stretch), which also removes it from the
   width budget.

## Known gaps at this point

- `GameStarted` and `ConfessionRevealed` are in the event vocabulary but **nothing emits them yet**.
  The renderer already handles both; they need hooks at setup completion and at the confess reveal.
- **Mirrors receive `game_event` but do not render it.** The TypeScript contract exists; the mirror
  log UI is separate work.
- The **full-screen reveal overlay** (stage 7d) is not built. Source card art is 176×264, so the
  design's 230×322 overlay would upscale ~1.3× — consider the prototype PNGs for that one element.
