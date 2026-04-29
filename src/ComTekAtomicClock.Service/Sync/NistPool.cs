// ComTekAtomicClock.Service.Sync.NistPool
//
// The hardcoded NIST stratum-1 pool described in requirements.txt
// § 1.5. Refresh from https://tf.nist.gov/tf-cgi/servers.cgi per
// release. The user-configurable "Sync server" setting must be one
// of these names; a future input validator will reject anything else
// per § 1.5.
//
// On each sync attempt the service walks: primary anycast first,
// then the rest of the pool in randomized order. The first
// successful, signature-validated response wins.

namespace ComTekAtomicClock.Service.Sync;

internal static class NistPool
{
    /// <summary>
    /// Anycast endpoint that load-balances across NIST's whole pool.
    /// Default primary; tried first on every sync attempt.
    /// </summary>
    public const string Anycast = "time.nist.gov";

    /// <summary>
    /// NIST stratum-1 servers reachable via SNTP. Two physical
    /// locations: NIST headquarters in Gaithersburg, MD (suffix `-g`)
    /// and the WWV/WWVB radio site in Fort Collins, CO (suffix `-wwv`).
    /// NIST's primary atomic clocks (NIST-F1, NIST-F2) live in
    /// Boulder, CO; the public NTP servers serve their time over IP.
    /// </summary>
    public static readonly IReadOnlyList<string> StratumOnePool = new[]
    {
        // Gaithersburg, MD
        "time-a-g.nist.gov",
        "time-b-g.nist.gov",
        "time-c-g.nist.gov",
        "time-d-g.nist.gov",
        "time-e-g.nist.gov",
        // Fort Collins, CO
        "time-a-wwv.nist.gov",
        "time-b-wwv.nist.gov",
        "time-c-wwv.nist.gov",
        "time-d-wwv.nist.gov",
        "time-e-wwv.nist.gov",
    };

    /// <summary>
    /// Walk order for one sync attempt: <paramref name="primary"/>
    /// first, then every server in <see cref="StratumOnePool"/> not
    /// equal to the primary, in randomized order. The randomization
    /// spreads load across NIST's pool.
    /// </summary>
    public static IEnumerable<string> GetWalkOrder(string primary)
    {
        yield return primary;

        var rest = StratumOnePool.Where(s =>
            !string.Equals(s, primary, StringComparison.OrdinalIgnoreCase)).ToList();

        // Fisher-Yates shuffle in place using a per-call Random.
        var rng = Random.Shared;
        for (var i = rest.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (rest[i], rest[j]) = (rest[j], rest[i]);
        }

        foreach (var s in rest) yield return s;
    }

    /// <summary>
    /// True iff <paramref name="hostname"/> is on NIST's published
    /// list (anycast or any pool member). Used by the §1.5 input
    /// validator on the "Sync server" setting.
    /// </summary>
    public static bool IsKnownNistHost(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname)) return false;
        if (string.Equals(hostname, Anycast, StringComparison.OrdinalIgnoreCase))
            return true;
        return StratumOnePool.Any(s =>
            string.Equals(s, hostname, StringComparison.OrdinalIgnoreCase));
    }
}
