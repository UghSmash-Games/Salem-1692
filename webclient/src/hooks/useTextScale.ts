/**
 * Large-text accessibility option for the phone client.
 *
 * MECHANISM: sets the ROOT font size. Tailwind's type and spacing utilities are rem-based
 * (`text-sm` = 0.875rem, `p-4` = 1rem), so one value scales copy AND touch targets together — which
 * is what someone enabling this actually needs. A wrapper that only bumped font-size would be
 * overridden by every `text-*` utility, so this is the one place it can be done cleanly.
 *
 * ⚠️ THE CAP IS A SAFETY LIMIT, NOT A STYLE CHOICE. Several phone screens are host-blocking with a
 * countdown (secret phase, target, tryal pick, card pick, confirm). Scale far enough and their
 * content outgrows the viewport, pushing the Confirm button below the fold — turning an
 * accessibility setting into a silently missed submission. Those screens now pin their primary
 * action with `sticky bottom-0` so it stays reachable, which is what makes even `larger` safe. If
 * you raise the cap, re-check every screen with a countdown FIRST.
 *
 * Persisted in localStorage, not sessionStorage: a preference should survive closing the tab, unlike
 * `salem.session` which deliberately does not.
 */

export type TextScale = 'normal' | 'large' | 'larger';

const KEY = 'salem.textScale';

/** Root font size in px per step. 16 is the browser default. */
const SIZE_PX: Record<TextScale, number> = {
  normal: 16,
  large: 19,
  larger: 22,
};

export const TEXT_SCALES: TextScale[] = ['normal', 'large', 'larger'];

export const TEXT_SCALE_LABELS: Record<TextScale, string> = {
  normal: 'Normal',
  large: 'Large',
  larger: 'Larger',
};

function isTextScale(v: unknown): v is TextScale {
  return v === 'normal' || v === 'large' || v === 'larger';
}

/** The stored preference, or 'normal'. Safe when storage is unavailable (private mode, SSR). */
export function getTextScale(): TextScale {
  try {
    const raw = localStorage.getItem(KEY);
    return isTextScale(raw) ? raw : 'normal';
  } catch {
    return 'normal';
  }
}

/** Apply to the document and persist. Applying is deliberately separate from React state so the
 *  saved value can be restored on load before any settings UI has mounted. */
export function applyTextScale(scale: TextScale): void {
  try {
    localStorage.setItem(KEY, scale);
  } catch {
    // Storage unavailable — still apply for this session rather than failing outright.
  }
  if (typeof document !== 'undefined') {
    document.documentElement.style.fontSize = `${SIZE_PX[scale]}px`;
  }
}

/** Restore the saved preference. Call once at app start. */
export function restoreTextScale(): void {
  applyTextScale(getTextScale());
}
