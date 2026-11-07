#!/usr/bin/env node
/**
 * Stamp the version in VERSION onto all three tiers.
 *
 *   node scripts/set-version.mjs            # apply VERSION everywhere
 *   node scripts/set-version.mjs 1.1.0      # set VERSION first, then apply
 *   node scripts/set-version.mjs --check    # report drift, change nothing (for CI / pre-release)
 *
 * 🔴 WHY ONE SOURCE OF TRUTH. Salem ships as three pieces that update on DIFFERENT schedules: the
 * Unity host is a downloaded build someone may run for months, the web client redeploys on every
 * push to dev, and the relay redeploys only when someone runs `fly deploy`. Before this they each
 * carried their own unrelated number (Unity 1.0, webclient 0.1.0, server 0.1.0), so "what version
 * are you on?" had three different answers and none of them identified a release.
 *
 * ⚠ UNITY MUST BE CLOSED. ProjectSettings.asset is held in memory by the Editor, which rewrites it
 * on exit — an edit made underneath a running Editor is silently reverted. This refuses to touch it
 * rather than appear to succeed.
 */

import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const checkOnly = args.includes('--check');
const explicit = args.find((a) => !a.startsWith('--'));

const SEMVER = /^\d+\.\d+\.\d+$/;

// ─── Resolve the target version ──────────────────────────────

if (explicit) {
  if (!SEMVER.test(explicit)) {
    console.error(`✗ "${explicit}" is not MAJOR.MINOR.PATCH (e.g. 1.2.0).`);
    process.exit(1);
  }
  if (checkOnly) {
    console.error('✗ --check cannot be combined with a version argument.');
    process.exit(1);
  }
  writeFileSync(join(root, 'VERSION'), `${explicit}\n`);
}

const version = readFileSync(join(root, 'VERSION'), 'utf8').trim();
if (!SEMVER.test(version)) {
  console.error(`✗ VERSION contains "${version}", which is not MAJOR.MINOR.PATCH.`);
  process.exit(1);
}

let drift = false;
const note = (ok, where, found) => {
  if (ok) {
    console.log(`  ✓ ${where} — ${version}`);
  } else {
    drift = true;
    console.log(`  ${checkOnly ? '✗' : '→'} ${where} — was ${found}`);
  }
};

// ─── package.json (web client + relay) ───────────────────────

for (const pkgPath of ['webclient/package.json', 'server/package.json']) {
  const full = join(root, pkgPath);
  const raw = readFileSync(full, 'utf8');
  const pkg = JSON.parse(raw);

  if (pkg.version === version) {
    note(true, pkgPath, pkg.version);
    continue;
  }
  note(false, pkgPath, pkg.version);
  if (checkOnly) continue;

  // Rewritten by hand rather than JSON.stringify so formatting, key order and trailing newline
  // survive — a reformatted package.json would make every release a noisy diff.
  writeFileSync(full, raw.replace(/("version"\s*:\s*)"[^"]*"/, `$1"${version}"`));
}

// ─── Unity ───────────────────────────────────────────────────

const unityPath = join(root, 'ProjectSettings/ProjectSettings.asset');
const unityRaw = readFileSync(unityPath, 'utf8');
const unityFound = (unityRaw.match(/^\s*bundleVersion:\s*(.+)$/m) || [])[1]?.trim();

if (unityFound === version) {
  note(true, 'ProjectSettings.asset (bundleVersion)', unityFound);
} else {
  note(false, 'ProjectSettings.asset (bundleVersion)', unityFound);

  if (!checkOnly) {
    // Unity writes these while an Editor has THIS project open, and removes them on exit.
    //
    // ⚠ Checking for a process named unity.exe instead was a false positive generator: Unity leaves
    // lightweight helper processes around (a 26MB, no-window one was blocking this script with the
    // Editor genuinely closed), and it would also fire when the Editor had a DIFFERENT project open,
    // which cannot affect this file at all. A lock the Editor itself maintains is the real signal.
    const editorHasProject =
      existsSync(join(root, 'Temp/UnityLockfile')) ||
      existsSync(join(root, 'Library/EditorInstance.json'));

    if (editorHasProject) {
      console.error(
        '\n✗ The Unity Editor is running. It holds ProjectSettings.asset in memory and rewrites it\n' +
        '  on exit, so an edit now would be silently reverted. Close Unity and re-run, or set\n' +
        `  Player Settings > Version to ${version} by hand.`,
      );
      process.exit(1);
    }

    writeFileSync(unityPath, unityRaw.replace(/^(\s*bundleVersion:\s*).+$/m, `$1${version}`));
  }
}

// ─── Result ──────────────────────────────────────────────────

if (checkOnly) {
  console.log(drift ? `\n✗ Tiers disagree with VERSION (${version}).` : `\n✓ All tiers at ${version}.`);
  process.exit(drift ? 1 : 0);
}

console.log(`\n✓ Version ${version} stamped. Next:`);
console.log('    git commit -am "Release v' + version + '"');
console.log('    git tag -a v' + version + ' -m "v' + version + '"');
console.log('    git push && git push --tags');
