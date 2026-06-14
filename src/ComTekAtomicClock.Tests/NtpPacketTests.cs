// Tests for the SNTP packet parser — the highest-stakes pure logic in the
// service (it decodes a network packet whose result is used to SET THE
// SYSTEM CLOCK). Covers the round-trip offset/delay math and every
// RFC 4330 §5 validation gate.

using System.Buffers.Binary;
using System.IO;
using ComTekAtomicClock.Service.Sync;

namespace ComTekAtomicClock.Tests;

public class NtpPacketTests
{
    private static readonly DateTime Epoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static void WriteTs(byte[] buf, int off, DateTime utc)
    {
        var ntp = (utc - Epoch).TotalSeconds;
        var seconds = (uint)Math.Truncate(ntp);
        var frac = (uint)((ntp - seconds) * 4_294_967_296.0);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(off, 4), seconds);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(off + 4, 4), frac);
    }

    private static byte[] Response(DateTime t2, DateTime t3,
        int stratum = 1, int vn = 4, int mode = 4, int li = 0)
    {
        var buf = new byte[NtpPacket.Size];
        buf[0] = (byte)((li << 6) | (vn << 3) | mode);
        buf[1] = (byte)stratum;
        WriteTs(buf, 32, t2); // Receive  Timestamp (T2)
        WriteTs(buf, 40, t3); // Transmit Timestamp (T3)
        return buf;
    }

    [Fact]
    public void ParseResponse_ComputesOffsetAndDelay()
    {
        var t1 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var t4 = t1.AddMilliseconds(40);   // 40 ms round trip
        var t2 = t1.AddSeconds(1.000);     // server ~1 s ahead
        var t3 = t1.AddSeconds(1.010);     // 10 ms server processing

        var r = NtpPacket.ParseResponse(Response(t2, t3), t1, t4);

        // offset = ((t2-t1)+(t3-t4))/2 = (1.000 + 0.970)/2 = 0.985
        Assert.Equal(0.985, r.OffsetSeconds, 3);
        // delay  = (t4-t1)-(t3-t2) = 0.040 - 0.010 = 0.030
        Assert.Equal(0.030, r.RoundTripSeconds, 3);
        Assert.Equal(1, r.Stratum);
    }

    [Fact]
    public void ParseResponse_NullBuffer_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            NtpPacket.ParseResponse(null!, DateTime.UtcNow, DateTime.UtcNow));

    [Fact]
    public void ParseResponse_ShortBuffer_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            NtpPacket.ParseResponse(new byte[NtpPacket.Size - 1], DateTime.UtcNow, DateTime.UtcNow));

    [Theory]
    [InlineData(3)] // client mode echoed back
    [InlineData(5)] // broadcast
    public void ParseResponse_WrongMode_Throws(int mode) =>
        Assert.Throws<InvalidDataException>(() =>
            NtpPacket.ParseResponse(Response(DateTime.UtcNow, DateTime.UtcNow, mode: mode),
                DateTime.UtcNow, DateTime.UtcNow));

    [Theory]
    [InlineData(2)] // too old
    [InlineData(5)] // too new
    public void ParseResponse_BadVersion_Throws(int vn) =>
        Assert.Throws<InvalidDataException>(() =>
            NtpPacket.ParseResponse(Response(DateTime.UtcNow, DateTime.UtcNow, vn: vn),
                DateTime.UtcNow, DateTime.UtcNow));

    [Fact]
    public void ParseResponse_LeapIndicatorAlarm_Throws() =>
        Assert.Throws<InvalidDataException>(() =>
            NtpPacket.ParseResponse(Response(DateTime.UtcNow, DateTime.UtcNow, li: 3),
                DateTime.UtcNow, DateTime.UtcNow));

    [Theory]
    [InlineData(0)]  // unspecified / kiss-o'-death
    [InlineData(16)] // out of stratum-1..15 range
    public void ParseResponse_BadStratum_Throws(int stratum) =>
        Assert.Throws<InvalidDataException>(() =>
            NtpPacket.ParseResponse(Response(DateTime.UtcNow, DateTime.UtcNow, stratum: stratum),
                DateTime.UtcNow, DateTime.UtcNow));

    [Fact]
    public void BuildClientRequest_HasClientModeAndStampsT1()
    {
        var buf = NtpPacket.BuildClientRequest(out var t1);
        Assert.Equal(NtpPacket.Size, buf.Length);
        Assert.Equal(0x23, buf[0]); // LI=0, VN=4, Mode=3 (client)
        Assert.Equal(DateTimeKind.Utc, t1.Kind);
    }
}
