using System;

namespace Salem.Networking
{
    /// <summary>
    /// Where this host build points: the relay it connects to, and the web-client URL it prints on
    /// the TV for players to type.
    ///
    /// 🔴 WHY THIS IS NOT JUST INSPECTOR FIELDS. Both values are deployment-specific, and a
    /// serialized field is baked into the scene at build time — so a standalone host would be
    /// permanently pointed at whatever was in the Inspector the day it was built, and moving between
    /// a LAN dev server and production would mean a rebuild. Worse for the client URL: it lived in
    /// TWO separate fields (HostLobbyPanel.baseUrl and HostHeader.displayUrl) that had to be edited
    /// by hand in step with each other, and nothing stopped the lobby and the in-game header from
    /// printing different addresses.
    ///
    /// Precedence for both, most specific first:
    ///   1. a command-line argument   (how you launch a built host)
    ///   2. an environment variable
    ///   3. the Inspector value       (the fallback, and what the Editor normally uses)
    /// </summary>
    public static class DeploymentConfig
    {
        /// <summary>`-server wss://relay.example.com` — the relay this host connects to.</summary>
        public const string ServerArg = "-server";
        public const string ServerEnv = "SALEM_SERVER_URL";

        /// <summary>`-clienturl https://game.example.com` — the web client's BASE url (no path).
        /// The lobby appends /join and /display; the header appends /join.</summary>
        public const string ClientArg = "-clienturl";
        public const string ClientEnv = "SALEM_CLIENT_URL";

        /// <summary>The launch-time override for a setting, or null when none was supplied.</summary>
        public static string Override(string argName, string envName)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == argName && !string.IsNullOrWhiteSpace(args[i + 1]))
                    return args[i + 1].Trim();
            }

            var fromEnv = Environment.GetEnvironmentVariable(envName);
            return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv.Trim();
        }

        /// <summary>The relay URL override, or null to use the Inspector fallback.</summary>
        public static string ServerUrlOverride() => Override(ServerArg, ServerEnv);

        /// <summary>
        /// The web-client base URL override, or null to use the Inspector fallback.
        /// Trailing slashes are trimmed so callers can append "/join" without doubling it.
        /// </summary>
        public static string ClientBaseUrlOverride()
        {
            var url = Override(ClientArg, ClientEnv);
            return url?.TrimEnd('/');
        }
    }
}
