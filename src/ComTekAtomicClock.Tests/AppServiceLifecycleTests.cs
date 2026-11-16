// Regression tests for the v1.1.8 fix: App.OnExit stops the time-sync
// service only when no other copy of the UI is still running. Before, any
// copy's exit stopped it. That matches the event log on Dan's box: a fresh
// UI started the service at 2026-11-15 21:41:06, the service was stopped at
// 21:41:13, and that UI kept running under the "installed but not running"
// banner until the next morning. (The second copy is inferred from the
// log; nothing recorded which process sent the stop.)

using ComTekAtomicClock.UI;

namespace ComTekAtomicClock.Tests;

public class AppServiceLifecycleTests
{
    [Fact]
    public void OnlyThisCopyRunning_StopsTheService()
    {
        Assert.True(App.ShouldStopServiceOnExit(100, new[] { 100 }));
    }

    [Fact]
    public void AnotherCopyStillRunning_KeepsTheService()
    {
        Assert.False(App.ShouldStopServiceOnExit(100, new[] { 100, 200 }));
    }

    [Fact]
    public void ProcessListUnreadable_KeepsTheService()
    {
        Assert.False(App.ShouldStopServiceOnExit(100, null));
    }

    [Fact]
    public void EmptyProcessList_StopsTheService()
    {
        Assert.True(App.ShouldStopServiceOnExit(100, Array.Empty<int>()));
    }
}
