# Backlog — ideas parked deliberately

Things worth doing that nothing is currently blocked on. This is NOT a bug list and NOT deferred
phase work: open bugs and known gaps live in `CLAUDE.md`, character work in `character-spec.md`,
and mirror parity debt in `phase-7-host-seat-design.md` §7b.

An item lands here when it is a real improvement, the shape of it is understood, and the decision
was to **not build it now**. Each entry records enough to pick it up cold, including why it was
parked — a parked item with no reason gets re-litigated every time someone reads the file.

---

## Deck shuffle / reshuffle — animation + audio

**Idea.** Make the deck re-forming a visible, audible moment on the host screen (and the mirror)
instead of a silent state change.

**Why it is worth doing.** The deck running out and re-forming from the discard pile is a real beat
in a game of Salem — it means every card played so far is back in circulation, and the discard pile
the table has been reading all game is suddenly gone. Right now it happens invisibly. It is also the
one moment the existing art and SFX assets were obviously bought for.

**Status: parked, and it needs a protocol decision first.**

🔴 **There is no public event for it.** `GameEventKind` is a CLOSED set of nine rendered kinds plus
`CardsDrawn`, and none of them covers a reshuffle — so there is currently nothing for either an
animation or an audio cue to hang on, on the host or the mirror. Building this means adding a kind
(e.g. `deck_reshuffled`, actor/target null, no card names — the card identities are the whole point
of what must not leak), which is wire-contract surface rather than a cosmetic change. That is why it
is parked rather than slotted in with the rest of the audio work.

⚠️ **Adding the kind is cheap but not free.** The closed vocabulary IS the event log's privacy
mechanism (see `protocol.md` → `game_event`), so every addition has to be argued, and both renderers
(`HostEventLog.Describe` and the web `gameEventCopy`) need a decision — most likely "do not render
it", as `CardsDrawn` already does, since a log line per reshuffle would crowd the 14-entry window.
It would **not** need a `protocolVersion` bump: an older host simply never sends it.

**Assets already owned, currently unreachable:**
- `Assets/Project/Audio/Sound Effects/shuffle.wav` and `shuffle (2).wav` — referenced by
  `DeckShuffleAnimation.cs`, which is in **no scene and no prefab**. Dead code today.
- `jasonlee3071-church-bells-194653.mp3` — referenced by nothing at all. Not a shuffle sound; noted
  here because it is the third orphan in that folder and a candidate for the night-begins cue.

**Sequence if picked up:** add the event kind → emit it from `DeckManager.ReshuffleDiscardPile` →
host animation (revive or replace `DeckShuffleAnimation`, which currently expects an `AudioSource`
on its own GameObject) → wire the clip → mirror playback alongside the rest of §7b's audio parity.

---

## Mirror audio

Tracked in `phase-7-host-seat-design.md` §7b, not here — it is parity debt with a standing
requirement behind it, not an optional idea.
