using StatisticsAnalysisTool.Network;
using StatisticsAnalysisTool.Network.Legacy;

namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed class LootLogPacketHandler : PacketHandler<object>
{
    private static readonly TimeSpan DeduplicationWindow = TimeSpan.FromSeconds(2);

    private readonly LootLogService _lootLog;
    private readonly EntityNameService _entityNames;
    private readonly GameDataIndex _gameData;
    private readonly EstimatedItemValueService _values;
    private readonly object _sync = new();
    private readonly Dictionary<long, string> _lootBodies = [];
    private readonly Dictionary<long, LegacyDiscoveredItem> _discoveredItems = [];
    private DedupedLootRecord? _lastLootedItem;
    private LegacyAttachItemContainerEvent? _currentContainer;
    private Guid? _localInteractGuid;

    public LootLogPacketHandler(
        LootLogService lootLog,
        EntityNameService entityNames,
        GameDataIndex gameData,
        EstimatedItemValueService values)
    {
        _lootLog = lootLog;
        _entityNames = entityNames;
        _gameData = gameData;
        _values = values;
    }

    protected override Task OnHandleAsync(object packet)
    {
        var now = DateTimeOffset.Now;
        switch (packet)
        {
            case ResponsePacket response:
                HandleResponse(response);
                break;
            case RequestPacket request:
                HandleRequest(request, now);
                break;
            case EventPacket eventPacket:
                HandleEvent(eventPacket, now);
                break;
        }

        return NextAsync(packet);
    }

    private void HandleResponse(ResponsePacket packet)
    {
        if (packet.OperationCode != (short)LegacyAlbionOperationCode.Join)
        {
            return;
        }

        var join = LegacyJoinResponse.FromParameters(packet.Parameters);
        lock (_sync)
        {
            _localInteractGuid = join.InteractGuid is not null && join.InteractGuid != Guid.Empty
                ? join.InteractGuid
                : _localInteractGuid;
        }
    }

    private void HandleRequest(RequestPacket packet, DateTimeOffset now)
    {
        if (packet.OperationCode != (short)LegacyAlbionOperationCode.InventoryMoveItem)
        {
            return;
        }

        var move = LegacyInventoryMoveItemRequest.FromParameters(packet.Parameters);
        RecordLocalContainerPickup(move, now);
    }

    private void HandleEvent(EventPacket packet, DateTimeOffset now)
    {
        switch ((LegacyAlbionEventCode)packet.EventCode)
        {
            case LegacyAlbionEventCode.NewLoot:
                RecordLootBody(packet.Parameters);
                break;
            case LegacyAlbionEventCode.AttachItemContainer:
                RecordAttachedContainer(packet.Parameters);
                break;
            case LegacyAlbionEventCode.NewEquipmentItem:
            case LegacyAlbionEventCode.NewSimpleItem:
            case LegacyAlbionEventCode.NewFurnitureItem:
            case LegacyAlbionEventCode.NewKillTrophyItem:
            case LegacyAlbionEventCode.NewJournalItem:
            case LegacyAlbionEventCode.NewLaborerItem:
            case LegacyAlbionEventCode.NewEquipmentItemLegendarySoul:
                RecordDiscoveredItem(packet.Parameters);
                break;
            case LegacyAlbionEventCode.OtherGrabbedLoot:
                RecordGrabbedLoot(packet.Parameters, now);
                break;
        }
    }

    private void RecordLootBody(IReadOnlyDictionary<byte, object> parameters)
    {
        var value = LegacyNewLootEvent.FromParameters(parameters);
        if (value.ObjectId is null)
        {
            return;
        }

        lock (_sync)
        {
            _lootBodies[value.ObjectId.Value] = string.IsNullOrWhiteSpace(value.LootBody) ? "loot body" : value.LootBody;
        }
    }

    private void RecordAttachedContainer(IReadOnlyDictionary<byte, object> parameters)
    {
        var value = LegacyAttachItemContainerEvent.FromParameters(parameters);
        lock (_sync)
        {
            _currentContainer = value;
        }
    }

    private void RecordDiscoveredItem(IReadOnlyDictionary<byte, object> parameters)
    {
        var item = LegacyDiscoveredItem.FromItemEventParameters(parameters);
        if (item is null || item.ItemIndex <= 0 || item.Quantity <= 0)
        {
            return;
        }

        lock (_sync)
        {
            _discoveredItems.TryAdd(item.ObjectId, item);
        }
    }

    private void RecordLocalContainerPickup(LegacyInventoryMoveItemRequest move, DateTimeOffset now)
    {
        LegacyDiscoveredItem? discoveredItem;
        string sourceName;
        string looterName;

        lock (_sync)
        {
            if (_localInteractGuid is null
                || move.UserInteractGuid is null
                || move.ContainerGuid is null
                || _currentContainer is null
                || _currentContainer.ContainerGuid != move.ContainerGuid
                || _localInteractGuid != move.UserInteractGuid
                || move.ContainerSlot < 0
                || move.ContainerSlot >= _currentContainer.SlotItemIds.Count)
            {
                return;
            }

            var itemObjectId = _currentContainer.SlotItemIds[move.ContainerSlot];
            if (!_discoveredItems.TryGetValue(itemObjectId, out discoveredItem))
            {
                return;
            }

            sourceName = _currentContainer.ObjectId is { } objectId && _lootBodies.TryGetValue(objectId, out var name)
                ? name
                : ResolveMobName("loot body");
            looterName = string.IsNullOrWhiteSpace(_entityNames.LocalName) ? "local player" : _entityNames.LocalName;
        }

        RecordItemLoot(
            now,
            looterName,
            ResolveMobName(sourceName),
            discoveredItem.ItemIndex,
            discoveredItem.Quantity,
            discoveredItem.Quality);
    }

    private void RecordGrabbedLoot(IReadOnlyDictionary<byte, object> parameters, DateTimeOffset now)
    {
        var value = LegacyOtherGrabbedLootEvent.FromParameters(parameters);
        if (value.IsSilver || value.ItemIndex <= 0 || value.Quantity <= 0)
        {
            return;
        }

        var sourceName = string.IsNullOrWhiteSpace(value.SourceName)
            ? _entityNames.Resolve(value.ObjectId ?? 0)
            : value.SourceName;

        RecordItemLoot(
            now,
            value.LooterName,
            ResolveMobName(sourceName),
            value.ItemIndex,
            value.Quantity,
            quality: 0);
    }

    private void RecordItemLoot(DateTimeOffset now, string looterName, string sourceName, int itemIndex, long quantity, int quality)
    {
        if (IsDuplicate(itemIndex, quantity, sourceName, now))
        {
            return;
        }

        var item = _gameData.GetItem(itemIndex);
        var unitValue = quality > 0 ? _values.GetUnitValue(itemIndex, quality) : _values.GetUnitValue(itemIndex);
        _lootLog.Record(new LootLogEntry(
            now,
            looterName,
            sourceName,
            false,
            itemIndex,
            item?.UniqueName ?? string.Empty,
            item?.DisplayName ?? $"item #{itemIndex}",
            quantity,
            unitValue,
            unitValue * quantity,
            quality));
    }

    private bool IsDuplicate(int itemIndex, long quantity, string sourceName, DateTimeOffset now)
    {
        lock (_sync)
        {
            if (_lastLootedItem is not null
                && _lastLootedItem.ItemIndex == itemIndex
                && _lastLootedItem.Quantity == quantity
                && string.Equals(_lastLootedItem.SourceName, sourceName, StringComparison.OrdinalIgnoreCase)
                && now - _lastLootedItem.Time <= DeduplicationWindow)
            {
                return true;
            }

            _lastLootedItem = new DedupedLootRecord(now, itemIndex, quantity, sourceName);
            return false;
        }
    }

    private static string ResolveMobName(string sourceName)
    {
        return sourceName.Contains("@MOB", StringComparison.OrdinalIgnoreCase)
            ? "MOB"
            : sourceName;
    }

    private sealed record DedupedLootRecord(DateTimeOffset Time, int ItemIndex, long Quantity, string SourceName);
}
