// ComTekAtomicClock.UI.Services.IpcClient
//
// Client side of the named pipe described in requirements.txt § 2.4.
// Connects to the LocalSystem Service over the per-machine pipe
// PipeNames.UiToService and exposes SendRequestAsync for the UI's
// view-models / services to call.
//
// This is the first-cut implementation: strict request/response, one
// in flight at a time. When we implement the ConfirmLargeOffset toast
// flow (§ 2.5) we'll extend it with a server-push read loop and a
// NotificationReceived event.

using System.IO.Pipes;
using System.Runtime.Versioning;
using ComTekAtomicClock.Shared.Ipc;

namespace ComTekAtomicClock.UI.Services;

[SupportedOSPlatform("windows")]
public sealed class IpcClient : IAsyncDisposable
{
    /// <summary>Default time to wait for the Service pipe to appear.</summary>
    public const int DefaultConnectTimeoutMs = 5_000;

    private NamedPipeClientStream? _pipe;
    private readonly SemaphoreSlim _writeLock = new(initialCount: 1, maxCount: 1);

    /// <summary>True iff we have an open, connected pipe to the Service.</summary>
    public bool IsConnected => _pipe?.IsConnected ?? false;

    /// <summary>
    /// Connect to <see cref="PipeNames.UiToService"/> on the local
    /// machine. Throws <see cref="TimeoutException"/> if the Service
    /// is not running (so the caller can drop into the §1.9
    /// degraded-mode UX). Any prior connection is replaced.
    /// </summary>
    public async Task ConnectAsync(int timeoutMs = DefaultConnectTimeoutMs,
                                   CancellationToken ct = default)
    {
        await DisposeCurrentAsync().ConfigureAwait(false);

        var pipe = new NamedPipeClientStream(
            serverName: ".",
            pipeName: PipeNames.UiToService,
            direction: PipeDirection.InOut,
            options: PipeOptions.Asynchronous);

        try
        {
            await pipe.ConnectAsync(timeoutMs, ct).ConfigureAwait(false);
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        _pipe = pipe;
    }

    /// <summary>
    /// Send <paramref name="request"/> and await the matching response.
    /// The write lock serializes calls so a concurrent caller can't
    /// interleave its bytes with ours mid-frame. Returns null if the
    /// Service replies with no body (notification-style request).
    /// </summary>
    public async Task<IpcEnvelope?> SendRequestAsync(IpcEnvelope request, CancellationToken ct)
    {
        if (_pipe is null || !_pipe.IsConnected)
            throw new InvalidOperationException(
                "IpcClient is not connected. Call ConnectAsync first.");

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await IpcWireFormat.WriteAsync(_pipe, request, ct).ConfigureAwait(false);
            return await IpcWireFormat.ReadAsync(_pipe, ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeCurrentAsync().ConfigureAwait(false);
        _writeLock.Dispose();
    }

    private async Task DisposeCurrentAsync()
    {
        if (_pipe != null)
        {
            try { await _pipe.DisposeAsync().ConfigureAwait(false); }
            catch { /* best effort */ }
            _pipe = null;
        }
    }
}
