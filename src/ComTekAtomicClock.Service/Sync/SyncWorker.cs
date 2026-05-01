// ComTekAtomicClock.Service.Sync.SyncWorker
//
// Periodically queries the configured stratum-1 pool (NIST or NTP.br
// per ServiceConfig.TimeSource, v0.0.36+) for the canonical time,
// applies the resulting correction to the Windows system clock via
// SetSystemTime, and updates SyncStateProvider so the UI can read
// the most recent result over IPC.
//
// Per requirements.txt § 1.5, § 1.6, § 2.5:
//   - SNTP/v4 over UDP/123 to a stratum-1 host.
//   - Walk: primary anycast for the configured source, then the
//     rest of that source's pool randomized.
//   - Default 1-hour interval, configurable 15 min - 24 hr in service.json.
//   - Honor the >= 4 s/server poll-interval rule (NIST AUP; NTP.br
//     AUP also asks for ≥ 1 s, so 4 s satisfies both).
//   - On total failure (every server tried), log Warning, sleep one
//     full interval, retry. Never falls back to a server outside
//     the configured source's pool.
//
// Confirmation flow for large offsets (§ 2.5) is NOT implemented in
// this commit; corrections above the threshold are applied
// immediately and logged at Warning. The toast-confirmation flow
// lands in a follow-up that adds the IPC notification path and the
// UI's ToastNotification handler.

using System.Net.Sockets;
using System.Runtime.Versioning;
using ComTekAtomicClock.Shared.Ipc;
using ComTekAtomicClock.Shared.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ComTekAtomicClock.Service.Sync;

[SupportedOSPlatform("windows")]
public sealed class SyncWorker : BackgroundService
{
    private readonly ILogger<SyncWorker> _logger;
    private readonly SyncStateProvider _state;

    /// <summary>SNTP server port per RFC 4330.</summary>
    private const int NtpPort = 123;

    /// <summary>Per-server query timeout. Conservative for slow links.</summary>
    private static readonly TimeSpan PerServerTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Minimum poll interval per server, per § 1.5. NIST AUP requires
    /// ≥ 4 s; NTP.br AUP requires ≥ 1 s. 4 s satisfies both.
    /// </summary>
    private static readonly TimeSpan MinPerServerPoll = TimeSpan.FromSeconds(4);

    public SyncWorker(ILogger<SyncWorker> logger, SyncStateProvider state)
    {
        _logger = logger;
        _state = state;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncWorker starting.");

        // First sync immediately on service start so the clock is
        // accurate before the user touches anything.
        await PerformSyncAttemptAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = LoadIntervalFromConfig();
            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }

            await PerformSyncAttemptAsync(stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("SyncWorker stopped.");
    }

    /// <summary>
    /// One sync attempt: walk the NIST pool, send SNTP, apply the
    /// correction on success. Updates <see cref="SyncStateProvider"/>.
    /// </summary>
    private async Task PerformSyncAttemptAsync(CancellationToken ct)
    {
        var config = SettingsStore.LoadServiceConfig();
        var source = config.TimeSource;

        // v0.0.36: pick primary based on the configured TimeSource.
        // If the user has a hand-edited SyncServer that's a member of
        // the active source's pool, honor it; otherwise fall back to
        // that source's anycast.
        var primary = TimeSourcePool.IsKnownHost(source, config.SyncServer)
            ? config.SyncServer
            : TimeSourcePool.GetAnycast(source);

        var thresholdSeconds = Math.Max(0, config.LargeOffsetThresholdSeconds);

        Exception? lastError = null;
        string? lastTried = null;

        foreach (var host in TimeSourcePool.GetWalkOrder(source, primary))
        {
            if (ct.IsCancellationRequested) return;
            lastTried = host;

            try
            {
                var result = await QueryServerAsync(host, ct).ConfigureAwait(false);

                // Apply.
                var newUtc = DateTime.UtcNow.AddSeconds(result.OffsetSeconds);
                var ok = SystemTime.TrySetUtcNow(newUtc, out var werr);
                if (!ok)
                {
                    _logger.LogError(
                        "SetSystemTime failed (Win32 error {Err}) after a successful query to {Host}; offset={Offset:F3}s.",
                        werr, host, result.OffsetSeconds);
                    _state.Update(new SyncStatus(
                        AttemptedAtUtc: DateTimeOffset.UtcNow,
                        Success:       false,
                        ServerHost:    host,
                        OffsetSeconds: result.OffsetSeconds,
                        ErrorMessage:  $"SetSystemTime failed (Win32 error {werr})."));
                    return;
                }

                var absOffset = Math.Abs(result.OffsetSeconds);
                if (absOffset >= thresholdSeconds && thresholdSeconds > 0)
                {
                    _logger.LogWarning(
                        "Large clock correction applied: {Offset:F3} s from {Host} (stratum {Stratum}, RTT {Rtt:F3}s). Threshold is {Threshold}s.",
                        result.OffsetSeconds, host, result.Stratum, result.RoundTripSeconds, thresholdSeconds);
                }
                else
                {
                    _logger.LogInformation(
                        "Clock synced: {Offset:F3} s from {Host} (stratum {Stratum}, RTT {Rtt:F3}s).",
                        result.OffsetSeconds, host, result.Stratum, result.RoundTripSeconds);
                }

                _state.Update(new SyncStatus(
                    AttemptedAtUtc: DateTimeOffset.UtcNow,
                    Success:       true,
                    ServerHost:    host,
                    OffsetSeconds: result.OffsetSeconds,
                    ErrorMessage:  null));
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogDebug(ex, "Sync to {Host} failed: {Msg}", host, ex.Message);
                // Honor NIST minimum poll interval before trying the next server.
                try { await Task.Delay(MinPerServerPoll, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        // Walked the whole pool, nothing answered.
        _logger.LogWarning(
            "Sync failed against the entire {Source} pool (last tried {Host}): {Error}",
            source, lastTried, lastError?.Message ?? "(no exception)");
        _state.Update(new SyncStatus(
            AttemptedAtUtc: DateTimeOffset.UtcNow,
            Success:       false,
            ServerHost:    lastTried,
            OffsetSeconds: null,
            ErrorMessage:  $"All {source} servers in the pool failed. Last error: {lastError?.Message ?? "(unknown)"}"));
    }

    /// <summary>
    /// Send one SNTP query to <paramref name="host"/> and parse the
    /// response. Throws on timeout, DNS failure, or a malformed
    /// response.
    /// </summary>
    private static async Task<SntpResult> QueryServerAsync(string host, CancellationToken ct)
    {
        using var udp = new UdpClient();
        udp.Client.ReceiveTimeout = (int)PerServerTimeout.TotalMilliseconds;
        udp.Client.SendTimeout    = (int)PerServerTimeout.TotalMilliseconds;

        var req = NtpPacket.BuildClientRequest(out var t1Utc);

        // Connect (in UDP, just sets the default destination) and send.
        await udp.Client.ConnectAsync(host, NtpPort, ct).ConfigureAwait(false);
        await udp.SendAsync(req, ct).ConfigureAwait(false);

        // Receive with our own timeout via a CTS so we don't block forever.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(PerServerTimeout);

        UdpReceiveResult rx;
        try
        {
            rx = await udp.ReceiveAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"SNTP query to {host} timed out after {PerServerTimeout.TotalSeconds}s.");
        }

        var t4Utc = DateTime.UtcNow;
        return NtpPacket.ParseResponse(rx.Buffer, t1Utc, t4Utc);
    }

    /// <summary>
    /// Read the current sync interval from service.json. Clamps to
    /// the configured range [15 min, 24 hr] per § 1.5.
    /// </summary>
    private static TimeSpan LoadIntervalFromConfig()
    {
        var config = SettingsStore.LoadServiceConfig();
        var iv = config.SyncInterval;

        var minIv = TimeSpan.FromMinutes(15);
        var maxIv = TimeSpan.FromHours(24);
        if (iv < minIv) iv = minIv;
        if (iv > maxIv) iv = maxIv;
        return iv;
    }
}
