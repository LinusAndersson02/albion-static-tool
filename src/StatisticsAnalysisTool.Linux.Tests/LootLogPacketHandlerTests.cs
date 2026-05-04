using StatisticsAnalysisTool.Linux.Core;
using StatisticsAnalysisTool.Network;
using StatisticsAnalysisTool.Linux.Runtime;

namespace StatisticsAnalysisTool.Linux.Tests;

public sealed class LootLogPacketHandlerTests
{
    [Test]
    public async Task OtherGrabbedLoot_RecordsNonSilverLootFromObservedLooter()
    {
        var (handler, lootLog, _) = CreateHandler();

        await handler.HandleAsync(new EventPacket(277, new Dictionary<byte, object>
        {
            [1] = "FallenPlayer",
            [2] = "OtherLooter",
            [4] = 1841,
            [5] = 2
        }));

        var entry = lootLog.GetEntries().Single();
        Assert.That(entry.LooterName, Is.EqualTo("OtherLooter"));
        Assert.That(entry.SourceName, Is.EqualTo("FallenPlayer"));
        Assert.That(entry.ItemIndex, Is.EqualTo(1841));
        Assert.That(entry.Quantity, Is.EqualTo(2));
    }

    [Test]
    public async Task OtherGrabbedLoot_IgnoresSilver()
    {
        var (handler, lootLog, _) = CreateHandler();

        await handler.HandleAsync(new EventPacket(277, new Dictionary<byte, object>
        {
            [2] = "OtherLooter",
            [3] = true,
            [5] = 1550115
        }));

        Assert.That(lootLog.GetEntries(), Is.Empty);
    }

    [Test]
    public async Task InventoryMoveItem_RecordsLocalContainerPickup()
    {
        var (handler, lootLog, entityNames) = CreateHandler();
        var playerGuid = Guid.NewGuid();
        var interactGuid = Guid.NewGuid();
        var containerGuid = Guid.NewGuid();
        entityNames.SetLocalEntity(1, playerGuid, "LocalPlayer");

        await handler.HandleAsync(new ResponsePacket(2, new Dictionary<byte, object>
        {
            [0] = 1,
            [1] = playerGuid.ToByteArray(),
            [2] = "LocalPlayer",
            [54] = interactGuid.ToByteArray()
        }));
        await handler.HandleAsync(new EventPacket(98, new Dictionary<byte, object>
        {
            [0] = 900,
            [3] = "Chest Guard"
        }));
        await handler.HandleAsync(new EventPacket(99, new Dictionary<byte, object>
        {
            [0] = 900,
            [1] = containerGuid.ToByteArray(),
            [3] = new long[] { 7001 }
        }));
        await handler.HandleAsync(new EventPacket(32, new Dictionary<byte, object>
        {
            [0] = 7001,
            [1] = 3200,
            [2] = 3
        }));

        await handler.HandleAsync(new RequestPacket(30, new Dictionary<byte, object>
        {
            [0] = 0,
            [1] = containerGuid.ToByteArray(),
            [3] = 14,
            [4] = interactGuid.ToByteArray()
        }));

        var entry = lootLog.GetEntries().Single();
        Assert.That(entry.LooterName, Is.EqualTo("LocalPlayer"));
        Assert.That(entry.SourceName, Is.EqualTo("Chest Guard"));
        Assert.That(entry.ItemIndex, Is.EqualTo(3200));
        Assert.That(entry.Quantity, Is.EqualTo(3));
    }

    [Test]
    public async Task InventoryMoveItemAndOtherGrabbedLoot_DeduplicateWithinTwoSeconds()
    {
        var (handler, lootLog, entityNames) = CreateHandler();
        var playerGuid = Guid.NewGuid();
        var interactGuid = Guid.NewGuid();
        var containerGuid = Guid.NewGuid();
        entityNames.SetLocalEntity(1, playerGuid, "LocalPlayer");

        await handler.HandleAsync(new ResponsePacket(2, new Dictionary<byte, object>
        {
            [0] = 1,
            [1] = playerGuid.ToByteArray(),
            [2] = "LocalPlayer",
            [54] = interactGuid.ToByteArray()
        }));
        await handler.HandleAsync(new EventPacket(98, new Dictionary<byte, object> { [0] = 900, [3] = "Chest Guard" }));
        await handler.HandleAsync(new EventPacket(99, new Dictionary<byte, object>
        {
            [0] = 900,
            [1] = containerGuid.ToByteArray(),
            [3] = new long[] { 7001 }
        }));
        await handler.HandleAsync(new EventPacket(32, new Dictionary<byte, object> { [0] = 7001, [1] = 3200, [2] = 3 }));
        await handler.HandleAsync(new RequestPacket(30, new Dictionary<byte, object>
        {
            [0] = 0,
            [1] = containerGuid.ToByteArray(),
            [4] = interactGuid.ToByteArray()
        }));
        await handler.HandleAsync(new EventPacket(277, new Dictionary<byte, object>
        {
            [1] = "Chest Guard",
            [2] = "LocalPlayer",
            [4] = 3200,
            [5] = 3
        }));

        Assert.That(lootLog.GetEntries(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task OtherGrabbedLoot_NormalizesOldMobSourceMarker()
    {
        var (handler, lootLog, _) = CreateHandler();

        await handler.HandleAsync(new EventPacket(277, new Dictionary<byte, object>
        {
            [1] = "KEEPER@MOB_T5",
            [2] = "OtherLooter",
            [4] = 1841,
            [5] = 2
        }));

        Assert.That(lootLog.GetEntries().Single().SourceName, Is.EqualTo("MOB"));
    }

    private static (LootLogPacketHandler Handler, LootLogService LootLog, EntityNameService EntityNames) CreateHandler()
    {
        var entityNames = new EntityNameService();
        entityNames.SetLocalEntity(null, null, "LocalPlayer");
        var gameData = new GameDataIndex(new LinuxAppPaths(), new LinuxSettings());
        var lootLog = new LootLogService();

        return (new LootLogPacketHandler(lootLog, entityNames, gameData, new EstimatedItemValueService()), lootLog, entityNames);
    }
}
