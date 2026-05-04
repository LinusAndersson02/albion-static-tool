using StatisticsAnalysisTool.Network;
using StatisticsAnalysisTool.Network.Legacy;

namespace StatisticsAnalysisTool.Linux.Runtime;

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
        switch ((LegacyAlbionEventCode)packet.EventCode)
        {
            case LegacyAlbionEventCode.NewCharacter:
                RecordCharacter(packet.Parameters);
                break;
            case LegacyAlbionEventCode.CharacterEquipmentChanged:
                RecordEquipmentChanged(packet.Parameters);
                break;
            case LegacyAlbionEventCode.NewLoot:
                RecordLootBody(packet.Parameters);
                break;
            case LegacyAlbionEventCode.NewMob:
                RecordMob(packet.Parameters);
                break;
            case LegacyAlbionEventCode.PartyJoined:
                RecordParty(packet.Parameters);
                break;
            case LegacyAlbionEventCode.PartyDisbanded:
                _entityNames.ResetPartyToLocal();
                _party.Disband();
                _timeline?.PartyDisbanded();
                break;
            case LegacyAlbionEventCode.PartyPlayerJoined:
                RecordPartyPlayer(packet.Parameters);
                break;
            case LegacyAlbionEventCode.PartyPlayerLeft:
                RemovePartyPlayer(packet.Parameters);
                break;
            case LegacyAlbionEventCode.PartyPlayerUpdated:
                break;
            case LegacyAlbionEventCode.PartyInviteOrJoinPlayerEquipmentInfo:
            case LegacyAlbionEventCode.PartyFinderEquipmentSnapshot:
                RecordPartyEquipmentSnapshots(packet.Parameters, markAsPartyMember: false);
                break;
        }

        return NextAsync(packet);
    }

    private void RecordCharacter(IReadOnlyDictionary<byte, object> parameters)
    {
        var value = LegacyNewCharacterEvent.FromParameters(parameters);

        _entityNames.SetEntity(value.ObjectId, value.Guid, value.Name);
        _party.UpsertCharacter(value.ObjectId, value.Guid, value.Name, value.GuildName, value.Equipment);
    }

    private void RecordEquipmentChanged(IReadOnlyDictionary<byte, object> parameters)
    {
        var value = LegacyCharacterEquipmentChangedEvent.FromParameters(parameters);
        _party.UpdateEquipment(value.ObjectId, value.Equipment);
    }

    private void RecordLootBody(IReadOnlyDictionary<byte, object> parameters)
    {
        var value = LegacyNewLootEvent.FromParameters(parameters);
        _entityNames.SetEntityName(value.ObjectId, string.IsNullOrWhiteSpace(value.LootBody) ? "loot body" : value.LootBody);
    }

    private void RecordMob(IReadOnlyDictionary<byte, object> parameters)
    {
        var value = LegacyNewMobEvent.FromParameters(parameters);
        _entityNames.SetEntityName(value.ObjectId, value.MobIndex > 0 ? $"mob #{value.MobIndex}" : "mob");
    }

    private void RecordPartyPlayer(IReadOnlyDictionary<byte, object> parameters)
    {
        var value = LegacyPartyPlayerJoinedEvent.FromParameters(parameters);
        _entityNames.SetPartyMember(value.Guid, value.Name);
        _party.AddPartyMember(value.Guid, value.Name);
        _timeline?.PlayerJoined(value.Name);
    }

    private void RemovePartyPlayer(IReadOnlyDictionary<byte, object> parameters)
    {
        var guid = LegacyPartyPlayerLeftEvent.FromParameters(parameters).Guid;
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
        var party = LegacyPartyJoinedEvent.FromParameters(parameters).PartyUsers;

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
