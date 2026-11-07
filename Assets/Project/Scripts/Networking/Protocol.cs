namespace Salem.Networking
{
    /// <summary>
    /// The wire contract's version. See docs/protocol.md → "Protocol version".
    ///
    /// 🔴 NOT the app version. Application.version describes a RELEASE; this describes the WIRE, and
    /// moves only when an older build can no longer talk to a newer one — a removed or renamed
    /// event, a changed payload shape, a changed meaning. Most releases leave it alone.
    ///
    /// 🔴 THE HOST IS THE REFERENCE. The relay records whatever this build sends at create_room and
    /// then refuses any phone or mirror that disagrees. That makes bumping this a BREAKING act by
    /// design: every host build already in players' hands stops being able to host. The alternative
    /// is those builds half-working in ways nobody can diagnose from a bug report — a stale host
    /// that connects fine and then mis-renders a seat is far worse than one that says so.
    ///
    /// ⚠ Keep in step with server/src/protocol.js and webclient/src/socket/protocol.ts.
    /// </summary>
    public static class Protocol
    {
        public const int Version = 1;
    }
}
