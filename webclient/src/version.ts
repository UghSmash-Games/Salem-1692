/**
 * The build's version, shown on the join screens so a bug report can name one.
 *
 * `__APP_VERSION__` is replaced at build time by Vite (see vite.config.ts) with the version from
 * package.json, which scripts/set-version.mjs stamps from the repo's VERSION file.
 *
 * ⚠ The fallback is not decoration. The constant only exists where Vite's `define` has run — a
 * stray import from a plain node context, or a test runner configured without this config, would
 * otherwise throw a ReferenceError at module load and take the whole screen down over a version
 * label. A screen that renders "dev" is strictly better than a screen that does not render.
 */

declare const __APP_VERSION__: string | undefined;

export const APP_VERSION: string =
  typeof __APP_VERSION__ === 'string' && __APP_VERSION__.length > 0 ? __APP_VERSION__ : 'dev';

/** Rendered form, e.g. "v1.0.0". */
export const APP_VERSION_LABEL = `v${APP_VERSION}`;
