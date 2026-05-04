using StatisticsAnalysisTool.Linux.Core;
using StatisticsAnalysisTool.Network;
using StatisticsAnalysisTool.Linux.Runtime;

namespace StatisticsAnalysisTool.Linux.Tests;

public sealed class PartyLegacyParityTests
{
    [Test]
    public async Task InspectEquipment_IsNotOverwrittenByFallbackSnapshotCandidate()
    {
        var guid = Guid.NewGuid();
        var entityNames = new EntityNameService();
        var party = new PartyService(new GameDataIndex(new LinuxAppPaths(), new LinuxSettings()));
        party.SetParty(new Dictionary<Guid, string> { [guid] = "Inspected" });
        var handler = new EntityNameResponseHandler(entityNames, party);

        await handler.HandleAsync(new ResponsePacket(143, new Dictionary<byte, object>
        {
            [0] = guid.ToByteArray(),
            [1] = new short[] { 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010 },
            [3] = 1200d
        }));
        await handler.HandleAsync(new ResponsePacket(364, new Dictionary<byte, object>
        {
            [0] = new Dictionary<byte, object>
            {
                [0] = guid.ToByteArray(),
                [1] = "Inspected",
                [2] = new short[] { 9001, 9002, 9003, 9004, 9005, 9006, 9007, 9008, 9009, 9010 },
                [3] = 1300d
            }
        }));

        var member = party.GetSnapshot().Members.Single(x => x.Guid == guid);
        Assert.That(member.Equipment.Select(x => x.ItemIndex), Is.EqualTo(new[] { 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010 }));
        Assert.That(member.AverageItemPower, Is.EqualTo(1300d));
    }

    [Test]
    public async Task InspectEquipment_MergesIntoVisiblePartyMemberByResolvedName()
    {
        var guid = Guid.NewGuid();
        var entityNames = new EntityNameService();
        entityNames.SetGuidName(guid, "Inspected");
        var party = new PartyService(new GameDataIndex(new LinuxAppPaths(), new LinuxSettings()));
        party.AddPartyMember(null, "Inspected");
        var handler = new EntityNameResponseHandler(entityNames, party);

        await handler.HandleAsync(new ResponsePacket(143, new Dictionary<byte, object>
        {
            [0] = guid.ToByteArray(),
            [1] = new short[] { 1101, 1102, 1103, 1104, 1105, 1106, 1107, 1108, 1109, 1110 },
            [3] = 1200d
        }));

        var member = party.GetSnapshot().Members.Single();
        Assert.That(member.Guid, Is.EqualTo(guid));
        Assert.That(member.Name, Is.EqualTo("Inspected"));
        Assert.That(member.Equipment.Select(x => x.ItemIndex), Is.EqualTo(new[] { 1101, 1102, 1103, 1104, 1105, 1106, 1107, 1108, 1109, 1110 }));
        Assert.That(member.AverageItemPower, Is.EqualTo(1200d));
    }

    [Test]
    public async Task InspectEquipment_EmptySlotPayloadDoesNotClearKnownGear()
    {
        var guid = Guid.NewGuid();
        var entityNames = new EntityNameService();
        var party = new PartyService(new GameDataIndex(new LinuxAppPaths(), new LinuxSettings()));
        party.SetParty(new Dictionary<Guid, string> { [guid] = "Inspected" });
        var handler = new EntityNameResponseHandler(entityNames, party);

        await handler.HandleAsync(new ResponsePacket(143, new Dictionary<byte, object>
        {
            [0] = guid.ToByteArray(),
            [1] = new short[] { 1201, 1202, 1203, 1204, 1205, 1206, 1207, 1208, 1209, 1210 },
            [3] = 1200d
        }));
        await handler.HandleAsync(new ResponsePacket(143, new Dictionary<byte, object>
        {
            [0] = guid.ToByteArray(),
            [1] = new short[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            [3] = 1200d
        }));

        var member = party.GetSnapshot().Members.Single(x => x.Guid == guid);
        Assert.That(member.Equipment.Select(x => x.ItemIndex), Is.EqualTo(new[] { 1201, 1202, 1203, 1204, 1205, 1206, 1207, 1208, 1209, 1210 }));
    }
}
