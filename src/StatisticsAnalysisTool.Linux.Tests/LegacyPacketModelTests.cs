using StatisticsAnalysisTool.Network.Legacy;

namespace StatisticsAnalysisTool.Linux.Tests;

public sealed class LegacyPacketModelTests
{
    [Test]
    public void LegacyPacketCodes_MatchBeforeValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That((short)LegacyAlbionEventCode.AttachItemContainer, Is.EqualTo(99));
            Assert.That((short)LegacyAlbionOperationCode.InventoryMoveItem, Is.EqualTo(30));
            Assert.That((short)LegacyAlbionEventCode.MightAndFavorReceivedEvent, Is.EqualTo(494));
            Assert.That((short)LegacyAlbionOperationCode.GetCharacterEquipment, Is.EqualTo(143));
        });
    }

    [Test]
    public void GetCharacterEquipment_UsesLegacyOffsets()
    {
        var guid = Guid.NewGuid();
        var value = LegacyGetCharacterEquipmentResponse.FromParameters(new Dictionary<byte, object>
        {
            [0] = guid.ToByteArray(),
            [1] = new short[] { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110 },
            [2] = new short[] { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 },
            [3] = 1234.5d
        });

        Assert.That(value.Guid, Is.EqualTo(guid));
        Assert.That(value.Equipment, Is.EqualTo(new[] { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110 }));
        Assert.That(value.ItemPower, Is.EqualTo(1234.5d));
    }

    [Test]
    public void NewCharacter_UsesLegacyOffsets()
    {
        var guid = Guid.NewGuid();
        var value = LegacyNewCharacterEvent.FromParameters(new Dictionary<byte, object>
        {
            [0] = 42,
            [1] = "InspectMe",
            [7] = guid.ToByteArray(),
            [8] = "Guild",
            [40] = new int[] { 201, 202, 203, 204, 205, 206, 207, 208, 209, 210 }
        });

        Assert.Multiple(() =>
        {
            Assert.That(value.ObjectId, Is.EqualTo(42));
            Assert.That(value.Name, Is.EqualTo("InspectMe"));
            Assert.That(value.Guid, Is.EqualTo(guid));
            Assert.That(value.GuildName, Is.EqualTo("Guild"));
            Assert.That(value.Equipment, Is.EqualTo(new[] { 201, 202, 203, 204, 205, 206, 207, 208, 209, 210 }));
        });
    }

    [Test]
    public void CharacterEquipmentChanged_UsesLegacyEquipmentAndSpellOffsets()
    {
        var value = LegacyCharacterEquipmentChangedEvent.FromParameters(new Dictionary<byte, object>
        {
            [0] = 77L,
            [2] = new short[] { 301, 302, 303, 304, 305, 306, 307, 308, 309, 310 },
            [7] = new short[] { 1, 2, 3, 4, 5, 6, -1, -1, -1, -1, -1, -1, 12, 13 }
        });

        Assert.That(value.ObjectId, Is.EqualTo(77));
        Assert.That(value.Equipment, Is.EqualTo(new[] { 301, 302, 303, 304, 305, 306, 307, 308, 309, 310 }));
        Assert.That(value.Spells.Take(6), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
    }

    [Test]
    public void PartyJoined_PairsGuidByteArrayAndNames()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var guidBytes = first.ToByteArray().Concat(second.ToByteArray()).ToArray();

        var value = LegacyPartyJoinedEvent.FromParameters(new Dictionary<byte, object>
        {
            [5] = guidBytes,
            [6] = new object[] { "One", "Two" }
        });

        Assert.That(value.PartyUsers, Has.Count.EqualTo(2));
        Assert.That(value.PartyUsers[first], Is.EqualTo("One"));
        Assert.That(value.PartyUsers[second], Is.EqualTo("Two"));
    }

    [Test]
    public void MightAndFavor_UsesOldBaseOffsets()
    {
        var value = LegacyMightAndFavorReceivedEvent.FromParameters(new Dictionary<byte, object>
        {
            [0] = 100_000L,
            [2] = 900_000L,
            [3] = 200_000L,
            [5] = 800_000L
        });

        Assert.That(value.MightInternal, Is.EqualTo(100_000L));
        Assert.That(value.FavorInternal, Is.EqualTo(200_000L));
    }

    [Test]
    public void HealthUpdates_UsesOldIndexedOffsets()
    {
        var value = LegacyHealthUpdatesEvent.FromParameters(new Dictionary<byte, object>
        {
            [0] = 20L,
            [2] = new double[] { -10, 25 },
            [3] = new double[] { 90, 100 },
            [6] = new long[] { 1, 2 },
            [7] = new short[] { 101, 102 }
        });

        Assert.That(value.Updates, Has.Count.EqualTo(2));
        Assert.That(value.Updates[0], Is.EqualTo(new LegacyHealthUpdate(20, -10, 90, 1, 101)));
        Assert.That(value.Updates[1], Is.EqualTo(new LegacyHealthUpdate(20, 25, 100, 2, 102)));
    }
}
