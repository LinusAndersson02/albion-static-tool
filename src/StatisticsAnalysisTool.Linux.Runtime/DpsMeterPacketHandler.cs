using StatisticsAnalysisTool.Network;
using StatisticsAnalysisTool.Network.Legacy;

namespace StatisticsAnalysisTool.Linux.Runtime;

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
        switch ((LegacyAlbionEventCode)packet.EventCode)
        {
            case LegacyAlbionEventCode.HealthUpdate:
                RecordHealthUpdate(packet.Parameters, now);
                break;
            case LegacyAlbionEventCode.HealthUpdates:
                RecordHealthUpdates(packet.Parameters, now);
                break;
            case LegacyAlbionEventCode.InCombatStateUpdate:
                var combatState = LegacyInCombatStateUpdateEvent.FromParameters(packet.Parameters);
                if (combatState.ObjectId is not null && _entityNames.IsPartyEntity(combatState.ObjectId))
                {
                    _dpsMeter.RecordCombatState(combatState.InActiveCombat, combatState.InPassiveCombat, now);
                }
                break;
        }

        return NextAsync(packet);
    }

    private void RecordHealthUpdate(IReadOnlyDictionary<byte, object> parameters, DateTimeOffset now)
    {
        var value = LegacyHealthUpdateEvent.FromParameters(parameters).Update;
        if (value is null)
        {
            return;
        }

        Record(value, now);
    }

    private void RecordHealthUpdates(IReadOnlyDictionary<byte, object> parameters, DateTimeOffset now)
    {
        foreach (var value in LegacyHealthUpdatesEvent.FromParameters(parameters).Updates)
        {
            Record(value, now);
        }
    }

    private void Record(LegacyHealthUpdate value, DateTimeOffset now)
    {
        _dpsMeter.RecordHealthUpdate(
            value.AffectedObjectId,
            value.CauserId,
            value.HealthChange,
            value.NewHealthValue,
            value.CausingSpellIndex,
            now,
            _entityNames.IsPartyEntity(value.CauserId),
            _entityNames.IsPartyEntity(value.AffectedObjectId));
    }
}
