namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed record PartyEquipmentSlot(
    string Slot,
    int ItemIndex,
    string ItemName,
    string ItemUniqueName,
    string RenderUrl);

public sealed record PartyMemberSnapshot(
    Guid? Guid,
    long? ObjectId,
    string Name,
    string GuildName,
    double? AverageItemPower,
    IReadOnlyList<PartyEquipmentSlot> Equipment,
    DateTimeOffset LastUpdatedAt);

public sealed record PartySnapshot(
    IReadOnlyList<PartyMemberSnapshot> Members,
    int PartySize,
    int KnownEquipmentCount,
    double? AverageItemPower);

public sealed class PartyService
{
    private static readonly string[] SlotNames =
    [
        "Main hand",
        "Off hand",
        "Head",
        "Chest",
        "Shoes",
        "Bag",
        "Cape",
        "Mount",
        "Potion",
        "Food"
    ];

    private readonly GameDataIndex _gameData;
    private readonly object _sync = new();
    private readonly Dictionary<Guid, PartyMemberState> _membersByGuid = [];
    private readonly Dictionary<long, PartyMemberState> _membersByObjectId = [];
    private readonly Dictionary<string, PartyMemberState> _membersByName = new(StringComparer.OrdinalIgnoreCase);
    private Guid? _localGuid;
    private long? _localObjectId;
    private string _localName = string.Empty;

    public PartyService(GameDataIndex gameData)
    {
        _gameData = gameData;
    }

    public void SetLocal(long? objectId, Guid? guid, string? name)
    {
        lock (_sync)
        {
            _localObjectId = objectId is not null and not 0 ? objectId : _localObjectId;
            _localGuid = guid is not null && guid != Guid.Empty ? guid : _localGuid;
            if (!string.IsNullOrWhiteSpace(name))
            {
                _localName = name.Trim();
            }

            var member = GetOrCreateLocked(_localGuid, _localObjectId, _localName);
            member.ObjectId = _localObjectId ?? member.ObjectId;
            member.Guid = _localGuid ?? member.Guid;
            if (!string.IsNullOrWhiteSpace(_localName))
            {
                member.Name = _localName;
            }

            member.IsPartyMember = true;
            member.LastUpdatedAt = DateTimeOffset.Now;
            IndexLocked(member);
        }
    }

    public void SetParty(IReadOnlyDictionary<Guid, string> members)
    {
        lock (_sync)
        {
            foreach (var member in _membersByGuid.Values.Concat(_membersByObjectId.Values).Concat(_membersByName.Values).Distinct())
            {
                member.IsPartyMember = false;
            }

            foreach (var (guid, name) in members)
            {
                var member = GetOrCreateLocked(guid, null, name);
                member.Guid = guid;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    member.Name = name.Trim();
                }

                member.IsPartyMember = true;
                member.LastUpdatedAt = DateTimeOffset.Now;
                IndexLocked(member);
            }

            if (!string.IsNullOrWhiteSpace(_localName) || _localGuid is not null || _localObjectId is not null)
            {
                SetLocalLocked(_localObjectId, _localGuid, _localName);
            }
        }
    }

    public void AddPartyMember(Guid? guid, string? name)
    {
        lock (_sync)
        {
            var member = GetOrCreateLocked(guid, null, name);
            member.Guid = guid ?? member.Guid;
            if (!string.IsNullOrWhiteSpace(name))
            {
                member.Name = name.Trim();
            }

            member.IsPartyMember = true;
            member.LastUpdatedAt = DateTimeOffset.Now;
            IndexLocked(member);
        }
    }

    public void RemovePartyMember(Guid? guid)
    {
        if (guid is null || guid == Guid.Empty)
        {
            return;
        }

        lock (_sync)
        {
            if (_localGuid == guid.Value)
            {
                DisbandLocked();
                return;
            }

            if (_membersByGuid.TryGetValue(guid.Value, out var member))
            {
                member.IsPartyMember = false;
                member.LastUpdatedAt = DateTimeOffset.Now;
            }
        }
    }

    public void Disband()
    {
        lock (_sync)
        {
            DisbandLocked();
        }
    }

    public void UpsertCharacter(long? objectId, Guid? guid, string? name, string? guildName, IReadOnlyList<int> equipment)
    {
        lock (_sync)
        {
            var member = GetOrCreateLocked(guid, objectId, name);
            member.ObjectId = objectId is not null and not 0 ? objectId : member.ObjectId;
            member.Guid = guid is not null && guid != Guid.Empty ? guid : member.Guid;
            if (!string.IsNullOrWhiteSpace(name))
            {
                member.Name = name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(guildName))
            {
                member.GuildName = guildName.Trim();
            }

            if (equipment.Count > 0)
            {
                var normalizedEquipment = NormalizeEquipment(equipment);
                if (normalizedEquipment.Count > 0)
                {
                    member.Equipment = normalizedEquipment;
                    member.EquipmentPriority = 2;
                }
            }

            if ((member.Guid is not null && _membersByGuid.TryGetValue(member.Guid.Value, out var byGuid) && byGuid.IsPartyMember)
                || (!string.IsNullOrWhiteSpace(member.Name) && _membersByName.TryGetValue(member.Name, out var byName) && byName.IsPartyMember)
                || IsLocalLocked(member))
            {
                member.IsPartyMember = true;
            }

            member.LastUpdatedAt = DateTimeOffset.Now;
            IndexLocked(member);
        }
    }

    public void UpdateEquipment(long? objectId, IReadOnlyList<int> equipment)
    {
        if (objectId is null or 0 || equipment.Count == 0)
        {
            return;
        }

        lock (_sync)
        {
            var member = GetOrCreateLocked(null, objectId, null);
            var normalizedEquipment = NormalizeEquipment(equipment);
            if (normalizedEquipment.Count == 0)
            {
                return;
            }

            member.ObjectId = objectId;
            member.Equipment = normalizedEquipment;
            member.EquipmentPriority = 2;
            member.LastUpdatedAt = DateTimeOffset.Now;
            IndexLocked(member);
        }
    }

    public void SetCharacterEquipment(Guid? guid, IReadOnlyList<int> equipment)
    {
        SetCharacterEquipment(guid, null, equipment);
    }

    public void SetCharacterEquipment(Guid? guid, string? name, IReadOnlyList<int> equipment)
    {
        if (equipment.Count == 0
            || ((guid is null || guid == Guid.Empty) && string.IsNullOrWhiteSpace(name)))
        {
            return;
        }

        lock (_sync)
        {
            var normalizedEquipment = NormalizeEquipment(equipment);
            if (normalizedEquipment.Count == 0)
            {
                return;
            }

            var member = GetOrCreateLocked(guid, null, name);
            member.Guid = guid is not null && guid != Guid.Empty ? guid : member.Guid;
            if (!string.IsNullOrWhiteSpace(name))
            {
                member.Name = name.Trim();
            }

            member.Equipment = normalizedEquipment;
            member.EquipmentPriority = 2;
            member.LastUpdatedAt = DateTimeOffset.Now;
            IndexLocked(member);
        }
    }

    public void SetItemPower(Guid? guid, double? itemPower)
    {
        SetItemPower(guid, null, itemPower);
    }

    public void SetItemPower(Guid? guid, string? name, double? itemPower)
    {
        if (((guid is null || guid == Guid.Empty) && string.IsNullOrWhiteSpace(name))
            || itemPower is null or <= 0)
        {
            return;
        }

        lock (_sync)
        {
            var member = GetOrCreateLocked(guid, null, name);
            member.Guid = guid is not null && guid != Guid.Empty ? guid : member.Guid;
            if (!string.IsNullOrWhiteSpace(name))
            {
                member.Name = name.Trim();
            }

            member.AverageItemPower = itemPower;
            member.LastUpdatedAt = DateTimeOffset.Now;
            IndexLocked(member);
        }
    }

    public void UpsertEquipmentSnapshot(Guid? guid, string? name, double? itemPower, IReadOnlyList<int> equipment, bool markAsPartyMember)
    {
        if ((guid is null || guid == Guid.Empty) && string.IsNullOrWhiteSpace(name) && equipment.Count == 0 && itemPower is null)
        {
            return;
        }

        lock (_sync)
        {
            var member = GetOrCreateLocked(guid, null, name);
            member.Guid = guid is not null && guid != Guid.Empty ? guid : member.Guid;
            if (!string.IsNullOrWhiteSpace(name))
            {
                member.Name = name.Trim();
            }

            if (itemPower is > 0)
            {
                member.AverageItemPower = itemPower;
            }

            if (equipment.Count > 0 && member.EquipmentPriority <= 1)
            {
                var normalizedEquipment = NormalizeEquipment(equipment);
                if (normalizedEquipment.Count > 0)
                {
                    member.Equipment = normalizedEquipment;
                    member.EquipmentPriority = 1;
                }
            }

            if (markAsPartyMember
                || (member.Guid is not null && _membersByGuid.TryGetValue(member.Guid.Value, out var byGuid) && byGuid.IsPartyMember)
                || (!string.IsNullOrWhiteSpace(member.Name) && _membersByName.TryGetValue(member.Name, out var byName) && byName.IsPartyMember)
                || IsLocalLocked(member))
            {
                member.IsPartyMember = true;
            }

            member.LastUpdatedAt = DateTimeOffset.Now;
            IndexLocked(member);
        }
    }

    public PartySnapshot GetSnapshot()
    {
        lock (_sync)
        {
            var members = _membersByGuid.Values
                .Concat(_membersByObjectId.Values)
                .Concat(_membersByName.Values)
                .Distinct()
                .Where(x => x.IsPartyMember)
                .OrderByDescending(x => IsLocalLocked(x))
                .ThenBy(x => x.Name)
                .Select(ToSnapshotLocked)
                .ToArray();
            var nonLocalMembers = members.Where(x => !IsLocalSnapshot(x)).ToArray();
            var ipSum = nonLocalMembers.Sum(x => x.AverageItemPower ?? 0);

            return new PartySnapshot(
                members,
                members.Length,
                members.Count(x => x.Equipment.Count > 0),
                members.Length == 0
                    ? null
                    : ipSum <= 0 || nonLocalMembers.Length == 0
                        ? 0
                        : ipSum / nonLocalMembers.Length);
        }
    }

    private void SetLocalLocked(long? objectId, Guid? guid, string? name)
    {
        var member = GetOrCreateLocked(guid, objectId, name);
        member.ObjectId = objectId is not null and not 0 ? objectId : member.ObjectId;
        member.Guid = guid is not null && guid != Guid.Empty ? guid : member.Guid;
        if (!string.IsNullOrWhiteSpace(name))
        {
            member.Name = name.Trim();
        }

        member.IsPartyMember = true;
        member.LastUpdatedAt = DateTimeOffset.Now;
        IndexLocked(member);
    }

    private void DisbandLocked()
    {
        foreach (var member in _membersByGuid.Values.Concat(_membersByObjectId.Values).Concat(_membersByName.Values).Distinct())
        {
            member.IsPartyMember = false;
        }

        if (!string.IsNullOrWhiteSpace(_localName) || _localGuid is not null || _localObjectId is not null)
        {
            SetLocalLocked(_localObjectId, _localGuid, _localName);
        }
    }

    private PartyMemberState GetOrCreateLocked(Guid? guid, long? objectId, string? name)
    {
        if (guid is not null && guid != Guid.Empty && _membersByGuid.TryGetValue(guid.Value, out var byGuid))
        {
            return byGuid;
        }

        if (objectId is not null and not 0 && _membersByObjectId.TryGetValue(objectId.Value, out var byObjectId))
        {
            return byObjectId;
        }

        if (!string.IsNullOrWhiteSpace(name) && _membersByName.TryGetValue(name.Trim(), out var byName))
        {
            return byName;
        }

        return new PartyMemberState();
    }

    private void IndexLocked(PartyMemberState member)
    {
        if (member.Guid is not null && member.Guid != Guid.Empty)
        {
            _membersByGuid[member.Guid.Value] = member;
        }

        if (member.ObjectId is not null and not 0)
        {
            _membersByObjectId[member.ObjectId.Value] = member;
        }

        if (!string.IsNullOrWhiteSpace(member.Name))
        {
            _membersByName[member.Name] = member;
        }
    }

    private PartyMemberSnapshot ToSnapshotLocked(PartyMemberState member)
    {
        return new PartyMemberSnapshot(
            member.Guid,
            member.ObjectId,
            string.IsNullOrWhiteSpace(member.Name) ? "(unknown)" : member.Name,
            member.GuildName,
            member.AverageItemPower,
            member.Equipment.Select(ToSlot).Where(x => x is not null).Cast<PartyEquipmentSlot>().ToArray(),
            member.LastUpdatedAt);
    }

    private PartyEquipmentSlot? ToSlot((string Slot, int ItemIndex) equipment)
    {
        if (equipment.ItemIndex <= 0)
        {
            return null;
        }

        var item = _gameData.GetItem(equipment.ItemIndex);
        return new PartyEquipmentSlot(
            equipment.Slot,
            equipment.ItemIndex,
            item?.DisplayName ?? $"item #{equipment.ItemIndex}",
            item?.UniqueName ?? string.Empty,
            item?.RenderUrl ?? string.Empty);
    }

    private static List<(string Slot, int ItemIndex)> NormalizeEquipment(IReadOnlyList<int> equipment)
    {
        var slots = new List<(string Slot, int ItemIndex)>();
        for (var i = 0; i < Math.Min(SlotNames.Length, equipment.Count); i++)
        {
            if (equipment[i] > 0)
            {
                slots.Add((SlotNames[i], equipment[i]));
            }
        }

        return slots;
    }

    private bool IsLocalLocked(PartyMemberState member)
    {
        return (member.ObjectId is not null && member.ObjectId == _localObjectId)
            || (member.Guid is not null && member.Guid == _localGuid)
            || (!string.IsNullOrWhiteSpace(member.Name)
                && !string.IsNullOrWhiteSpace(_localName)
                && string.Equals(member.Name, _localName, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsLocalSnapshot(PartyMemberSnapshot member)
    {
        return (member.ObjectId is not null && member.ObjectId == _localObjectId)
            || (member.Guid is not null && member.Guid == _localGuid)
            || (!string.IsNullOrWhiteSpace(member.Name)
                && !string.IsNullOrWhiteSpace(_localName)
                && string.Equals(member.Name, _localName, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class PartyMemberState
    {
        public Guid? Guid { get; set; }
        public long? ObjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string GuildName { get; set; } = string.Empty;
        public double? AverageItemPower { get; set; }
        public List<(string Slot, int ItemIndex)> Equipment { get; set; } = [];
        public int EquipmentPriority { get; set; }
        public bool IsPartyMember { get; set; }
        public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.Now;
    }
}
