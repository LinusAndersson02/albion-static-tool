using StatisticsAnalysisTool.Network;
using StatisticsAnalysisTool.Linux.Runtime;

namespace StatisticsAnalysisTool.Linux.Tests;

public sealed class DpsMeterParityTests
{
    [Test]
    public async Task HealthUpdate_RecordsDamageTakenHealAndOverhealWithOldOffsets()
    {
        var entityNames = new EntityNameService();
        var localGuid = Guid.NewGuid();
        var partyGuid = Guid.NewGuid();
        entityNames.SetLocalEntity(1, localGuid, "LocalPlayer");
        entityNames.SetPartyMember(partyGuid, "PartyPlayer");
        entityNames.SetEntity(20, partyGuid, "PartyPlayer");
        var meter = new DpsMeterService();
        var handler = new DpsMeterPacketHandler(meter, entityNames);

        await handler.HandleAsync(new EventPacket(6, new Dictionary<byte, object>
        {
            [0] = 20L,
            [2] = -123d,
            [3] = 877d,
            [6] = 1L,
            [7] = 101
        }));
        await handler.HandleAsync(new EventPacket(6, new Dictionary<byte, object>
        {
            [0] = 1L,
            [2] = -50d,
            [3] = 950d,
            [6] = 99L
        }));
        await handler.HandleAsync(new EventPacket(6, new Dictionary<byte, object>
        {
            [0] = 20L,
            [2] = 25d,
            [3] = 900d,
            [6] = 1L
        }));
        await handler.HandleAsync(new EventPacket(6, new Dictionary<byte, object>
        {
            [0] = 20L,
            [2] = 25d,
            [3] = 900d,
            [6] = 1L
        }));

        var snapshot = meter.GetSnapshot();
        var local = snapshot.Entries.Single(x => x.EntityId == 1);
        Assert.That(local.Damage, Is.EqualTo(123));
        Assert.That(local.Heal, Is.EqualTo(25));
        Assert.That(local.Overheal, Is.EqualTo(25));
        Assert.That(local.TakenDamage, Is.EqualTo(50));
        Assert.That(snapshot.TotalOverheal, Is.EqualTo(25));
    }

    [Test]
    public async Task HealthUpdates_BatchUsesTypedLegacyModel()
    {
        var entityNames = new EntityNameService();
        entityNames.SetLocalEntity(1, Guid.NewGuid(), "LocalPlayer");
        var meter = new DpsMeterService();
        var handler = new DpsMeterPacketHandler(meter, entityNames);

        await handler.HandleAsync(new EventPacket(7, new Dictionary<byte, object>
        {
            [0] = 99L,
            [2] = new double[] { -10, -20 },
            [3] = new double[] { 90, 70 },
            [6] = new long[] { 1, 1 },
            [7] = new short[] { 100, 101 }
        }));

        Assert.That(meter.GetSnapshot().Entries.Single().Damage, Is.EqualTo(30));
    }
}
