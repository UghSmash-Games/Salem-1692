'use strict';

/**
 * The wire contract's version. See docs/protocol.md → "Protocol version".
 *
 * 🔴 NOT the app version. `VERSION` / semver describes a RELEASE; this describes the WIRE, and moves
 * only when an older build can no longer talk to a newer one — a removed or renamed event, a changed
 * payload shape, a changed meaning. Most releases leave it alone.
 *
 * ⚠ Bumping this is a BREAKING act, deliberately: every Unity host build already in players' hands
 * stops being able to host. That is the point — the alternative is those builds half-working in ways
 * nobody can diagnose from a bug report. Bump it only alongside a MAJOR app version, and only when
 * an old host genuinely cannot work against the new contract.
 *
 * Keep in step with webclient/src/socket/protocol.ts and
 * Assets/Project/Scripts/Networking/Protocol.cs.
 */
const PROTOCOL_VERSION = 1;

/**
 * What a client claims to speak.
 *
 * Builds shipped before this check existed send nothing, and refusing them would have broken working
 * installs the day it landed — so absent means 1. ⛔ Do not extend that allowance to a later version:
 * once builds send the field, absence can only mean "older than the check", which is protocol 1.
 */
function claimedProtocol(data) {
  const raw = data && data.protocolVersion;
  return Number.isInteger(raw) && raw > 0 ? raw : 1;
}

/**
 * Compare a joiner against the host, returning null when they agree or an `error_msg` payload when
 * they do not.
 *
 * The message names WHICH SIDE is behind. "Incompatible versions" tells the person holding the phone
 * nothing they can act on; "the host needs to update" tells them who to talk to — and in this game
 * the host is a human in the room, not a server somewhere.
 */
function protocolMismatch(hostProtocol, yourProtocol) {
  if (hostProtocol === yourProtocol) return null;

  const message = hostProtocol < yourProtocol
    ? "This game's host is running an older version of Salem. The host needs to update."
    : 'Your device is running an older version of Salem. Reload this page to update.';

  return { message, code: 'protocol_mismatch', hostProtocol, yourProtocol };
}

module.exports = { PROTOCOL_VERSION, claimedProtocol, protocolMismatch };
