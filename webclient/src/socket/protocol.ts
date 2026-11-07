/**
 * The wire contract's version. See docs/protocol.md → "Protocol version".
 *
 * 🔴 NOT the app version. `APP_VERSION` describes a RELEASE; this describes the WIRE, and moves only
 * when an older build can no longer talk to a newer one. Most releases leave it alone.
 *
 * The relay compares this against the HOST's and refuses the join on a mismatch, naming which side
 * is behind — the host and the phones update on completely different schedules, so disagreement is
 * a normal thing to encounter rather than an exceptional one.
 *
 * ⚠ Keep in step with server/src/protocol.js and
 * Assets/Project/Scripts/Networking/Protocol.cs.
 */
export const PROTOCOL_VERSION = 1;
