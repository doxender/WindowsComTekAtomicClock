// ComTekAtomicClock.Shared.Ipc
//
// Contract types shared between the unprivileged UI process and the
// LocalSystem Worker Service that owns SetSystemTime. Communication is
// over a single named pipe whose name is defined in PipeNames below;
// the pipe ACL restricts access to interactive local users (per
// requirements.txt § 2.4). Messages are JSON-serialized records
// versioned by Envelope.SchemaVersion so old clients and new servers
// can detect each other.
//
// This file is the SCAFFOLD: just enough type definitions to compile
// against. Real IpcServer / IpcClient implementations land in
// ComTekAtomicClock.Service and ComTekAtomicClock.UI in a follow-up.

namespace ComTekAtomicClock.Shared.Ipc;

/// <summary>
/// Well-known names used at the IPC layer.
/// </summary>
public static class PipeNames
{
    /// <summary>
    /// The single full-duplex named pipe between UI and Service.
    /// Server side: <see cref="System.IO.Pipes.NamedPipeServerStream"/>
    /// in the Service. Client side: <see cref="System.IO.Pipes.NamedPipeClientStream"/>
    /// in the UI.
    /// </summary>
    public const string UiToService = "ComTekAtomicClock.UiToService";
}

/// <summary>
/// Versioning constants for the IPC schema. Bumped whenever the
/// payload shape of any <see cref="IpcMessageType"/> changes in a
/// non-backward-compatible way.
/// </summary>
public static class IpcSchema
{
    public const int CurrentVersion = 1;
}

/// <summary>
/// All message types the UI and Service may exchange. Request/response
/// pairs share the same name with a Request/Response suffix.
/// </summary>
public enum IpcMessageType
{
    /// <summary>UI asks the Service to perform a sync now.</summary>
    SyncNowRequest,
    /// <summary>Service responds with the result of the sync attempt.</summary>
    SyncNowResponse,

    /// <summary>UI asks for the most recent sync status.</summary>
    LastSyncStatusRequest,
    /// <summary>Service replies with a <see cref="SyncStatus"/> snapshot.</summary>
    LastSyncStatusResponse,

    /// <summary>
    /// Service-initiated push: an offset above the user-configured
    /// threshold (per requirements.txt § 2.5) was detected and the
    /// user has opted in to confirmation. UI responds with
    /// <see cref="ConfirmLargeOffsetResponse"/>.
    /// </summary>
    ConfirmLargeOffsetRequest,
    ConfirmLargeOffsetResponse,

    /// <summary>Service-initiated push: status changed (running, error, etc).</summary>
    StatusChangedNotification,
}

/// <summary>
/// Wrapper around every message exchanged on the pipe. Carries the
/// schema version, the message type discriminator, and the JSON
/// payload as a string. Exact payload shape is determined by
/// <see cref="Type"/>.
/// </summary>
public sealed record IpcEnvelope(
    int SchemaVersion,
    IpcMessageType Type,
    string PayloadJson)
{
    public static IpcEnvelope Create(IpcMessageType type, string payloadJson) =>
        new(IpcSchema.CurrentVersion, type, payloadJson);
}

/// <summary>
/// Snapshot of the Service's most recent sync attempt. Returned as the
/// payload of <see cref="IpcMessageType.LastSyncStatusResponse"/> and
/// <see cref="IpcMessageType.SyncNowResponse"/>.
/// </summary>
public sealed record SyncStatus(
    /// <summary>UTC moment when the attempt completed (or failed).</summary>
    DateTimeOffset AttemptedAtUtc,
    /// <summary>True iff the sync succeeded.</summary>
    bool Success,
    /// <summary>NIST hostname that answered (or last-tried, on failure).</summary>
    string? ServerHost,
    /// <summary>
    /// Signed offset between the previous system clock and the NIST
    /// reference, in seconds. Positive = system clock was fast,
    /// negative = system clock was slow. Null on failure.
    /// </summary>
    double? OffsetSeconds,
    /// <summary>
    /// Human-readable error description if <see cref="Success"/> is
    /// false. Null on success.
    /// </summary>
    string? ErrorMessage);

/// <summary>
/// Service -> UI: please ask the user whether to apply this large
/// correction. Triggers the toast described in requirements.txt § 2.5.
/// </summary>
public sealed record ConfirmLargeOffsetRequest(
    string ServerHost,
    double OffsetSeconds,
    DateTimeOffset DetectedAtUtc);

/// <summary>UI -> Service reply to <see cref="ConfirmLargeOffsetRequest"/>.</summary>
public sealed record ConfirmLargeOffsetResponse(bool Apply);
