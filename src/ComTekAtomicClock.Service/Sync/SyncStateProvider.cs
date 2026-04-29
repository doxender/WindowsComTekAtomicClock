// ComTekAtomicClock.Service.Sync.SyncStateProvider
//
// Thread-safe holder for the most recent SyncStatus. Updated by
// SyncWorker after each sync attempt; read by IpcRequestHandler
// when the UI asks for last-sync status.

using ComTekAtomicClock.Shared.Ipc;

namespace ComTekAtomicClock.Service.Sync;

/// <summary>
/// Singleton (DI-registered) holder for the latest <see cref="SyncStatus"/>.
/// Initial state is "no sync attempted yet" (success=false, server null,
/// offset null, error null).
/// </summary>
public sealed class SyncStateProvider
{
    private readonly object _lock = new();
    private SyncStatus _current = new(
        AttemptedAtUtc: DateTimeOffset.MinValue,
        Success: false,
        ServerHost: null,
        OffsetSeconds: null,
        ErrorMessage: null);

    /// <summary>Snapshot of the latest sync result.</summary>
    public SyncStatus Current
    {
        get { lock (_lock) return _current; }
    }

    /// <summary>Replace the held status (called by SyncWorker after each attempt).</summary>
    public void Update(SyncStatus next)
    {
        lock (_lock) _current = next;
    }
}
