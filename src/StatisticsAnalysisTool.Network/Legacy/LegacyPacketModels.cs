#nullable enable

namespace StatisticsAnalysisTool.Network.Legacy;

public sealed record LegacyJoinResponse(long? ObjectId, Guid? Guid, string Name, Guid? InteractGuid)
{
    public static LegacyJoinResponse FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyJoinResponse(
            LegacyPacketValueReader.GetLong(parameters, 0),
            LegacyPacketValueReader.GetGuid(parameters, 1),
            LegacyPacketValueReader.GetString(parameters, 2),
            LegacyPacketValueReader.GetGuid(parameters, 54));
    }
}

public sealed record LegacyGetCharacterEquipmentResponse(Guid? Guid, IReadOnlyList<int> Equipment, double? ItemPower)
{
    public static LegacyGetCharacterEquipmentResponse FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyGetCharacterEquipmentResponse(
            LegacyPacketValueReader.GetGuid(parameters, 0),
            LegacyPacketValueReader.GetLegacyEquipment(parameters, 1),
            LegacyPacketValueReader.GetDouble(parameters, 3));
    }
}

public sealed record LegacyNewCharacterEvent(
    long? ObjectId,
    string Name,
    Guid? Guid,
    string GuildName,
    IReadOnlyList<int> Equipment)
{
    public static LegacyNewCharacterEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyNewCharacterEvent(
            LegacyPacketValueReader.GetLong(parameters, 0),
            LegacyPacketValueReader.GetString(parameters, 1),
            LegacyPacketValueReader.GetGuid(parameters, 7),
            LegacyPacketValueReader.GetString(parameters, 8),
            LegacyPacketValueReader.GetLegacyEquipment(parameters, 40));
    }
}

public sealed record LegacyCharacterEquipmentChangedEvent(long? ObjectId, IReadOnlyList<int> Equipment, IReadOnlyList<int> Spells)
{
    public static LegacyCharacterEquipmentChangedEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyCharacterEquipmentChangedEvent(
            LegacyPacketValueReader.GetLong(parameters, 0),
            LegacyPacketValueReader.GetLegacyEquipment(parameters, 2),
            LegacyPacketValueReader.GetIndexedValues(parameters, 7, LegacyPacketValueReader.ToInt));
    }
}

public sealed record LegacyPartyJoinedEvent(IReadOnlyDictionary<Guid, string> PartyUsers)
{
    public static LegacyPartyJoinedEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        var guids = LegacyPacketValueReader.GetGuidList(parameters, 5);
        var names = LegacyPacketValueReader.GetStringList(parameters, 6);
        var party = new Dictionary<Guid, string>();
        var count = Math.Min(guids.Count, names.Count);

        for (var i = 0; i < count; i++)
        {
            if (guids[i] != Guid.Empty && !string.IsNullOrWhiteSpace(names[i]))
            {
                party[guids[i]] = names[i].Trim();
            }
        }

        return new LegacyPartyJoinedEvent(party);
    }
}

public sealed record LegacyPartyPlayerJoinedEvent(Guid? Guid, string Name)
{
    public static LegacyPartyPlayerJoinedEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyPartyPlayerJoinedEvent(
            LegacyPacketValueReader.GetGuid(parameters, 1),
            LegacyPacketValueReader.GetString(parameters, 2));
    }
}

public sealed record LegacyPartyPlayerLeftEvent(Guid? Guid)
{
    public static LegacyPartyPlayerLeftEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyPartyPlayerLeftEvent(LegacyPacketValueReader.GetGuid(parameters, 1));
    }
}

public sealed record LegacyNewLootEvent(long? ObjectId, string LootBody)
{
    public static LegacyNewLootEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyNewLootEvent(
            LegacyPacketValueReader.GetLong(parameters, 0),
            LegacyPacketValueReader.GetString(parameters, 3));
    }
}

public sealed record LegacyAttachItemContainerEvent(long? ObjectId, Guid? ContainerGuid, Guid? PrivateContainerGuid, IReadOnlyList<long> SlotItemIds)
{
    public static LegacyAttachItemContainerEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyAttachItemContainerEvent(
            LegacyPacketValueReader.GetLong(parameters, 0),
            LegacyPacketValueReader.GetGuid(parameters, 1),
            LegacyPacketValueReader.GetGuid(parameters, 2),
            LegacyPacketValueReader.GetLegacyLongSlots(parameters, 3));
    }
}

public sealed record LegacyInventoryMoveItemRequest(int ContainerSlot, Guid? ContainerGuid, int InventorySlot, Guid? UserInteractGuid)
{
    public static LegacyInventoryMoveItemRequest FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyInventoryMoveItemRequest(
            LegacyPacketValueReader.GetInt(parameters, 0) ?? 0,
            LegacyPacketValueReader.GetGuid(parameters, 1),
            LegacyPacketValueReader.GetInt(parameters, 3) ?? 0,
            LegacyPacketValueReader.GetGuid(parameters, 4));
    }
}

public sealed record LegacyOtherGrabbedLootEvent(
    long? ObjectId,
    string SourceName,
    string LooterName,
    bool IsSilver,
    int ItemIndex,
    long Quantity)
{
    public static LegacyOtherGrabbedLootEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyOtherGrabbedLootEvent(
            LegacyPacketValueReader.GetLong(parameters, 0),
            LegacyPacketValueReader.GetString(parameters, 1),
            LegacyPacketValueReader.GetString(parameters, 2),
            LegacyPacketValueReader.GetBool(parameters, 3) ?? false,
            LegacyPacketValueReader.GetInt(parameters, 4) ?? 0,
            LegacyPacketValueReader.GetLong(parameters, 5) ?? 0);
    }
}

public sealed record LegacyDiscoveredItem(long ObjectId, int ItemIndex, int Quantity, long EstimatedMarketValue, int Quality)
{
    public static LegacyDiscoveredItem? FromItemEventParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        var objectId = LegacyPacketValueReader.GetLong(parameters, 0);
        if (objectId is null)
        {
            return null;
        }

        return new LegacyDiscoveredItem(
            objectId.Value,
            LegacyPacketValueReader.GetInt(parameters, 1) ?? 0,
            LegacyPacketValueReader.GetInt(parameters, 2) ?? 0,
            LegacyPacketValueReader.GetLong(parameters, 4) ?? 0,
            LegacyPacketValueReader.GetInt(parameters, 6) ?? 1);
    }
}

public sealed record LegacyNewMobEvent(long? ObjectId, int MobIndex, double HitPointsMax)
{
    public static LegacyNewMobEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyNewMobEvent(
            LegacyPacketValueReader.GetLong(parameters, 0),
            LegacyPacketValueReader.GetInt(parameters, 1) ?? 0,
            LegacyPacketValueReader.GetDouble(parameters, 14) ?? 0);
    }
}

public sealed record LegacyTakeSilverEvent(
    long? ObjectId,
    long? TargetEntityId,
    long YieldPreTaxInternal,
    long GuildTaxInternal,
    long ClusterTaxInternal,
    bool IsPremiumBonus,
    long MultiplierInternal)
{
    public static LegacyTakeSilverEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyTakeSilverEvent(
            LegacyPacketValueReader.GetLong(parameters, 0),
            LegacyPacketValueReader.GetLong(parameters, 2),
            LegacyPacketValueReader.GetLong(parameters, 3) ?? 0,
            LegacyPacketValueReader.GetLong(parameters, 5) ?? 0,
            LegacyPacketValueReader.GetLong(parameters, 6) ?? 0,
            LegacyPacketValueReader.GetBool(parameters, 7) ?? false,
            LegacyPacketValueReader.GetLong(parameters, 8) ?? 0);
    }
}

public sealed record LegacyUpdateFameEvent(
    long FameWithZoneMultiplierInternal,
    bool IsPremiumBonus,
    long SatchelFameInternal,
    double BonusFactor)
{
    public static LegacyUpdateFameEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        var bonusFactor = 1d;
        if (parameters.TryGetValue(17, out var rawBonusFactor))
        {
            bonusFactor = 1d + (LegacyPacketValueReader.ToDouble(rawBonusFactor) ?? 0d);
            if ((bonusFactor - 1d) * 100d > 0d)
            {
                bonusFactor = 1d;
            }
        }

        return new LegacyUpdateFameEvent(
            LegacyPacketValueReader.GetLong(parameters, 2) ?? 0,
            LegacyPacketValueReader.GetBool(parameters, 5) ?? false,
            LegacyPacketValueReader.GetLong(parameters, 10) ?? 0,
            bonusFactor);
    }
}

public sealed record LegacyUpdateReSpecPointsEvent(bool HasCurrentTotalReSpecPoints, long GainedReSpecPointsInternal, long PaidSilverInternal)
{
    public static LegacyUpdateReSpecPointsEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        var totals = LegacyPacketValueReader.GetIndexedValues(parameters, 0, LegacyPacketValueReader.ToLong);
        return new LegacyUpdateReSpecPointsEvent(
            totals.Count > 1,
            LegacyPacketValueReader.GetLong(parameters, 2) ?? 0,
            LegacyPacketValueReader.GetLong(parameters, 3) ?? 0);
    }
}

public sealed record LegacyUpdateCurrencyEvent(int CityFaction, long GainedFactionCoinsInternal)
{
    public static LegacyUpdateCurrencyEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyUpdateCurrencyEvent(
            LegacyPacketValueReader.GetInt(parameters, 2) ?? 0,
            LegacyPacketValueReader.GetLong(parameters, 3) ?? 0);
    }
}

public sealed record LegacyUpdateFactionStandingEvent(int CityFaction, long GainedFactionFlagPointsInternal, long BonusPremiumGainedFactionFlagPointsInternal)
{
    public static LegacyUpdateFactionStandingEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyUpdateFactionStandingEvent(
            LegacyPacketValueReader.GetInt(parameters, 0) ?? 0,
            LegacyPacketValueReader.GetLong(parameters, 1) ?? 0,
            LegacyPacketValueReader.GetLong(parameters, 2) ?? 0);
    }
}

public sealed record LegacyMightAndFavorReceivedEvent(long MightInternal, long FavorInternal)
{
    public static LegacyMightAndFavorReceivedEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyMightAndFavorReceivedEvent(
            LegacyPacketValueReader.GetLong(parameters, 0) ?? 0,
            LegacyPacketValueReader.GetLong(parameters, 3) ?? 0);
    }
}

public sealed record LegacyHealthUpdate(
    long AffectedObjectId,
    double HealthChange,
    double NewHealthValue,
    long CauserId,
    int CausingSpellIndex);

public sealed record LegacyHealthUpdateEvent(LegacyHealthUpdate? Update)
{
    public static LegacyHealthUpdateEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        var affectedId = LegacyPacketValueReader.GetLong(parameters, 0);
        var healthChange = LegacyPacketValueReader.GetDouble(parameters, 2);
        var causerId = LegacyPacketValueReader.GetLong(parameters, 6);
        if (affectedId is null || healthChange is null || causerId is null)
        {
            return new LegacyHealthUpdateEvent((LegacyHealthUpdate?)null);
        }

        return new LegacyHealthUpdateEvent(new LegacyHealthUpdate(
            affectedId.Value,
            healthChange.Value,
            LegacyPacketValueReader.GetDouble(parameters, 3) ?? 0,
            causerId.Value,
            LegacyPacketValueReader.GetInt(parameters, 7) ?? 0));
    }
}

public sealed record LegacyHealthUpdatesEvent(IReadOnlyList<LegacyHealthUpdate> Updates)
{
    public static LegacyHealthUpdatesEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        var affectedId = LegacyPacketValueReader.GetLong(parameters, 0);
        if (affectedId is null)
        {
            return new LegacyHealthUpdatesEvent([]);
        }

        var healthChanges = LegacyPacketValueReader.GetIndexedValues(parameters, 2, LegacyPacketValueReader.ToDouble);
        var newHealthValues = LegacyPacketValueReader.GetIndexedValues(parameters, 3, LegacyPacketValueReader.ToDouble);
        var causerIds = LegacyPacketValueReader.GetIndexedValues(parameters, 6, LegacyPacketValueReader.ToLong);
        var causingSpellIndices = LegacyPacketValueReader.GetIndexedValues(parameters, 7, LegacyPacketValueReader.ToInt);
        var count = new[] { healthChanges.Count, newHealthValues.Count, causerIds.Count, causingSpellIndices.Count }.Max();
        var updates = new List<LegacyHealthUpdate>();

        for (var i = 0; i < count; i++)
        {
            updates.Add(new LegacyHealthUpdate(
                affectedId.Value,
                i < healthChanges.Count ? healthChanges[i] : 0,
                i < newHealthValues.Count ? newHealthValues[i] : 0,
                i < causerIds.Count ? causerIds[i] : 0,
                i < causingSpellIndices.Count ? causingSpellIndices[i] : 0));
        }

        return new LegacyHealthUpdatesEvent(updates);
    }
}

public sealed record LegacyInCombatStateUpdateEvent(long? ObjectId, bool InActiveCombat, bool InPassiveCombat)
{
    public static LegacyInCombatStateUpdateEvent FromParameters(IReadOnlyDictionary<byte, object> parameters)
    {
        return new LegacyInCombatStateUpdateEvent(
            LegacyPacketValueReader.GetLong(parameters, 0),
            LegacyPacketValueReader.GetBool(parameters, 1) ?? false,
            LegacyPacketValueReader.GetBool(parameters, 2) ?? false);
    }
}
