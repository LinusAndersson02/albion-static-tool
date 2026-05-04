using StatisticsAnalysisTool.Linux.Runtime;

namespace StatisticsAnalysisTool.Linux.Tests;

public sealed class SessionTimelineServiceTests
{
    [Test]
    public void SilverLootSplit_AddsEntriesForSoloPlayer()
    {
        var timeline = new SessionTimelineService();
        timeline.SetLocalPlayer("LocalPlayer");

        timeline.AddSilverCheckpoint(100_000, null);

        var firstSnapshot = timeline.GetSnapshot();
        Assert.That(firstSnapshot.SplitSilver, Is.EqualTo(100_000));
        Assert.That(firstSnapshot.Shares.Single().PlayerName, Is.EqualTo("LocalPlayer"));
        Assert.That(firstSnapshot.Shares.Single().Silver, Is.EqualTo(100_000));

        timeline.AddSilverCheckpoint(1_000_000, null);

        var secondSnapshot = timeline.GetSnapshot();
        Assert.That(secondSnapshot.LatestSilver, Is.EqualTo(1_000_000));
        Assert.That(secondSnapshot.SplitSilver, Is.EqualTo(1_100_000));
        Assert.That(secondSnapshot.Shares.Single().PlayerName, Is.EqualTo("LocalPlayer"));
        Assert.That(secondSnapshot.Shares.Single().Silver, Is.EqualTo(1_100_000));
    }
}
