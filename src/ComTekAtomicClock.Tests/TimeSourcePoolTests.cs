// Tests for the stratum-1 pool walk order + the §1.5 host validator.
// The validator is a real security control: it's the only thing stopping
// a hand-edited service.json from pointing the clock-setting service at
// an arbitrary host (see the audit's NtpPacket / installer findings).

using ComTekAtomicClock.Service.Sync;
using ComTekAtomicClock.Shared.Settings;

namespace ComTekAtomicClock.Tests;

public class TimeSourcePoolTests
{
    [Fact]
    public void WalkOrder_AnycastPrimary_YieldsAnycastThenWholePool()
    {
        var walk = TimeSourcePool
            .GetWalkOrder(TimeSource.Boulder, TimeSourcePool.BoulderAnycast)
            .ToList();

        Assert.Equal(TimeSourcePool.BoulderAnycast, walk[0]);
        Assert.Equal(1 + TimeSourcePool.BoulderStratumOne.Count, walk.Count);
        Assert.Equal(walk.Count, walk.Distinct().Count()); // no duplicates
        foreach (var host in TimeSourcePool.BoulderStratumOne)
            Assert.Contains(host, walk);
    }

    [Fact]
    public void WalkOrder_PoolMemberPrimary_ListedFirstAndExactlyOnce()
    {
        var primary = TimeSourcePool.BoulderStratumOne[0];
        var walk = TimeSourcePool.GetWalkOrder(TimeSource.Boulder, primary).ToList();

        Assert.Equal(primary, walk[0]);
        Assert.Equal(TimeSourcePool.BoulderStratumOne.Count, walk.Count); // primary + the other 9
        Assert.Equal(1, walk.Count(h => h == primary));
    }

    [Theory]
    [InlineData("time.nist.gov", true)]        // anycast
    [InlineData("time-a-g.nist.gov", true)]    // pool member
    [InlineData("TIME-A-WWV.NIST.GOV", true)]  // case-insensitive
    [InlineData("evil.example.com", false)]    // off-pool host rejected
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsKnownHost_Boulder(string host, bool expected) =>
        Assert.Equal(expected, TimeSourcePool.IsKnownHost(TimeSource.Boulder, host));

    [Fact]
    public void IsKnownHost_ScopedToSource()
    {
        // a Brazil host is NOT a known Boulder host...
        Assert.False(TimeSourcePool.IsKnownHost(TimeSource.Boulder, "a.ntp.br"));
        // ...but IS known across sources.
        Assert.True(TimeSourcePool.IsKnownHostAcrossSources("a.ntp.br"));
    }

    [Fact]
    public void Anycast_And_Pool_PerSource()
    {
        Assert.Equal("time.nist.gov", TimeSourcePool.GetAnycast(TimeSource.Boulder));
        Assert.Equal("a.ntp.br", TimeSourcePool.GetAnycast(TimeSource.Brazil));
        Assert.Equal(5, TimeSourcePool.GetPool(TimeSource.Brazil).Count);
        Assert.Equal(10, TimeSourcePool.GetPool(TimeSource.Boulder).Count);
    }
}
