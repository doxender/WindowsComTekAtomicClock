// Tests for the status-bar formatter — a pure function that renders the
// cached SyncStatus + service state into the human-readable "Last sync: …"
// string shown in the bottom bar.

using ComTekAtomicClock.Shared.Ipc;
using ComTekAtomicClock.UI.Services;
using ComTekAtomicClock.UI.ViewModels;

namespace ComTekAtomicClock.Tests;

public class FormatLastSyncTests
{
    [Fact]
    public void ServiceNotRunning_SaysSoRegardlessOfStatus()
    {
        var s = MainWindowViewModel.FormatLastSync(null, ServiceLifecycleState.NotInstalled);
        Assert.Equal("Last sync: service not running", s);
    }

    [Fact]
    public void RunningButNoStatusYet_ShowsPending()
    {
        var s = MainWindowViewModel.FormatLastSync(null, ServiceLifecycleState.Running);
        Assert.Equal("Last sync: pending…", s);
    }

    [Fact]
    public void Success_ShowsCorrectionAmount()
    {
        var status = new SyncStatus(DateTimeOffset.UtcNow, true, "time.nist.gov", 0.25, null);
        var s = MainWindowViewModel.FormatLastSync(status, ServiceLifecycleState.Running);
        Assert.StartsWith("Last sync:", s);
        Assert.Contains("corrected", s);
    }

    [Fact]
    public void Failure_ShowsTrimmedError()
    {
        var status = new SyncStatus(DateTimeOffset.UtcNow, false, "time.nist.gov", null, "boom");
        var s = MainWindowViewModel.FormatLastSync(status, ServiceLifecycleState.Running);
        Assert.StartsWith("Last sync failed:", s);
        Assert.Contains("boom", s);
    }
}
