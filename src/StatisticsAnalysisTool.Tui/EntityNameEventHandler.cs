using StatisticsAnalysisTool.Network;

namespace StatisticsAnalysisTool.Tui;

public sealed class EntityNameEventHandler : PacketHandler<EventPacket>
{
    private readonly EntityNameService _entityNames;
    private readonly PartyService _party;
    private readonly SessionTimelineService? _timeline;
    private readonly PartySnapshotDiagnosticsService? _snapshotDiagnostics;

    public EntityNameEventHandler(
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

    protected override Task OnHandleAsync(EventPacket packet)
    {
        switch ((AlbionEventCodes)packet.EventCode)
        {
            case AlbionEventCodes.NewCharacter:
                RecordCharacter(packet.Parameters);
                break;
            case AlbionEventCodes.CharacterEquipmentChanged:
                RecordEquipmentChanged(packet.Parameters);
                break;
            case AlbionEventCodes.NewLoot:
                RecordLootBody(packet.Parameters);
                break;
            case AlbionEventCodes.NewMob:
                RecordMob(packet.Parameters);
                break;
            case AlbionEventCodes.PartyJoined:
                RecordParty(packet.Parameters);
                break;
            case AlbionEventCodes.PartyDisbanded:
                _entityNames.ResetPartyToLocal();
                _party.Disband();
                _timeline?.PartyDisbanded();
                break;
            case AlbionEventCodes.PartyPlayerJoined:
                RecordPartyPlayer(packet.Parameters);
                break;
            case AlbionEventCodes.PartyPlayerLeft:
                RemovePartyPlayer(packet.Parameters);
                break;
            case AlbionEventCodes.PartyPlayerUpdated:
                break;
            case AlbionEventCodes.PartyInviteOrJoinPlayerEquipmentInfo:
            case AlbionEventCodes.PartyFinderEquipmentSnapshot:
                RecordPartyEquipmentSnapshots(packet.Parameters, markAsPartyMember: false);
                break;
        }

        return NextAsync(packet);
    }

    private void RecordCharacter(IReadOnlyDictionary<byte, object> parameters)
    {
        var objectId = PacketValueReader.GetLong(parameters, 0);
        var name = PacketValueReader.GetString(parameters, 1);
        var guid = PacketValueReader.GetGuid(parameters, 7);
        var guildName = PacketValueReader.GetString(parameters, 8);
        var equipment = PacketValueReader.GetIndexedValues(parameters, 40, PacketValueReader.ToInt);

        _entityNames.SetEntity(objectId, guid, name);
        _party.UpsertCharacter(objectId, guid, name, guildName, equipment);
    }

    private void RecordEquipmentChanged(IReadOnlyDictionary<byte, object> parameters)
    {
        var objectId = PacketValueReader.GetLong(parameters, 0);
        var equipment = PacketValueReader.GetIndexedValues(parameters, 2, PacketValueReader.ToInt);
        _party.UpdateEquipment(objectId, equipment);
    }

    private void RecordLootBody(IReadOnlyDictionary<byte, object> parameters)
    {
        var objectId = PacketValueReader.GetLong(parameters, 0);
        var name = PacketValueReader.GetString(parameters, 3);
        _entityNames.SetEntityName(objectId, string.IsNullOrWhiteSpace(name) ? "loot body" : name);
    }

    private void RecordMob(IReadOnlyDictionary<byte, object> parameters)
    {
        var objectId = PacketValueReader.GetLong(parameters, 0);
        var mobIndex = PacketValueReader.GetInt(parameters, 1);
        _entityNames.SetEntityName(objectId, mobIndex is > 0 ? $"mob #{mobIndex}" : "mob");
    }

    private void RecordPartyPlayer(IReadOnlyDictionary<byte, object> parameters)
    {
        var guid = PacketValueReader.GetGuid(parameters, 1);
        var name = PacketValueReader.GetString(parameters, 2);
        _entityNames.SetPartyMember(guid, name);
        _party.AddPartyMember(guid, name);
        _timeline?.PlayerJoined(name);
    }

    private void RemovePartyPlayer(IReadOnlyDictionary<byte, object> parameters)
    {
        var guid = PacketValueReader.GetGuid(parameters, 1);
        var name = _entityNames.Resolve(guid);
        var localLeft = _entityNames.IsLocalGuid(guid);
        if (localLeft)
        {
            _entityNames.ResetPartyToLocal();
            _party.Disband();
            _timeline?.SetParty([_entityNames.LocalName]);
            return;
        }

        _entityNames.RemovePartyMember(guid);
        _party.RemovePartyMember(guid);
        _timeline?.PlayerLeft(name);
    }

    private void RecordParty(IReadOnlyDictionary<byte, object> parameters)
    {
        var guids = PacketValueReader.GetGuidList(parameters, 5);
        var names = PacketValueReader.GetStringList(parameters, 6);
        var party = new Dictionary<Guid, string>();
        var count = Math.Min(guids.Count, names.Count);

        for (var i = 0; i < count; i++)
        {
            if (guids[i] != Guid.Empty && !string.IsNullOrWhiteSpace(names[i]))
            {
                party[guids[i]] = names[i].Trim();
            }
        }

        _entityNames.SetParty(party);
        _party.SetParty(party);
        _timeline?.SetParty(party.Values.Append(_entityNames.LocalName).ToArray());
    }

    private void RecordPartyEquipmentSnapshots(IReadOnlyDictionary<byte, object> parameters, bool markAsPartyMember)
    {
        var snapshots = PartyEquipmentSnapshotParser.Parse(parameters);
        _snapshotDiagnostics?.Record("event", parameters, snapshots.Count);

        foreach (var snapshot in snapshots)
        {
            _entityNames.SetGuidName(snapshot.Guid, snapshot.Name);
            _party.UpsertEquipmentSnapshot(snapshot.Guid, snapshot.Name, snapshot.ItemPower, snapshot.Equipment, markAsPartyMember);
        }
    }
}
