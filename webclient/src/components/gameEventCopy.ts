/**
 * kind + ids → one sentence. The ONLY prose in the client's event log.
 *
 * 🔴 THIS IS THE OTHER HALF OF THE PRIVACY MECHANISM. `game_event` carries no free text — just a
 * kind from a CLOSED vocabulary, public ids, a public card name, and a short enumerable `value`.
 * The wire physically cannot describe a secret action because no kind can express one, and the
 * prose is invented HERE from that closed set. Never add a free-text field upstream to make this
 * easier, and never add a kind for secret-phase content.
 *
 * ⚠ MUST STAY IN STEP WITH Unity's HostEventLog.Describe
 * (Assets/Project/Scripts/UI/HostDisplay/HostEventLog.cs). Both rooms watch the same game; a mirror
 * that phrases an event differently from the host screen reads as a different event. This is a
 * direct port — when you change one, change the other.
 *
 * An unrecognised kind returns null and the entry is DROPPED. A log that silently omits something
 * it does not understand is strictly safer than one that invents text for it — a future kind this
 * build has never heard of must not be guessed at.
 */

import type { GameEventPayload } from '../socket/types';

/** Resolve a public player id to a display name, or '' when unknown. */
export type NameResolver = (playerId: string | null | undefined) => string;

export function describeGameEvent(
  e: GameEventPayload,
  nameOf: NameResolver,
): string | null {
  const actor = nameOf(e.actorId);
  const target = nameOf(e.targetId);

  switch (e.kind) {
    case 'game_started':
      return 'The table is set. Tryal cards are dealt.';

    case 'phase_changed':
      return describePhase(e.value);

    case 'card_played':
      return describeCardPlayed(actor, e.cardName, target);

    case 'tryal_revealed':
      return target ? `${target}'s Tryal card is turned: ${e.value}.` : null;

    case 'double_witch_revealed':
      return target ? `${target} holds another Witch card, and survives.` : null;

    case 'gavel_placed':
      // ⛔ The RECIPIENT only. `actorId` is null by contract — naming who placed the gavel would
      // publish the constable's secret identity. Never phrase this with an actor.
      return target ? `The gavel is set before ${target}.` : null;

    case 'confession_revealed':
      return target ? `${target} confesses, and is spared the night.` : null;

    case 'player_eliminated':
      return target ? `${target} is hanged.` : null;

    case 'cards_drawn':
      // DELIBERATELY SILENT — mirrors Unity's HostEventLog. The event exists for the AUDIO cue, not
      // the log: a turn is either a draw or a play, so logging every draw would roughly double the
      // volume. Cased explicitly so this reads as a decision, not an unhandled kind.
      return null;

    case 'game_over':
      return describeWinner(e.value);

    default:
      return null;
  }
}

/**
 * The red accusation cards get their own phrasing — "accuses" reads as the dramatic beat it is,
 * where "plays Accusation on" reads like a rules footnote. Everything else falls back to the
 * generic form, so a card added later still logs sensibly without a code change.
 */
function describeCardPlayed(
  actor: string,
  card: string | null | undefined,
  target: string,
): string | null {
  if (!card) return null;

  if (actor && target) {
    switch (card) {
      case 'Accusation':
        return `${actor} accuses ${target} of consorting with the Devil.`;
      case 'Evidence':
        return `${actor} presents Evidence against ${target}.`;
      case 'Witness':
        return `${actor} calls a Witness against ${target}.`;
      default:
        return `${actor} plays ${card} on ${target}.`;
    }
  }

  if (actor) return `${actor} plays ${card}.`;
  return `${card} is played.`;
}

function describePhase(phase: string | null | undefined): string | null {
  if (!phase) return null;
  switch (phase.toLowerCase()) {
    case 'dawn':
      return 'Dawn breaks over Salem.';
    case 'day':
      return 'The town gathers in daylight.';
    case 'night':
      return 'Night falls. Players close their eyes.';
    default:
      // Setup / Conspiracy deliberately render nothing — the interesting fact is logged by its own
      // kind (game_started), not by entering the phase.
      return null;
  }
}

function describeWinner(winner: string | null | undefined): string | null {
  if (!winner) return null;
  const w = winner.toLowerCase();
  if (w.includes('witch')) return 'The witches prevail. Salem is lost.';
  if (w.includes('town') || w.includes('village'))
    return 'The town prevails. The witches are undone.';
  return null;
}

/**
 * Epoch ms → the VIEWER's local HH:mm.
 *
 * The host stamps epoch milliseconds precisely so each screen can do this. A preformatted "19:04"
 * on the wire would bake in the host's timezone and read wrong on a mirror in another region —
 * the same principle as phase_resolve.revealAt.
 */
export function formatEventTime(atMs: number): string {
  if (!Number.isFinite(atMs)) return '';
  const d = new Date(atMs);
  const hh = String(d.getHours()).padStart(2, '0');
  const mm = String(d.getMinutes()).padStart(2, '0');
  return `${hh}:${mm}`;
}
