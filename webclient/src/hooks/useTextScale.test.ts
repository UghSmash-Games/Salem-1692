/**
 * Large-text preference.
 *
 * The properties worth locking are the ones a player would notice if they broke: the choice
 * survives a reload, a corrupt or absent stored value degrades to normal rather than throwing, and
 * unavailable storage still applies the scale for the session.
 *
 * ⚠️ STORAGE IS STUBBED DELIBERATELY. In this test environment `localStorage` is a bare object with
 * no Storage methods — `setItem`/`clear` are undefined. That is exactly why the module wraps every
 * storage call in try/catch, and it means these tests must supply their own storage to exercise the
 * persistence logic rather than assert against the environment's.
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import {
  getTextScale,
  applyTextScale,
  restoreTextScale,
  TEXT_SCALES,
} from './useTextScale';

const KEY = 'salem.textScale';

/** Minimal in-memory Storage. */
function memoryStorage(overrides: Partial<Storage> = {}) {
  const map = new Map<string, string>();
  return {
    getItem: (k: string) => map.get(k) ?? null,
    setItem: (k: string, v: string) => void map.set(k, v),
    removeItem: (k: string) => void map.delete(k),
    clear: () => map.clear(),
    key: () => null,
    length: 0,
    ...overrides,
  } as Storage;
}

describe('text scale preference', () => {
  beforeEach(() => {
    vi.stubGlobal('localStorage', memoryStorage());
    document.documentElement.style.fontSize = '';
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('defaults to normal', () => {
    expect(getTextScale()).toBe('normal');
  });

  it('persists the choice and applies it to the document root', () => {
    applyTextScale('large');
    expect(localStorage.getItem(KEY)).toBe('large');
    expect(document.documentElement.style.fontSize).toBe('19px');
  });

  it('restores the saved choice on load — the point of localStorage over sessionStorage', () => {
    applyTextScale('larger');
    document.documentElement.style.fontSize = ''; // simulate a fresh document
    restoreTextScale();
    expect(document.documentElement.style.fontSize).toBe('22px');
  });

  it('ignores a corrupt stored value rather than applying nonsense', () => {
    localStorage.setItem(KEY, 'enormous');
    expect(getTextScale()).toBe('normal');
  });

  it('still applies when storage is unavailable (private browsing)', () => {
    // The setting must not be lost for the session just because it cannot be saved. This is also
    // the real behaviour in this test environment, where localStorage has no methods at all.
    vi.stubGlobal(
      'localStorage',
      memoryStorage({
        setItem: () => {
          throw new Error('QuotaExceededError');
        },
      }),
    );
    expect(() => applyTextScale('large')).not.toThrow();
    expect(document.documentElement.style.fontSize).toBe('19px');
  });

  it('survives storage with no methods at all', () => {
    vi.stubGlobal('localStorage', {} as Storage);
    expect(() => applyTextScale('larger')).not.toThrow();
    expect(getTextScale()).toBe('normal');
    expect(document.documentElement.style.fontSize).toBe('22px');
  });

  it('every step maps to a real size', () => {
    for (const s of TEXT_SCALES) {
      applyTextScale(s);
      expect(document.documentElement.style.fontSize).toMatch(/^\d+px$/);
    }
  });
});
