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
