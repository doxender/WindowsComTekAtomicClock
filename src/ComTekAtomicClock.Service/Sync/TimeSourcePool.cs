// ComTekAtomicClock.Service.Sync.TimeSourcePool
//
// Multi-source stratum-1 pool registry. Replaces the v0.0.35-and-earlier
// NistPool.cs which hardcoded a single (US/NIST) pool. Each named
// `TimeSource` (per Shared.Settings.TimeSource enum) maps to:
//
//   · A primary anycast hostname (tried first on every sync attempt).
//   · A list of stratum-1 server hostnames that the worker walks in
//     randomized order after the primary.
//   · An IsKnownHost predicate to validate user-configured SyncServer
//     entries (per legacy spec § 1.5).
//
// Sources currently shipped (v0.0.36):
//
//   Boulder — US, NIST. Anycast time.nist.gov plus 10 named stratum-1
//             servers across Gaithersburg, MD (-g) and Fort Collins, CO
//             (-wwv). NIST's primary atomic clocks (NIST-F1, NIST-F2)
//             live in Boulder, CO — hence the brand-friendly source name.
//
//   Brazil  — NIC.br / NTP.br. Anycast a.ntp.br plus the rest of the
//             public NTP.br pool (b/c/d.ntp.br + gps.ntp.br for the
//             GPS-disciplined stratum-1 explicitly). Hosted by NIC.br
//             in São Paulo, BR — the de-facto regional time authority
//             for South America.
//
// Refresh from each operator's published list per release:
//   NIST:    https://tf.nist.gov/tf-cgi/servers.cgi
//   NTP.br:  https://ntp.br/guia-mais-rapida.php

namespace ComTekAtomicClock.Service.Sync;

using ComTekAtomicClock.Shared.Settings;

internal static class TimeSourcePool
{
    // ----------------------------------------------------------------
    // Boulder — NIST stratum-1
    // ----------------------------------------------------------------

    /// <summary>NIST anycast endpoint that load-balances across the whole pool.</summary>
    public const string BoulderAnycast = "time.nist.gov";

    /// <summary>
    /// NIST stratum-1 servers reachable via SNTP. Two physical
    /// locations: NIST headquarters in Gaithersburg, MD (suffix -g)
    /// and the WWV/WWVB radio site in Fort Collins, CO (suffix -wwv).
    /// </summary>
    public static readonly IReadOnlyList<string> BoulderStratumOne = new[]
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

    // ----------------------------------------------------------------
    // Brazil — NIC.br / NTP.br stratum-1
    // ----------------------------------------------------------------

    /// <summary>NTP.br anycast equivalent (first published server).</summary>
    public const string BrazilAnycast = "a.ntp.br";

    /// <summary>
    /// NTP.br public stratum-1 pool. Hosted by NIC.br in São Paulo, BR.
    /// All GPS-disciplined; gps.ntp.br is the explicit GPS server.
    /// </summary>
    public static readonly IReadOnlyList<string> BrazilStratumOne = new[]
    {
        "a.ntp.br",
        "b.ntp.br",
        "c.ntp.br",
        "d.ntp.br",
        "gps.ntp.br",
    };

    // ----------------------------------------------------------------
    // Source-aware accessors
    // ----------------------------------------------------------------

    /// <summary>
    /// The default primary anycast for <paramref name="source"/>. Returned
    /// as the first hostname from <see cref="GetWalkOrder"/>.
    /// </summary>
    public static string GetAnycast(TimeSource source) => source switch
    {
        TimeSource.Boulder => BoulderAnycast,
        TimeSource.Brazil  => BrazilAnycast,
        _                  => BoulderAnycast,
    };

    /// <summary>
    /// The full stratum-1 pool for <paramref name="source"/>.
    /// </summary>
    public static IReadOnlyList<string> GetPool(TimeSource source) => source switch
    {
        TimeSource.Boulder => BoulderStratumOne,
        TimeSource.Brazil  => BrazilStratumOne,
        _                  => BoulderStratumOne,
    };

    /// <summary>
    /// Walk order for one sync attempt against <paramref name="source"/>:
    /// <paramref name="primary"/> first, then every server in the pool
    /// not equal to the primary, in randomized (Fisher–Yates) order.
    /// </summary>
    public static IEnumerable<string> GetWalkOrder(TimeSource source, string primary)
    {
        yield return primary;

        var rest = GetPool(source).Where(s =>
            !string.Equals(s, primary, StringComparison.OrdinalIgnoreCase)).ToList();

        var rng = Random.Shared;
        for (var i = rest.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (rest[i], rest[j]) = (rest[j], rest[i]);
        }

        foreach (var s in rest) yield return s;
    }

    /// <summary>
    /// True iff <paramref name="hostname"/> is on the published list
    /// for <paramref name="source"/> (anycast or any pool member).
    /// Used by the §1.5 input validator on the "Sync server" setting,
    /// scoped to the active TimeSource.
    /// </summary>
    public static bool IsKnownHost(TimeSource source, string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname)) return false;
        var anycast = GetAnycast(source);
        if (string.Equals(hostname, anycast, StringComparison.OrdinalIgnoreCase))
            return true;
        return GetPool(source).Any(s =>
            string.Equals(s, hostname, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// True iff <paramref name="hostname"/> is on ANY pool's published
    /// list. Useful for round-tripping a SyncServer entry that was set
    /// under a different TimeSource than the current one.
    /// </summary>
    public static bool IsKnownHostAcrossSources(string hostname)
    {
        return IsKnownHost(TimeSource.Boulder, hostname)
            || IsKnownHost(TimeSource.Brazil, hostname);
    }
}
