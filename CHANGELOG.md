# Changelog

Player-facing changes, one section per released version. This is the **source** for Steam patch
notes — write it here, then paste to Steamworks, so a version maps to a tag, a build, *and* a
description of what changed. Steam's own notes are not version-controlled; this is.

Entries are written for **players**, not for the commit log. If a change is invisible in play, it
belongs under *Behind the scenes* or nowhere at all — `git log` already records everything.

Versions follow [semver](docs/versioning.md). The separate wire `protocolVersion` is noted only when
it moves, because that is the change that stops older host builds from hosting.

---

## [Unreleased]

_Nothing yet._

---

## [1.1.0] — 2026-11-25

A front door, a voice, and opponents who no longer beat themselves. Wire `protocolVersion` is still
**1**, so a 1.0.0 host still works against this relay and web client.

### Added
- **Title screen.** The game now opens on **SALEM 1692** with **Start**, **Instructions** and
  **Quit**. Previously the first thing a group saw was the lobby — a room code, two
  near-identical addresses, a roster and a pace control, with no context for any of it.
- **An instructions page**, reachable before anyone commits to a game. It leads with the part that
  actually goes wrong in a living room: every player needs their own phone, that phone *is* their
  hand of cards, and the second address is an optional extra screen rather than a way to play. Then
  a brief manual — setup, taking a turn, accusations, the night, and how each side wins.
- **A studio logo plays at launch.** Skippable with any key or click.
- **Sound.** Playing a card, drawing two, an elimination, nightfall, and each side's victory now
  have their own cue on the host screen. More to come — accusations and the day and dawn beds are
  still silent.

### Changed
- **AI opponents play like opponents.** Their choice of target now follows what the card or the
  phase actually does, and they remember who has done what to whom across the game — the same
  plays everyone at the table watched happen, nothing more. They pick from what that history
  suggests rather than always playing the single best move, so they stay unpredictable rather than
  becoming readable after two rounds.
- **AI turns are paced to be watchable.** A turn used to resolve in one beat, which with four AI
  seats was a blur; it is now a pause to think and then the card landing, so there is something to
  form an opinion about.

### Fixed
- **An AI witch no longer kills her own partner at night**, and an AI constable no longer protects
  at random. Every secret-phase choice ran through one line that picked a random name, so at five
  players the witches sabotaged themselves on roughly a quarter of nights — which reads as a
  broken game rather than a weak opponent.
- **AI players no longer hand out help to the player they are trying to beat.** Alibi, Asylum and
  Piety purely benefit whoever receives them, and were being aimed like attacks. Scapegoat and
  Robbery had the same problem in reverse: Scapegoat clears the accusations in front of its target,
  and Robbery's second player *gains* the stolen hand.

### Behind the scenes
- The AI is confined to public information by construction — the board everyone can see, its own
  cards, and its fellow witches only once dawn has introduced them. It runs inside the host, where
  every tryal card is one step away, so this is enforced rather than trusted.
- The host screen has sound and second-screen displays do not. Logged as a parity gap to close, not
  as polish: a cue is a broadcast to the room, so a mirror room currently misses a beat the host
  room hears. Phones are deliberately excluded — a sound on one phone and not another during a
  secret phase is exactly the signal the masking model exists to prevent.
- New `docs/backlog.md` for ideas parked on purpose, so they stop being re-litigated or forgotten.

---

## [1.0.0] — 2026-11-07

First tagged release. Everything before this lives in git history rather than here; this entry
covers the cycle that produced the tag.

Shipped to the Steam **Playtest** app (5526150) and the **main** app (5409770). All three parts —
Unity host, phone client, relay — report `1.0.0`. Wire `protocolVersion` 1.

### Added
- **End-of-game screen** on the host display, naming the winning side, with **New Game** and
  **Quit**. Previously the final board simply stayed on screen.
- **Escape menu** on the host: Keep Playing / Restart / Quit. Restart hands out a fresh room code
  and returns every phone to the join screen. Restart only appears once a game is running.
- **Lobby labels both links** — one for every player's phone or tablet, one an optional second
  screen for a TV or laptop.
- **The display page explains itself** when opened by mistake, with a link to the player join page.
- **Version number** on the host screen and both join pages, so a bug report can name a build.

### Fixed
- The end-of-game panel could stay stuck on screen after restarting, which also left the Escape
  menu unresponsive for the rest of the session.
- A player sharing a display name with someone else was not warned when selecting themselves during
  the constable save.

### Behind the scenes
- Host and phones check they are on compatible versions when joining, and say which side is out of
  date, rather than failing confusingly later.
- One version number is now stamped across the host, the phone client and the relay from a single
  file.

[Unreleased]: https://github.com/UghSmash-Games/Salem-1692/compare/v1.0.0...dev
[1.0.0]: https://github.com/UghSmash-Games/Salem-1692/releases/tag/v1.0.0
