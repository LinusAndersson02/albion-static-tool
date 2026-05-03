using StatisticsAnalysisTool.Network;

namespace StatisticsAnalysisTool.Tui;

public sealed class DpsMeterPacketHandler : PacketHandler<EventPacket>
{
    private readonly DpsMeterService _dpsMeter;
    private readonly EntityNameService _entityNames;

    public DpsMeterPacketHandler(DpsMeterService dpsMeter, EntityNameService entityNames)
    {
        _dpsMeter = dpsMeter;
        _entityNames = entityNames;
    }

    protected override Task OnHandleAsync(EventPacket packet)
    {
        var now = DateTimeOffset.Now;
        switch ((AlbionEventCodes)packet.EventCode)
        {
            case AlbionEventCodes.HealthUpdate:
                RecordHealthUpdate(packet.Parameters, now);
                break;
            case AlbionEventCodes.HealthUpdates:
                RecordHealthUpdates(packet.Parameters, now);
                break;
            case AlbionEventCodes.InCombatStateUpdate:
                _dpsMeter.RecordCombatState(
                    PacketValueReader.GetBool(packet.Parameters, 1) ?? false,
                    PacketValueReader.GetBool(packet.Parameters, 2) ?? false,
                    now);
                break;
        }

        return NextAsync(packet);
    }

    private void RecordHealthUpdate(IReadOnlyDictionary<byte, object> parameters, DateTimeOffset now)
    {
        var affectedId = PacketValueReader.GetLong(parameters, 0);
        var healthChange = PacketValueReader.GetDouble(parameters, 2);
        var newHealthValue = PacketValueReader.GetDouble(parameters, 3);
        var causerId = PacketValueReader.GetLong(parameters, 6);
        var causingSpellIndex = PacketValueReader.GetInt(parameters, 7);

        if (affectedId is null || healthChange is null || causerId is null)
        {
            return;
        }

        _dpsMeter.RecordHealthUpdate(
            affectedId.Value,
            causerId.Value,
            healthChange.Value,
            newHealthValue ?? 0,
            causingSpellIndex ?? 0,
            now,
            _entityNames.IsPartyEntity(causerId.Value),
            _entityNames.IsPartyEntity(affectedId.Value));
    }

    private void RecordHealthUpdates(IReadOnlyDictionary<byte, object> parameters, DateTimeOffset now)
    {
        var affectedId = PacketValueReader.GetLong(parameters, 0);
        if (affectedId is null)
        {
            return;
        }

        var healthChanges = PacketValueReader.GetIndexedValues(parameters, 2, PacketValueReader.ToDouble);
        var newHealthValues = PacketValueReader.GetIndexedValues(parameters, 3, PacketValueReader.ToDouble);
        var causerIds = PacketValueReader.GetIndexedValues(parameters, 6, PacketValueReader.ToLong);
        var causingSpellIndices = PacketValueReader.GetIndexedValues(parameters, 7, PacketValueReader.ToInt);
        var count = new[] { healthChanges.Count, newHealthValues.Count, causerIds.Count, causingSpellIndices.Count }.Max();

        for (var i = 0; i < count; i++)
        {
            if (i >= healthChanges.Count || i >= causerIds.Count)
            {
                continue;
            }

            _dpsMeter.RecordHealthUpdate(
                affectedId.Value,
                causerIds[i],
                healthChanges[i],
                i < newHealthValues.Count ? newHealthValues[i] : 0,
                i < causingSpellIndices.Count ? causingSpellIndices[i] : 0,
                now,
                _entityNames.IsPartyEntity(causerIds[i]),
                _entityNames.IsPartyEntity(affectedId.Value));
        }
    }
}
