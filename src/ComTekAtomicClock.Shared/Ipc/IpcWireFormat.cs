// ComTekAtomicClock.Shared.Ipc.IpcWireFormat
//
// Length-prefixed JSON framing for the UI <-> Service named pipe.
//
// Wire layout per message:
//   [4 bytes little-endian int32 payload length] [N bytes UTF-8 JSON]
//
// Both sides use ReadAsync/WriteAsync from this class so the framing
// stays in lock-step. A 1 MiB cap on payload size is enforced
// defensively — actual payloads (sync status, confirmation requests)
// are well under 1 KiB.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComTekAtomicClock.Shared.Ipc;

public static class IpcWireFormat
{
    /// <summary>Hard upper bound on a single IPC frame's payload, in bytes.</summary>
    public const int MaxPayloadBytes = 1 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOpts = BuildJsonOptions();

    private static JsonSerializerOptions BuildJsonOptions()
    {
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        opts.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return opts;
    }

    /// <summary>
    /// Read one envelope from <paramref name="stream"/>. Returns null when
    /// the stream is closed cleanly between messages (i.e., the peer
    /// disconnected gracefully). Throws on a truncated message or on a
    /// malformed length prefix.
    /// </summary>
    public static async Task<IpcEnvelope?> ReadAsync(Stream stream, CancellationToken ct)
    {
        var lenBuf = new byte[4];
        var read = await ReadExactOrZeroAsync(stream, lenBuf, ct).ConfigureAwait(false);
        if (read == 0) return null; // clean disconnect
        if (read != 4) throw new EndOfStreamException("Truncated length prefix.");

        var len = BitConverter.ToInt32(lenBuf, 0);
        if (len < 0 || len > MaxPayloadBytes)
            throw new InvalidDataException($"IPC frame length out of range: {len}");

        var payload = new byte[len];
        var got = await ReadExactOrZeroAsync(stream, payload, ct).ConfigureAwait(false);
        if (got != len)
            throw new EndOfStreamException(
                $"Truncated payload: expected {len} bytes, got {got}.");

        return JsonSerializer.Deserialize<IpcEnvelope>(payload, JsonOpts);
    }

    /// <summary>Write one envelope to <paramref name="stream"/> and flush.</summary>
    public static async Task WriteAsync(Stream stream, IpcEnvelope envelope, CancellationToken ct)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOpts);
        if (json.Length > MaxPayloadBytes)
            throw new InvalidOperationException(
                $"IPC payload too large: {json.Length} > {MaxPayloadBytes}.");

        var lenBuf = BitConverter.GetBytes(json.Length);
        await stream.WriteAsync(lenBuf.AsMemory(0, 4), ct).ConfigureAwait(false);
        await stream.WriteAsync(json.AsMemory(), ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Read exactly <c>buffer.Length</c> bytes, or 0 if the stream closes
    /// before any byte is read. Throws on partial reads (some bytes read,
    /// then EOF before the buffer is full).
    /// </summary>
    private static async Task<int> ReadExactOrZeroAsync(Stream s, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var n = await s.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct)
                           .ConfigureAwait(false);
            if (n == 0)
            {
                if (offset == 0) return 0;     // clean disconnect at message boundary
                throw new EndOfStreamException(
                    $"Stream closed mid-frame after {offset} of {buffer.Length} bytes.");
            }
            offset += n;
        }
        return offset;
    }

    // ------------------------------------------------------------------
    // Convenience helpers that wrap an arbitrary payload object in an
    // envelope of a given message type.
    // ------------------------------------------------------------------

    /// <summary>Build an envelope around any serializable payload.</summary>
    public static IpcEnvelope WrapPayload<T>(IpcMessageType type, T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        return IpcEnvelope.Create(type, json);
    }

    /// <summary>Deserialize the payload of an envelope as <typeparamref name="T"/>.</summary>
    public static T? UnwrapPayload<T>(IpcEnvelope envelope)
    {
        if (string.IsNullOrEmpty(envelope.PayloadJson))
            return default;
        return JsonSerializer.Deserialize<T>(envelope.PayloadJson, JsonOpts);
    }
}
