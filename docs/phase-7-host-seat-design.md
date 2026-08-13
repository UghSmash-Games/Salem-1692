# Phase 7 — Host Screen Build Spec (LOCKED)

**Visual design is locked:** `docs/host-screen-design.pdf`. That PDF supersedes the earlier
prose-based seat design. This document is the build spec derived from it.

**Read note:** the PDF was analysed by text + coordinate extraction (poppler unavailable, so it was
never rendered). Text content and geometry are verified; graphical composition (portraits, badge
shapes, card art, colour) is taken from the design owner's description.

---

## 0. Locked decisions

1. **DISPLAY-ONLY.** The PDF's `ADVANCE ▸`, `RESET TABLE`, "click a seat", and "click a sealed Tryal
   to flip" are Claude Design prototype artifacts and are **NOT built**. Page 2's *"Try next: add the
   night phase…"* is the same class of artifact. Zero interactive controls on the host screen —
   consistent with stripping `DrawPileUI`'s draw button and `PlayerBoardUI`'s per-tryal buttons.
2. **Rectangular ring** replaces the ellipse.
3. **Build the "What Has Passed" event log.**
4. **No accusation pips** — the PDF's `ACCUSATIONS n/7` text counter replaces them. `HostPip.prefab`
   retires.
5. **`WITCHES REVEALED` counts revealed witch CARDS**, not players who are witches. (A player with two
   witch cards, one revealed, contributes 1 and stays alive. Cards is the only public reading.)
6. **Event log names the PLAYER**, not the character. The seat box shows the **player name** as the
   primary label with the **character name** as a secondary line.

---

## 1. Ring geometry

Verified from the PDF: seat name rows cluster at four bands — 4 seats (x≈131/392/653/914), 2 seats
(x≈130/918), 2 seats (x≈130/918), 4 seats. Side seats share the exact x of the outer top/bottom
columns, so it is a true rectangle with corners owned by the horizontal rows.

Caps: **max 4 per horizontal row, max 2 per vertical side** → 4+4+2+2 = 12 = the max player count.

```
s      = clamp(ceil((n - 6) / 2), 1, 2)   // seats per vertical side; LEFT ALWAYS == RIGHT
H      = n - 2s                           // seats across both horizontal rows
top    = floor(H / 2)
bottom = ceil(H / 2)                      // the odd extra goes to the BOTTOM row
```

| n | Top | Left | Right | Bottom | Tryals/player |
|---|---|---|---|---|---|
| 4 | 1 | 1 | 1 | 1 | 5 |
| 5 | 1 | 1 | 1 | **2** | 5 |
| 6 | 2 | 1 | 1 | 2 | 5 |
| 7 | 2 | 1 | 1 | **3** | 5 |
| 8 | 3 | 1 | 1 | 3 | 4 |
| 9 | 2 | 2 | 2 | **3** | 4 |
| 10 | 3 | 2 | 2 | 3 | 3 |
| 11 | 3 | 2 | 2 | **4** | 3 |
| 12 | 4 | 2 | 2 | 4 | 3 |

Properties: left always equals right (asymmetric sides read as broken; an uneven horizontal row reads
as natural). The odd extra goes to the bottom — nearest the viewer, wider base reads grounded. Sides
step to 2 at n=9, which keeps the ring as square as possible rather than maximising row width.

**Known discontinuity (inherent):** at the s=1→2 step (n=8→9) the top row goes 3→2, because moving two
seats from horizontal to vertical must shorten the horizontal rows. Unavoidable in any four-sided
ring; placed at 8→9 so the rounder shape holds across 9–12.

**Tryals per player** is from `GameSetup.TryalDistribution` (total ÷ player count): 5 at 4–7, 4 at
8–9, 3 at 10–12. ⚠️ The PDF shows `1/5` on a 12-seat board — placeholder mock data; 12 players is
**3** each. Seats must render from `tryalTotal`, never a hardcoded 5. Note the tryal row is *widest at
low player counts*, exactly when seats are largest.

### Center ("The Meeting House")

**Derived, not fixed.** Compute the center rect from the same geometry that places the seats (inner
edges of the four sides, minus a margin) so ring and center can never disagree. With s=1 the ring is
vertically shorter and a fixed-size center would overflow or float.

Contents in a vertical layout group so they compress gracefully: stats row
(`WITCHES REVEALED` / `TRYALS FLIPPED` / `STILL LIVING`), `DRAW · n` / `DISCARD · n`, the top discard
card, the `FACE DOWN / NOT A WITCH / WITCH` legend, and the `THE MEETING HOUSE` label.

---

## 2. Seat composition

Vertical order per the PDF (top → bottom):

1. **Accusation cards** — stacked by type with a **×N badge** (not one image per card)
2. **Portrait + player name + character name + stats** (`N IN HAND · X/Y`, `ACCUSATIONS n/7`)
3. **Tryal row** — face-up art for revealed, the shared back for the rest

### Prefab mapping

The duplicated `PlayerBoardUI.prefab` still supplies the expensive parts — a seat-sized card row with
layout groups, overflow text, a tryal row, a card-art slot, turn highlight, elimination overlay. The
**vertical arrangement is re-anchored** (~4 RectTransforms); the slots themselves survive.

| Source object | Action | New purpose |
|---|---|---|
| `PlayerBoardUI` (root) | **strip** `PlayerBoardUI.cs`; keep `CanvasGroup`+`Image` | Rename `HostPlayerSeat`; add `HostPlayerSeat.cs`; `CanvasGroup` → `seatGroup` |
| `TurnHighlighter` | keep | → `turnHighlight` |
| `TownHallCard` | **strip** `TownHallCardUI.cs`; keep `Image` | → `townHallImage` (doubles as the portrait) |
| `Header` | keep | name block |
| `Header/PlayerName` | keep | → `nameText` (**player** name, primary) |
| `Header/TurnIndicator` (+child) | **delete** | redundant with `turnHighlight` + `activeTurnScale` |
| `TrialArea` | keep, **re-anchor to bottom** | → `tryalContainer` |
| `AgainstArea` | **strip `_Archive/PlayerStatusUI.cs`**; keep `VerticalLayoutGroup` | accusation/status card area, **re-anchored to top** |
| `AgainstArea/AgainstRow` | keep | → `cardRowContainer` (×N stacks) |
| `AgainstArea/MoreCountTxt` | keep | → `overflowText` |
| `Interactions` (Button, its Text, `TargetHighlight`) | **DELETE ENTIRELY** | input paths on a display-only screen |
| `EliminationIndicator` (+`Indicator`, `Text`) | keep | → `eliminatedOverlay` + badge |

**New objects with no source counterpart:** `visualRoot` (insert between root and children so the
eliminated tip doesn't move the seat's table position), `characterNameText`, `handCountText`,
`tryalProgressText`, `accusationCountText`.

⚠️ **The landmine:** `AgainstArea` carries `_Archive/PlayerStatusUI.cs`. `_Archive/` **compiles** (no
asmdef, no `#if`), and that script binds `Player`, `TryalCard`, and `PlayerService`. Miss it and the
boundary is breached on day one by a script nobody thinks is running.

**Stripped total:** `PlayerBoardUI.cs`, `TownHallCardUI.cs`, `_Archive/PlayerStatusUI.cs`, `Button`.

---

## 3. `HostCardSpriteRegistry`

Built. Single shared `Back` sprite (a face-down tryal is rendered by repeating one image, so no code
path can select a sprite from an unrevealed card's identity — the host is never sent one). Lazy
normalized dictionary: `Trim().ToLowerInvariant()` with all whitespace stripped, so `"Not a Witch"`
(the wire label from `LabelFor`) and `"Not A Witch"` (the asset's `Card.Name`) both resolve — a real
mismatch that would otherwise blank all tryal art.

**Extension for this design:** add an optional `description` to `Entry`, for the IN EFFECT panel's
rules text. Populated from `TownHallCard.GetRulesText()` by the Editor utility; the ~5 blue cards
hand-authored. **Static copy, not game state — no new wire data.**

Populator lives in `Assets/Project/Scripts/Editor/` (Unity's special-folder rule → editor-only
assembly) because it must read `Card`/`TryalCard`, which would breach the HostDisplay grep.

---

## 4. Public payload

Landed in Step 1: `accusationCards`, `townHall`, `tryalTotal`, `revealedTryals`, plus `statusCards`
narrowed to non-Red. All four are used by this design.

### Still needed

```csharp
public int    handCount;   // NEW — cards held. COUNT ONLY, never names.
public string topDiscard;  // NEW — name of the top discard card (face-up at a table).
```

### Derived host-side — no new wire data

| Display | Derivation |
|---|---|
| Tryal progress `X/Y` | `revealedTryals.Length` / `tryalTotal` |
| `TRYALS FLIPPED` | Σ `revealedTryals.Length` |
| `STILL LIVING` | count of `!eliminated` |
| `WITCHES REVEALED` | count of `"Witch"` across all `revealedTryals` (**cards**, per decision 5) |
| IN EFFECT panel | card name + holder from `statusCards`; rules text from the registry |

---

## 5. Event log — "What Has Passed"

### The existing seam is NOT reusable

`Assets/Project/Scripts/UI/CardLogManager.cs` exists and works, but is **model-bound**: it subscribes
to `CardEffectManager.OnCardPlayed`, `Player.AccusationCountChanged`, `Player.TryalCardRevealed`,
`PlayerService.OnPlayerEliminated`, `TrialService.OnDoubleWitchRevealed` and reads `PlayerNameText`
and `card.TryalCardType`. It would breach the boundary immediately, and it has **no phase gating at
all** — it logs whatever fires.

Treat it as a reference for *which events matter*, not a base class. Same relationship as
`PlayerBoardUI`.

### The structural mechanism (not "be careful")

**1. Closed enum of event kinds with fixed schemas. No free text.**
The host emits `{ kind, actorId, targetId, cardName, atMs }`; the RENDERER maps kind + ids to copy.
Game code cannot pass a string, so no code path can emit prose carrying secret information.

There is no `night_vote_cast` kind, no `constable_saved` kind, no `witch_identity` kind. **The log
physically cannot express "Alice voted for Bob" because no such kind exists.** That is the guarantee.

**2. Only what the public DTO already carries.** Allowed kinds map to public state transitions:

```
game_started · phase_changed · card_played · accusation_added · tryal_revealed
player_eliminated · double_witch_revealed · confession_revealed · game_over
```

**Confession is the sharp edge, and it resolves cleanly:** there is no kind for "X confessed" during
the window. `confession_revealed` fires only at the synchronized `revealAt`, when the flip is already
public per the rulebook. Timing masking is preserved because the event does not exist until
resolution.

**3. `actorId` is carried** (decision 6) — who plays a card is public at a physical table. The
renderer resolves it to the **player** name via the existing public board, never the character name.

**4. Contract test** in the same file as the payload test: allow-list of kinds, plus an assertion that
no event carries a free-text field.

### Timestamps

The host stamps **epoch milliseconds** at emit; each renderer formats `HH:mm` in local time. Never
send `"19:04"` preformatted — that bakes in the host's locale and timezone and breaks a mirror in
another region. Same principle as `phase_resolve.revealAt`.

---

## 6. Survives vs replaced

**Survives** — all Step-1 DTO work and `BuildRevealedTryalLabels` + its contract test;
`HostDisplayController`; `HostCardSpriteRegistry` + populator; `HostPlayerSeat`'s `Bind`, pooling
pattern, `RenderTryals`, `RenderTownHall`, elimination tip/dim/veil, turn spotlight, Town Hall
caching; `HostDeckView` counts; the prefab-duplication approach and strip list.

**Replaced** — `HostTableView.Layout()` + `SeatScaleFor` (ellipse → ring); accusation pips (decision
4, `HostPip.prefab` retires); `RenderCardRow`'s per-card + `+N` model (→ group-by-label ×N stacks);
seat vertical arrangement.

**Net new** — ring geometry + derived center; event log; IN EFFECT panel; table stats row;
`handCount` + `topDiscard`; seat text fields (player name, character name, hand count, tryal progress,
accusation counter); top-discard visual on `HostDeckView`.

---

## 7. Privacy verdicts (design-time, per project standard)

### Step-1 fields — audited, no FAIL findings

| Field | Verdict |
|---|---|
| `townHall` | SAFE-WITH-CONDITION — printed name only; never from the draft pool |
| `tryalTotal` | SAFE — count only; never publish `unrevealedCount` |
| `revealedTryals` | SAFE-WITH-CONDITION — filter-then-map, order-insensitive, one helper + contract test |
| `accusationCards` | SAFE — already broadcast inside `statusCards`; this is a split, not a disclosure |

**`townHall` is PRINTED, not effective.** `GetEffectiveTownHallName()` resolves Martha Corey's copy
from already-public inputs (seat order, alive status, printed cards), so clients can derive it.
Broadcasting only the effective name would erase the public fact that the player *is* Martha Corey and
render the wrong card art. Carries **card identity only** — never charges, never eligibility.
Goes empty at elimination (`Player.OnElimination` nulls the card); the host caches the last non-empty
value rather than reaching for another source.

**`revealedTryals` shapes explicitly RULED OUT:** fixed-length array with `null`/`""` placeholders;
`{ label, index }`; `{ label, faceUp }` (a public mirror of the private `TryalViewMsg`).
`AddTryalCardAndNotify` **appends**, so after a Conspiracy pass the last slot is always the card just
received from the left neighbour — who knows its identity from their own `private_state`. Publishing
slot positions would let them pin a known card to an exact face-down slot and narrow the rest by
elimination. A tabletop gives no such handle.

**Structural note:** `tryalTotal`/`revealedTryals` make the public builder read `Player.TryalCards`
for the first time. `dispatch.js` relays `game_state_update` verbatim (host-role gate only), so the
single `.Where(IsRevealed)` in `BuildRevealedTryalLabels` is the **entire** enforcement. All public
access goes through that one greppable helper.

**Audit caveat:** PDF extraction was unavailable to the auditor, so *"Town Hall cards are dealt
face-up and read aloud"* is corroborated by three project sources (`character-spec.md`, CLAUDE.md, and
the code comment at `Player.cs:801`) but not primary-source reverified.

### New fields

| Field | Verdict |
|---|---|
| `handCount` | **SAFE** — hand *size* is openly countable at a physical table; contents stay in `private_state`. Condition: must be an `int`, never a `string[]`, so a contents leak looks wrong at a glance. |
| `topDiscard` | **SAFE** — the discard pile is face-up at a table. Single card name only, never the pile's contents or order (order would leak play history beyond what is visible). |
| `actorId` on log events | **SAFE** — who plays a card is public at a physical table. Renderer resolves to player name. |

Both new fields must be added to the contract test's `ALLOWED_PUBLIC_PLAYER_KEYS` allow-list, which
otherwise fails by design.

---

## 7b. Mirror parity debt (deferred — but REQUIRED, not polish)

**The mirror must end up an exact visual copy of this host screen.** Its purpose is that a player who
cannot see the host TV connects a device as `display` and sees **exactly** what the people at the host
screen see. Anything public the host shows and the mirror does not is an **information asymmetry
between players** — a fairness bug in a social deduction game.

Deferred to a later phase by decision (2026-08-13). The sync properties that matter are already in
place — reveals land together, no private data reaches the mirror, and the event log renders from the
same closed-kind copy — so what remains is layout and art.

**🔴 NO PROTOCOL WORK IS NEEDED.** The mirror already RECEIVES every field the host renders:
`townHall`, `tryalTotal`, `revealedTryals`, `accusationCards`, `statusCards`, `accusationLimit`,
`handCount`, and `topDiscard` on the state payload. `BoardSummary` just doesn't render them. That also
means the privacy audit does not need redoing — the data already flows and was already reviewed.

| Host element | Mirror today | Debt |
|---|---|---|
| Rectangular ring (`HostTableView`) | flat `BoardSummary` list | **layout port** — the `s`/`H`/top/bottom geometry |
| Seat: portrait, player + character name, `N IN HAND · X/Y`, `ACCUSATIONS n/7` | name + accusations only | **layout + art** |
| Seat: tryal row (revealed art + shared back) | none | **art** |
| Seat: accusation ×N stacks, effect badges | merged text list | layout |
| Seat: turn ring, HANGED overlay | turn marker only | layout |
| Meeting House: stats row, deck/discard, top-discard art, legend | `DeckSummary` counts | layout + art |
| IN EFFECT panel (+ rules text) | none | layout (text is static copy, not wire data) |
| Header: `TABLE code · N SOULS`, phase pill | room code + phase tag | layout |
| Event log | ✅ built (`EventLog.tsx`) | — |
| Night/dawn overlay | ✅ `NightDawnOverlay` | — |
| Synchronized reveal | ✅ `RevealOverlay` | card-flip art only |
| Public-reveal toast | ✅ `PublicRevealToast` | — |
| Lobby (room code + URLs) | none | decide whether a mirror needs it |

**The non-obvious cost is ART, not code.** The host resolves card images through
`HostCardSpriteRegistry` (Unity sprites). The browser needs the same images as web assets — an export
step plus a label→URL map mirroring the registry's normalization (`Trim().ToLowerInvariant()`, spaces
stripped) so `"Not a Witch"` and `"Not A Witch"` both resolve. Budget that before the layout work.

⚠️ **Do not widen the gap:** any new host element showing public information is added to both, or
appended to this table.

---

## 8. Boundary invariant

After every change to `Assets/Project/Scripts/UI/HostDisplay/`:

```
grep -nE "PlayerService|(^|[^\w])Player\b|TryalCard|HandManager|StatusCards" Assets/Project/Scripts/UI/HostDisplay/
```

Currently clean (comment/banner lines only). Any hit in executable code is a regression.
