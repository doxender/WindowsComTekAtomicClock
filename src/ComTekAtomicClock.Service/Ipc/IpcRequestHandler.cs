// ComTekAtomicClock.Service.Ipc.IpcRequestHandler
//
// Routes incoming UI -> Service requests to the right piece of Service
// state. This is the contract between IpcServer (which owns the pipe
// and the read loop) and the Service's domain logic (sync status,
// confirmation handling, etc.).
//
// The stub implementation in this file just acknowledges every request
// with an empty response and logs receipt. As we wire up SyncWorker
// and the confirmation flow in subsequent commits, the stub gets
// replaced with real handlers — without touching IpcServer.

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
/// Default stub handler. Accepts any envelope, logs it, and returns a
/// matching empty response envelope so the UI's read loop unblocks.
/// Real domain logic (SyncWorker, confirmation flow) will replace this
/// in subsequent commits.
/// </summary>
public sealed class StubIpcRequestHandler : IIpcRequestHandler
{
    private readonly ILogger<StubIpcRequestHandler> _logger;

    public StubIpcRequestHandler(ILogger<StubIpcRequestHandler> logger)
    {
        _logger = logger;
    }

    public Task<IpcEnvelope?> HandleAsync(IpcEnvelope request, CancellationToken ct)
    {
        _logger.LogInformation(
            "IPC request received: type={Type} schemaVersion={SchemaVersion}",
            request.Type, request.SchemaVersion);

        // Match each request type to its corresponding response type with
        // an empty payload. Notification-style messages (no response
        // expected) return null.
        IpcEnvelope? response = request.Type switch
        {
            IpcMessageType.SyncNowRequest          => IpcEnvelope.Create(IpcMessageType.SyncNowResponse,          payloadJson: "{}"),
            IpcMessageType.LastSyncStatusRequest   => IpcEnvelope.Create(IpcMessageType.LastSyncStatusResponse,   payloadJson: "{}"),
            IpcMessageType.ConfirmLargeOffsetResponse => null, // UI -> Service notification, no reply
            _                                       => null,
        };

        return Task.FromResult(response);
    }
}
