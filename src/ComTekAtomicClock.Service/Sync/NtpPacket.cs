// ComTekAtomicClock.Service.Sync.NtpPacket
//
// Minimal RFC 4330 SNTP/v4 packet support — just enough to send a
// client query and parse a server response.
//
// Wire layout (48 bytes):
//   byte  0:    LI(2) | VN(3) | Mode(3)
//   byte  1:    Stratum
//   byte  2:    Poll      (signed log2 seconds)
//   byte  3:    Precision (signed log2 seconds)
//   bytes 4-7:  Root Delay      (32-bit signed fixed-point, sec)
//   bytes 8-11: Root Dispersion (32-bit unsigned fixed-point, sec)
//   bytes 12-15: Reference Identifier
//   bytes 16-23: Reference Timestamp (64-bit NTP timestamp, big-endian)
//   bytes 24-31: Originate Timestamp
//   bytes 32-39: Receive Timestamp
//   bytes 40-47: Transmit Timestamp
//
// NTP timestamps are 64-bit fixed-point: high 32 bits = seconds since
// 1900-01-01 UTC, low 32 bits = 2^-32 fractional second.

using System.Buffers.Binary;

namespace ComTekAtomicClock.Service.Sync;

internal static class NtpPacket
{
    public const int Size = 48;

    /// <summary>NTP epoch: 1900-01-01T00:00:00Z.</summary>
    public static readonly DateTime NtpEpoch =
        new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Build a 48-byte client SNTPv4 query packet. Stamps the
    /// Transmit Timestamp field (bytes 40-47) with the current
    /// system time so the round-trip computation in
    /// <see cref="ParseResponse"/> can use it as T1 (originate).
    /// </summary>
    public static byte[] BuildClientRequest(out DateTime t1Utc)
    {
        var buf = new byte[Size];
        // LI=0, VN=4, Mode=3 (client) -> 0x23
        buf[0] = 0x23;

        t1Utc = DateTime.UtcNow;
        WriteNtpTimestamp(buf, 40, t1Utc);
        return buf;
    }

    /// <summary>
    /// Parse a 48-byte SNTP server response. Returns the round-trip
    /// offset in seconds (signed; positive = local clock is fast,
    /// negative = local clock is slow), the round-trip delay in
    /// seconds, and the server's Stratum field. The server's
    /// Transmit Timestamp is also returned for callers that want to
    /// log the raw server time.
    /// </summary>
    /// <param name="buf">48-byte server response.</param>
    /// <param name="t1Utc">Local clock at moment of send (T1). Same value returned by <see cref="BuildClientRequest"/>.</param>
    /// <param name="t4Utc">Local clock at moment of receive (T4). Caller measures this immediately on read.</param>
    public static SntpResult ParseResponse(byte[] buf, DateTime t1Utc, DateTime t4Utc)
    {
        if (buf is null || buf.Length < Size)
            throw new ArgumentException($"SNTP response must be at least {Size} bytes.", nameof(buf));

        // Validate the response shape per RFC 4330 § 5.
        var b0 = buf[0];
        var li     = (b0 >> 6) & 0x03;     // 0..3
        var vn     = (b0 >> 3) & 0x07;     // 1..4
        var mode   =  b0       & 0x07;     // 4 = server response
        var stratum = buf[1];

        if (mode != 4)
            throw new InvalidDataException($"SNTP response Mode field is {mode}, expected 4 (server).");
        if (vn is < 3 or > 4)
            throw new InvalidDataException($"SNTP response Version field is {vn}, expected 3 or 4.");
        if (li == 3)
            throw new InvalidDataException("SNTP response Leap Indicator is 3 (server unsynchronized).");
        if (stratum is 0 or > 15)
            throw new InvalidDataException($"SNTP response Stratum field is {stratum}, expected 1..15.");

        // Read the four key timestamps.
        // T1 is what the client sent (server should echo it in
        //   Originate Timestamp at bytes 24-31). We trust our local
        //   t1Utc rather than the server's echo to avoid a malicious
        //   server lying about it.
        var t2Utc = ReadNtpTimestamp(buf, 32); // Receive Timestamp
        var t3Utc = ReadNtpTimestamp(buf, 40); // Transmit Timestamp

        // RFC 4330 round-trip offset and delay:
        //   offset = ((T2 - T1) + (T3 - T4)) / 2
        //   delay  = (T4 - T1) - (T3 - T2)
        //
        // Sign convention: if offset > 0, the server's clock is AHEAD
        // of ours -> we are SLOW -> add `offset` to local clock to
        // sync. Equivalently, the system clock is "behind by `offset`".
        var offset = ((t2Utc - t1Utc) + (t3Utc - t4Utc)) / 2.0;
        var delay  = (t4Utc - t1Utc) - (t3Utc - t2Utc);

        return new SntpResult(
            OffsetSeconds:    offset.TotalSeconds,
            RoundTripSeconds: delay.TotalSeconds,
            Stratum:          stratum,
            ServerTransmitUtc: t3Utc);
    }

    // --------------------------------------------------------------
    // NTP timestamp <-> DateTime helpers
    // --------------------------------------------------------------

    private static void WriteNtpTimestamp(byte[] buf, int offset, DateTime utc)
    {
        var ntp = (utc - NtpEpoch).TotalSeconds;
        var seconds = (uint)Math.Truncate(ntp);
        var frac    = (uint)((ntp - seconds) * 4_294_967_296.0); // 2^32
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(offset, 4), seconds);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(offset + 4, 4), frac);
    }

    private static DateTime ReadNtpTimestamp(byte[] buf, int offset)
    {
        var seconds = BinaryPrimitives.ReadUInt32BigEndian(buf.AsSpan(offset, 4));
        var frac    = BinaryPrimitives.ReadUInt32BigEndian(buf.AsSpan(offset + 4, 4));
        var fracSec = frac / 4_294_967_296.0;
        return NtpEpoch.AddSeconds(seconds + fracSec);
    }
}

/// <summary>Result of parsing an SNTP server response.</summary>
internal readonly record struct SntpResult(
    double OffsetSeconds,
    double RoundTripSeconds,
    int Stratum,
    DateTime ServerTransmitUtc);
