using StatisticsAnalysisTool.Network;
using StatisticsAnalysisTool.Network.Legacy;

namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed class EntityNameResponseHandler : PacketHandler<ResponsePacket>
{
    private readonly EntityNameService _entityNames;
    private readonly PartyService _party;
    private readonly SessionTimelineService? _timeline;
    private readonly PartySnapshotDiagnosticsService? _snapshotDiagnostics;

    public EntityNameResponseHandler(
        EntityNameService entityNames,
        PartyService party,
        SessionTimelineService? timeline = null,
        PartySnapshotDiagnosticsService? snapshotDiagnostics = null)
    {
        _entityNames = entityNames;
        _party = party;
        _timeline = timeline;
        _snapshotDiagnostics = snapshotDiagnostics;
    }

    protected override Task OnHandleAsync(ResponsePacket packet)
    {
        if (packet.OperationCode == (short)LegacyAlbionOperationCode.Join)
        {
            var value = LegacyJoinResponse.FromParameters(packet.Parameters);

            _entityNames.SetLocalEntity(value.ObjectId, value.Guid, value.Name);
            _party.SetLocal(value.ObjectId, value.Guid, value.Name);
            _timeline?.SetLocalPlayer(value.Name);
        }
        else if (packet.OperationCode == (short)LegacyAlbionOperationCode.GetCharacterEquipment)
        {
            var value = LegacyGetCharacterEquipmentResponse.FromParameters(packet.Parameters);
            var name = _entityNames.Resolve(value.Guid);

            _party.SetCharacterEquipment(value.Guid, name, value.Equipment);
            _party.SetItemPower(value.Guid, name, value.ItemPower);
        }
        else if (packet.OperationCode == (short)LegacyAlbionOperationCode.PartyFinderGetEquipmentSnapshot
                 || packet.OperationCode == (short)LegacyAlbionOperationCode.PartyFinderRequestEquipmentSnapshot)
        {
            var snapshots = PartyEquipmentSnapshotParser.Parse(packet.Parameters);
            _snapshotDiagnostics?.Record("response", packet.Parameters, snapshots.Count);

            foreach (var snapshot in snapshots)
            {
                _entityNames.SetGuidName(snapshot.Guid, snapshot.Name);
                _party.UpsertEquipmentSnapshot(snapshot.Guid, snapshot.Name, snapshot.ItemPower, snapshot.Equipment, markAsPartyMember: false);
            }
        }

        return NextAsync(packet);
    }
}
