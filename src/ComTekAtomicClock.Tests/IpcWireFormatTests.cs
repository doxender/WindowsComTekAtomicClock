// Tests for the length-prefixed JSON framing on the UI <-> Service pipe.
// The 1 MiB cap and the truncation/disconnect handling are the defensive
// boundary against a malformed or hostile peer on the named pipe.

using System.IO;
using ComTekAtomicClock.Shared.Ipc;

namespace ComTekAtomicClock.Tests;

public class IpcWireFormatTests
{
    [Fact]
    public async Task WriteThenRead_RoundTrips()
    {
        var env = IpcEnvelope.Create(IpcMessageType.LastSyncStatusRequest, "{\"x\":1}");

        using var ms = new MemoryStream();
        await IpcWireFormat.WriteAsync(ms, env, default);
        ms.Position = 0;
        var read = await IpcWireFormat.ReadAsync(ms, default);

        Assert.NotNull(read);
        Assert.Equal(env.Type, read!.Type);
        Assert.Equal(env.SchemaVersion, read.SchemaVersion);
        Assert.Equal(env.PayloadJson, read.PayloadJson);
    }

    [Fact]
    public async Task Read_CleanlyClosedStream_ReturnsNull()
    {
        using var ms = new MemoryStream();
        Assert.Null(await IpcWireFormat.ReadAsync(ms, default));
    }

    [Fact]
    public async Task Read_NegativeLengthPrefix_Throws()
    {
        using var ms = new MemoryStream(BitConverter.GetBytes(-1));
        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await IpcWireFormat.ReadAsync(ms, default));
    }

    [Fact]
    public async Task Read_LengthOverCap_Throws()
    {
        using var ms = new MemoryStream(BitConverter.GetBytes(IpcWireFormat.MaxPayloadBytes + 1));
        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await IpcWireFormat.ReadAsync(ms, default));
    }

    [Fact]
    public async Task Read_TruncatedPayload_Throws()
    {
        // Prefix claims 10 payload bytes, only 3 follow.
        var buf = new byte[4 + 3];
        BitConverter.GetBytes(10).CopyTo(buf, 0);
        using var ms = new MemoryStream(buf);
        await Assert.ThrowsAsync<EndOfStreamException>(
            async () => await IpcWireFormat.ReadAsync(ms, default));
    }

    [Fact]
    public async Task Write_OversizePayload_Throws()
    {
        var big = new string('x', IpcWireFormat.MaxPayloadBytes + 10);
        var env = IpcEnvelope.Create(IpcMessageType.LastSyncStatusResponse, big);
        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await IpcWireFormat.WriteAsync(ms, env, default));
    }
}
