/**
 * Event-log copy — the browser half of the "What Has Passed" renderer.
 *
 * These lock two things:
 *  1. PARITY with Unity's HostEventLog.Describe. Both rooms watch the same game; a mirror that
 *     phrases an event differently from the host screen beside it reads as a different event. The
 *     expected strings here are copied verbatim from the C# renderer.
 *  2. The DROP-UNKNOWN rule. An unrecognised kind must render nothing rather than invent text —
 *     a build that has never heard of a future kind must not guess at it.
 */

import { describe, it, expect } from 'vitest';
import { describeGameEvent, formatEventTime } from './gameEventCopy';
import type { GameEventPayload } from '../socket/types';

const NAMES: Record<string, string> = { p0: 'Alice', p1: 'Bob' };
const nameOf = (id: string | null | undefined) => (id ? (NAMES[id] ?? '') : '');

function ev(over: Partial<GameEventPayload>): GameEventPayload {
  return {
    kind: 'game_started',
    actorId: null,
    targetId: null,
    cardName: null,
    value: null,
    atMs: 0,
    ...over,
  } as GameEventPayload;
}

describe('describeGameEvent', () => {
  it('game_started', () => {
    expect(describeGameEvent(ev({ kind: 'game_started' }), nameOf)).toBe(
      'The table is set. Tryal cards are dealt.',
    );
  });

  it('phase_changed renders only the three public phases', () => {
    expect(describeGameEvent(ev({ kind: 'phase_changed', value: 'Dawn' }), nameOf)).toBe(
      'Dawn breaks over Salem.',
    );
    expect(describeGameEvent(ev({ kind: 'phase_changed', value: 'Day' }), nameOf)).toBe(
      'The town gathers in daylight.',
    );
    expect(describeGameEvent(ev({ kind: 'phase_changed', value: 'Night' }), nameOf)).toBe(
      'Night falls. Players close their eyes.',
    );
    // Setup / Conspiracy render nothing — game_started covers the interesting fact.
    expect(describeGameEvent(ev({ kind: 'phase_changed', value: 'Setup' }), nameOf)).toBeNull();
  });

  it('card_played gives the red cards dramatic phrasing', () => {
    const base = { kind: 'card_played' as const, actorId: 'p0', targetId: 'p1' };
    expect(describeGameEvent(ev({ ...base, cardName: 'Accusation' }), nameOf)).toBe(
      'Alice accuses Bob of consorting with the Devil.',
    );
    expect(describeGameEvent(ev({ ...base, cardName: 'Evidence' }), nameOf)).toBe(
      'Alice presents Evidence against Bob.',
    );
    expect(describeGameEvent(ev({ ...base, cardName: 'Witness' }), nameOf)).toBe(
      'Alice calls a Witness against Bob.',
    );
  });

  it('card_played falls back generically so a new card still logs', () => {
    expect(
      describeGameEvent(
        ev({ kind: 'card_played', actorId: 'p0', targetId: 'p1', cardName: 'Asylum' }),
        nameOf,
      ),
    ).toBe('Alice plays Asylum on Bob.');
    expect(
      describeGameEvent(ev({ kind: 'card_played', actorId: 'p0', cardName: 'Alibi' }), nameOf),
    ).toBe('Alice plays Alibi.');
    expect(describeGameEvent(ev({ kind: 'card_played', cardName: 'Alibi' }), nameOf)).toBe(
      'Alibi is played.',
    );
  });

  it('tryal_revealed names the player and the revealed label', () => {
    expect(
      describeGameEvent(ev({ kind: 'tryal_revealed', targetId: 'p1', value: 'Witch' }), nameOf),
    ).toBe("Bob's Tryal card is turned: Witch.");
  });

  it('double_witch_revealed, player_eliminated, confession_revealed', () => {
    expect(
      describeGameEvent(ev({ kind: 'double_witch_revealed', targetId: 'p1' }), nameOf),
    ).toBe('Bob holds another Witch card, and survives.');
    expect(describeGameEvent(ev({ kind: 'player_eliminated', targetId: 'p1' }), nameOf)).toBe(
      'Bob is hanged.',
    );
    expect(
      describeGameEvent(ev({ kind: 'confession_revealed', targetId: 'p1' }), nameOf),
    ).toBe('Bob confesses, and is spared the night.');
  });

  it('gavel_placed names ONLY the recipient — never who placed it', () => {
    // ⛔ The constable's identity is secret. actorId is null by contract; the copy must not have a
    // slot for it. If this ever reads "Alice sets the gavel…", a secret role has been published.
    const line = describeGameEvent(
      ev({ kind: 'gavel_placed', actorId: null, targetId: 'p1' }),
      nameOf,
    );
    expect(line).toBe('The gavel is set before Bob.');
    expect(line).not.toContain('Alice');
  });

  it('game_over maps the winner label', () => {
    // ⚠ These are the values Unity ACTUALLY emits — EndGameResult.WinningTeam.ToString(), i.e. the
    // Team enum: "Witches" / "Villagers". Not "Townspeople", which reads naturally but is not a
    // value this system ever produces. The emitter previously sent the type name
    // "Salem.Data.EndGameResult" here, which matched nothing and dropped the entry entirely.
    expect(describeGameEvent(ev({ kind: 'game_over', value: 'Witches' }), nameOf)).toBe(
      'The witches prevail. Salem is lost.',
    );
    expect(describeGameEvent(ev({ kind: 'game_over', value: 'Villagers' }), nameOf)).toBe(
      'The town prevails. The witches are undone.',
    );
  });

  it('drops a Draw result rather than inventing a winner', () => {
    // Team.Draw exists in the enum. Neither renderer has copy for it, so the entry is dropped —
    // correct by the drop-unknown rule, and noted here so a real draw is a known silence, not a
    // mystery. Add copy to BOTH renderers if draws ever become reachable.
    expect(describeGameEvent(ev({ kind: 'game_over', value: 'Draw' }), nameOf)).toBeNull();
  });

  it('renders NOTHING for cards_drawn — it exists for the audio cue only', () => {
    // Deliberate silence, not an oversight. A turn is either a draw or a play, so logging every
    // draw would roughly double log volume and push more interesting entries out of the window.
    // If someone later "fixes" this by adding copy, this test should fail and make them decide.
    expect(
      describeGameEvent(ev({ kind: 'cards_drawn', actorId: 'p0', value: '2' }), nameOf),
    ).toBeNull();
  });

  it('DROPS an unrecognised kind rather than inventing text', () => {
    expect(
      describeGameEvent(ev({ kind: 'night_vote_cast' as never, targetId: 'p1' }), nameOf),
    ).toBeNull();
  });

  it('drops entries whose required player is unknown', () => {
    // A name that cannot be resolved would otherwise render "'s Tryal card is turned".
    expect(
      describeGameEvent(ev({ kind: 'tryal_revealed', targetId: 'ghost', value: 'Witch' }), nameOf),
    ).toBeNull();
  });
});

describe('formatEventTime', () => {
  it('formats epoch ms in the VIEWER local time, zero-padded', () => {
    // Built from local components so the assertion holds in any timezone — the point is that the
    // client does the conversion, not that any particular zone is used.
    const d = new Date(2026, 0, 2, 9, 4, 0);
    expect(formatEventTime(d.getTime())).toBe('09:04');
  });

  it('is safe on a missing timestamp', () => {
    expect(formatEventTime(Number.NaN)).toBe('');
  });
});
