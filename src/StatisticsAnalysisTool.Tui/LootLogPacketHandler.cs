using StatisticsAnalysisTool.Network;

namespace StatisticsAnalysisTool.Tui;

public sealed class LootLogPacketHandler : PacketHandler<EventPacket>
{
    private readonly LootLogService _lootLog;
    private readonly EntityNameService _entityNames;
    private readonly GameDataIndex _gameData;
    private readonly EstimatedItemValueService _values;

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

    protected override Task OnHandleAsync(EventPacket packet)
    {
        var now = DateTimeOffset.Now;
        switch ((AlbionEventCodes)packet.EventCode)
        {
            case AlbionEventCodes.OtherGrabbedLoot:
                RecordGrabbedLoot(packet.Parameters, now);
                break;
        }

        return NextAsync(packet);
    }

    private void RecordGrabbedLoot(IReadOnlyDictionary<byte, object> parameters, DateTimeOffset now)
    {
        var isSilver = PacketValueReader.GetBool(parameters, 3) ?? false;
        var itemIndex = PacketValueReader.GetInt(parameters, 4) ?? 0;
        var quantity = PacketValueReader.GetLong(parameters, 5) ?? 0;
        var looterName = PacketValueReader.GetString(parameters, 2);
        if (isSilver || itemIndex <= 0 || quantity <= 0 || !_entityNames.IsLocalName(looterName))
        {
            return;
        }

        var item = _gameData.GetItem(itemIndex);
        var unitValue = _values.GetUnitValue(itemIndex);
        _lootLog.Record(new LootLogEntry(
            now,
            looterName,
            ResolveSourceName(parameters),
            false,
            itemIndex,
            item?.UniqueName ?? string.Empty,
            item?.DisplayName ?? $"item #{itemIndex}",
            quantity,
            unitValue,
            unitValue * quantity));
    }

    private string ResolveSourceName(IReadOnlyDictionary<byte, object> parameters)
    {
        var sourceName = PacketValueReader.GetString(parameters, 1);
        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            return sourceName;
        }

        return _entityNames.Resolve(PacketValueReader.GetLong(parameters, 0) ?? 0);
    }
}
