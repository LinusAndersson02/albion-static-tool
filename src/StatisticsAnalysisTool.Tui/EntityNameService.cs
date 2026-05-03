namespace StatisticsAnalysisTool.Tui;

public sealed class EntityNameService
{
    private readonly object _sync = new();
    private readonly Dictionary<long, string> _namesByObjectId = [];
    private readonly Dictionary<Guid, string> _namesByGuid = [];
    private readonly HashSet<Guid> _partyGuids = [];
    private readonly HashSet<string> _partyNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<long> _partyObjectIds = [];
    private long? _localObjectId;
    private Guid? _localGuid;
    private string _localName = string.Empty;

    public int KnownEntityCount
    {
        get
        {
            lock (_sync)
            {
                return _namesByObjectId.Count;
            }
        }
    }

    public long? LocalObjectId
    {
        get
        {
            lock (_sync)
            {
                return _localObjectId;
            }
        }
    }

    public string LocalName
    {
        get
        {
            lock (_sync)
            {
                return _localName;
            }
        }
    }

    public void SetLocalEntity(long? objectId, Guid? guid, string? name)
    {
        if (objectId is not null and not 0)
        {
            lock (_sync)
            {
                _localObjectId = objectId;
                _partyObjectIds.Add(objectId.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            lock (_sync)
            {
                _localName = name.Trim();
                _partyNames.Add(_localName);
            }
        }

        if (guid is not null && guid != Guid.Empty)
        {
            lock (_sync)
            {
                _localGuid = guid.Value;
                _partyGuids.Add(guid.Value);
            }
        }

        SetEntityName(objectId, name);
        SetGuidName(guid, name);
    }

    public bool IsLocalEntity(long? objectId)
    {
        if (objectId is null or 0)
        {
            return false;
        }

        lock (_sync)
        {
            return _localObjectId == objectId.Value;
        }
    }

    public void SetEntityName(long? objectId, string? name)
    {
        if (objectId is null or 0 || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        lock (_sync)
        {
            _namesByObjectId[objectId.Value] = name.Trim();
        }
    }

    public void SetEntity(long? objectId, Guid? guid, string? name)
    {
        SetEntityName(objectId, name);
        SetGuidName(guid, name);

        if (objectId is null or 0)
        {
            return;
        }

        lock (_sync)
        {
            if ((guid is not null && guid != Guid.Empty && _partyGuids.Contains(guid.Value))
                || (!string.IsNullOrWhiteSpace(name) && _partyNames.Contains(name.Trim())))
            {
                _partyObjectIds.Add(objectId.Value);
            }
        }
    }

    public void SetPartyMember(Guid? guid, string? name)
    {
        lock (_sync)
        {
            if (guid is not null && guid != Guid.Empty)
            {
                _partyGuids.Add(guid.Value);
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                _partyNames.Add(name.Trim());
            }

            RebuildPartyObjectIds();
        }

        SetGuidName(guid, name);
    }

    public void SetParty(IReadOnlyDictionary<Guid, string> members)
    {
        lock (_sync)
        {
            _partyGuids.Clear();
            _partyNames.Clear();
            _partyObjectIds.Clear();

            if (_localObjectId is not null and not 0)
            {
                _partyObjectIds.Add(_localObjectId.Value);
            }

            if (!string.IsNullOrWhiteSpace(_localName))
            {
                _partyNames.Add(_localName);
            }

            foreach (var (guid, name) in members)
            {
                if (guid != Guid.Empty)
                {
                    _partyGuids.Add(guid);
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    _partyNames.Add(name.Trim());
                    _namesByGuid[guid] = name.Trim();
                }
            }

            foreach (var (objectId, name) in _namesByObjectId)
            {
                if (_partyNames.Contains(name))
                {
                    _partyObjectIds.Add(objectId);
                }
            }
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
                ResetPartyToLocalLocked();
                return;
            }

            _partyGuids.Remove(guid.Value);
            if (_namesByGuid.TryGetValue(guid.Value, out var name))
            {
                _partyNames.Remove(name);
            }

            RebuildPartyObjectIds();
        }
    }

    public string Resolve(Guid? guid)
    {
        if (guid is null || guid == Guid.Empty)
        {
            return string.Empty;
        }

        lock (_sync)
        {
            return _namesByGuid.GetValueOrDefault(guid.Value) ?? string.Empty;
        }
    }

    public void ResetPartyToLocal()
    {
        lock (_sync)
        {
            ResetPartyToLocalLocked();
        }
    }

    public bool IsLocalGuid(Guid? guid)
    {
        if (guid is null || guid == Guid.Empty)
        {
            return false;
        }

        lock (_sync)
        {
            return _localGuid == guid.Value;
        }
    }

    public bool IsPartyEntity(long? objectId)
    {
        if (objectId is null or 0)
        {
            return false;
        }

        lock (_sync)
        {
            return _partyObjectIds.Contains(objectId.Value);
        }
    }

    public bool IsPartyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        lock (_sync)
        {
            return _partyNames.Contains(name.Trim());
        }
    }

    public bool IsLocalName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        lock (_sync)
        {
            return !string.IsNullOrWhiteSpace(_localName)
                && string.Equals(_localName, name.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetGuidName(Guid? guid, string? name)
    {
        if (guid is null || guid == Guid.Empty || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        lock (_sync)
        {
            _namesByGuid[guid.Value] = name.Trim();
            if (_partyGuids.Contains(guid.Value))
            {
                _partyNames.Add(name.Trim());
                RebuildPartyObjectIds();
            }
        }
    }

    public string Resolve(long entityId)
    {
        if (entityId == 0)
        {
            return "(unknown)";
        }

        lock (_sync)
        {
            return _namesByObjectId.TryGetValue(entityId, out var name)
                ? name
                : $"entity {entityId}";
        }
    }

    private void RebuildPartyObjectIds()
    {
        _partyObjectIds.Clear();
        if (_localObjectId is not null and not 0)
        {
            _partyObjectIds.Add(_localObjectId.Value);
        }

        foreach (var (objectId, name) in _namesByObjectId)
        {
            if (_partyNames.Contains(name))
            {
                _partyObjectIds.Add(objectId);
            }
        }
    }

    private void ResetPartyToLocalLocked()
    {
        _partyGuids.Clear();
        _partyNames.Clear();
        _partyObjectIds.Clear();

        if (_localGuid is not null && _localGuid != Guid.Empty)
        {
            _partyGuids.Add(_localGuid.Value);
        }

        if (_localObjectId is not null and not 0)
        {
            _partyObjectIds.Add(_localObjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(_localName))
        {
            _partyNames.Add(_localName);
        }
    }
}
