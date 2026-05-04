using StatisticsAnalysisTool.Network;
using StatisticsAnalysisTool.Linux.Runtime;

namespace StatisticsAnalysisTool.Linux.Tests;

public sealed class ActivityRatesParityTests
{
    [Test]
    public async Task TakeSilver_UsesOldTaxAwareLocalAndPartyLogic()
    {
        var entityNames = new EntityNameService();
        var partyGuid = Guid.NewGuid();
        entityNames.SetLocalEntity(1, Guid.NewGuid(), "LocalPlayer");
        entityNames.SetPartyMember(partyGuid, "PartyPlayer");
        entityNames.SetEntity(2, partyGuid, "PartyPlayer");
        var rates = new ActivityRatesService();
        var handler = new ActivityRatesPacketHandler(rates, entityNames);

        await handler.HandleAsync(new EventPacket(62, new Dictionary<byte, object>
        {
            [0] = 1L,
            [2] = 10L,
            [3] = 1_000_000L,
            [5] = 100_000L,
            [6] = 200_000L
        }));
        await handler.HandleAsync(new EventPacket(62, new Dictionary<byte, object>
        {
            [0] = 2L,
            [2] = 10L,
            [3] = 1_000_000L
        }));

        Assert.That(rates.GetSnapshot().Silver, Is.EqualTo(162));
    }

    [Test]
    public async Task ReSpec_RequiresOldCurrentTotalShapeAndTracksPaidSilver()
    {
        var rates = new ActivityRatesService();
        var handler = new ActivityRatesPacketHandler(rates, new EntityNameService());

        await handler.HandleAsync(new EventPacket(84, new Dictionary<byte, object>
        {
            [2] = 100_000L,
            [3] = 200_000L
        }));
        await handler.HandleAsync(new EventPacket(84, new Dictionary<byte, object>
        {
            [0] = new long[] { 0, 1_000_000L },
            [2] = 100_000L,
            [3] = 200_000L
        }));

        var snapshot = rates.GetSnapshot();
        Assert.That(snapshot.ReSpecPoints, Is.EqualTo(10));
        Assert.That(snapshot.PaidSilverForReSpec, Is.EqualTo(20));
    }

    [Test]
    public async Task MightFavorAndFactionStanding_UseOldOffsets()
    {
        var rates = new ActivityRatesService();
        var handler = new ActivityRatesPacketHandler(rates, new EntityNameService());

        await handler.HandleAsync(new EventPacket(494, new Dictionary<byte, object>
        {
            [0] = 100_000L,
            [2] = 900_000L,
            [3] = 200_000L,
            [5] = 800_000L
        }));
        await handler.HandleAsync(new EventPacket(86, new Dictionary<byte, object>
        {
            [0] = 1,
            [1] = 300_000L
        }));

        var snapshot = rates.GetSnapshot();
        Assert.That(snapshot.Might, Is.EqualTo(10));
        Assert.That(snapshot.Favor, Is.EqualTo(20));
        Assert.That(snapshot.FactionStanding, Is.EqualTo(30));
    }
}
