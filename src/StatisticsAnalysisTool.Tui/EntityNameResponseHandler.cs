using StatisticsAnalysisTool.Network;

namespace StatisticsAnalysisTool.Tui;

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
        if (packet.OperationCode == (short)AlbionOperationCodes.Join)
        {
            var objectId = PacketValueReader.GetLong(packet.Parameters, 0);
            var guid = PacketValueReader.GetGuid(packet.Parameters, 1);
            var username = PacketValueReader.GetString(packet.Parameters, 2);

            _entityNames.SetLocalEntity(objectId, guid, username);
            _party.SetLocal(objectId, guid, username);
            _timeline?.SetLocalPlayer(username);
        }
        else if (packet.OperationCode == (short)AlbionOperationCodes.GetCharacterEquipment)
        {
            var guid = PacketValueReader.GetGuid(packet.Parameters, 0);
            var equipment = PacketValueReader.GetIndexedValues(packet.Parameters, 1, PacketValueReader.ToInt);
            var itemPower = PacketValueReader.GetDouble(packet.Parameters, 3);

            _party.SetCharacterEquipment(guid, equipment);
            _party.SetItemPower(guid, itemPower);
        }
        else if (packet.OperationCode == (short)AlbionOperationCodes.PartyFinderGetEquipmentSnapshot
                 || packet.OperationCode == (short)AlbionOperationCodes.PartyFinderRequestEquipmentSnapshot)
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
