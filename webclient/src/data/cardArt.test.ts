/**
 * Card art resolution.
 *
 * The property that matters is the one that broke on the host: the WIRE label and the ASSET name
 * differ in case and spacing, so normalization has to reconcile them. A silent miss here blanks the
 * mirror's tryals while the host shows them — the exact information asymmetry the parity work exists
 * to remove.
 */

import { describe, it, expect } from 'vitest';
import { cardArt, townHallArt, normalizeCardLabel, TRYAL_BACK } from './cardArt';

describe('normalizeCardLabel', () => {
  it('reconciles the wire label with the asset name', () => {
    // "Not a Witch" is what LabelFor puts on the wire; "NOT A Witch" is the asset. Both must land
    // on the same key.
    expect(normalizeCardLabel('Not a Witch')).toBe('notawitch');
    expect(normalizeCardLabel('NOT A Witch')).toBe('notawitch');
    expect(normalizeCardLabel('  Black  Cat ')).toBe('blackcat');
  });
});

describe('cardArt', () => {
  it('resolves every tryal label the wire can send', () => {
    // These are exactly the three LabelFor produces.
    for (const label of ['Witch', 'Constable', 'Not a Witch']) {
      expect(cardArt(label), label).not.toBeNull();
    }
  });

  it('resolves the played-card names that appear on seats', () => {
    for (const label of [
      'Accusation', 'Evidence', 'Witness',
      'Asylum', 'Piety', 'Matchmaker', 'Black Cat',
      'Alibi', 'Curse', 'Scapegoat', 'Robbery', 'Arson', 'Stocks',
    ]) {
      expect(cardArt(label), label).not.toBeNull();
    }
  });

  it('returns null for an unknown label rather than a broken URL', () => {
    expect(cardArt('Spectral Evidence')).toBeNull();
    expect(cardArt('')).toBeNull();
    expect(cardArt(null)).toBeNull();
  });

  it('exposes ONE shared face-down back', () => {
    expect(TRYAL_BACK).toBe('/cards/back.jpg');
  });
});

describe('townHallArt', () => {
  it('resolves all 15 characters by their PRINTED name', () => {
    for (const name of [
      'Abigail Williams', 'Anne Putnam', 'Cotton Mather', 'George Burroughs',
      'Giles Corey', 'John Proctor', 'Martha Corey', 'Mary Warren',
      'Rebecca Nurse', 'Samuel Parris', 'Sarah Good', 'Thomas Danforth',
      'Tituba', 'Will Griggs', 'William Phipps',
    ]) {
      expect(townHallArt(name), name).not.toBeNull();
    }
  });

  it('uses the CARD spelling, not the C# enum spelling', () => {
    // enum WillGrigs / WilliamsPhipps vs card "Will Griggs" / "William Phipps".
    expect(townHallArt('Will Grigs')).toBeNull();
    expect(townHallArt('Williams Phipps')).toBeNull();
  });
});
