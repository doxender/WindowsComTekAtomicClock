// ComTekAtomicClock.Service.Ipc.IpcRequestHandler
//
// Routes incoming UI -> Service requests to the right piece of Service
// state. This is the contract between IpcServer (which owns the pipe
// and the read loop) and the Service's domain logic.

using ComTekAtomicClock.Service.Sync;
using ComTekAtomicClock.Shared.Ipc;
using Microsoft.Extensions.Logging;

namespace ComTekAtomicClock.Service.Ipc;

/// <summary>
/// Server-side request handler. Returns the envelope to send back to
/// the UI, or null if the request is fire-and-forget (no response).
/// </summary>
public interface IIpcRequestHandler
{
    Task<IpcEnvelope?> HandleAsync(IpcEnvelope request, CancellationToken ct);
}

/// <summary>
/// Production handler. Reads live state from <see cref="SyncStateProvider"/>
/// and answers queries with real <see cref="SyncStatus"/> snapshots.
///
/// SyncNowRequest currently still returns the most recent state without
/// forcing an out-of-cycle sync — `Sync now` triggering an immediate
/// SNTP query lands in the next commit alongside the tray menu. The
/// shape of the response is final.
///
/// ConfirmLargeOffset (UI -> Service) is also stubbed pending the
/// toast-driven confirmation flow (§ 2.5).
/// </summary>
public sealed class LiveIpcRequestHandler : IIpcRequestHandler
{
    private readonly ILogger<LiveIpcRequestHandler> _logger;
    private readonly SyncStateProvider _state;

    public LiveIpcRequestHandler(ILogger<LiveIpcRequestHandler> logger, SyncStateProvider state)
    {
        _logger = logger;
        _state = state;
    }

    public Task<IpcEnvelope?> HandleAsync(IpcEnvelope request, CancellationToken ct)
    {
        _logger.LogDebug(
            "IPC request: type={Type} schemaVersion={SchemaVersion}",
            request.Type, request.SchemaVersion);

        switch (request.Type)
        {
            case IpcMessageType.LastSyncStatusRequest:
            {
                var snapshot = _state.Current;
                var env = IpcWireFormat.WrapPayload(IpcMessageType.LastSyncStatusResponse, snapshot);
                return Task.FromResult<IpcEnvelope?>(env);
            }

            case IpcMessageType.SyncNowRequest:
            {
                // For now: return the latest snapshot. A future commit
                // will trigger SyncWorker to perform an immediate
                // out-of-cycle sync and respond with the result of that
                // attempt instead.
                var snapshot = _state.Current;
                var env = IpcWireFormat.WrapPayload(IpcMessageType.SyncNowResponse, snapshot);
                return Task.FromResult<IpcEnvelope?>(env);
            }

            case IpcMessageType.ConfirmLargeOffsetResponse:
            {
                // Notification: UI is telling the Service whether to
                // apply a previously-prompted large correction.
                // Confirmation flow itself is not wired yet (§ 2.5).
                _logger.LogInformation(
                    "Received ConfirmLargeOffsetResponse; confirmation flow not yet implemented.");
                return Task.FromResult<IpcEnvelope?>(null);
            }

            default:
                _logger.LogWarning("Unhandled IPC request type: {Type}", request.Type);
                return Task.FromResult<IpcEnvelope?>(null);
        }
    }
}
