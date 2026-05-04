namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed class DpsMeterService
{
    private readonly object _sync = new();
    private readonly Dictionary<long, MutableDpsEntry> _entries = [];
    private readonly Dictionary<long, double> _lastPlayerHealth = [];
    private bool _inCombat;
    private DateTimeOffset? _combatStartedAt;
    private DateTimeOffset? _lastCombatEventAt;

    public void RecordCombatState(bool activeCombat, bool passiveCombat, DateTimeOffset now)
    {
        lock (_sync)
        {
            _inCombat = activeCombat || passiveCombat;
            if (_inCombat)
            {
                _combatStartedAt ??= now;
                _lastCombatEventAt = now;
            }
        }
    }

    public void RecordHealthUpdate(
        long affectedId,
        long causerId,
        double healthChange,
        double newHealthValue,
        int causingSpellIndex,
        DateTimeOffset now,
        bool causerInParty,
        bool affectedInParty)
    {
        if (causerId == 0 || affectedId == causerId || double.IsNaN(healthChange) || (!causerInParty && !affectedInParty))
        {
            return;
        }

        var roundedChange = (long)Math.Round(Math.Abs(healthChange), MidpointRounding.AwayFromZero);
        if (roundedChange <= 0)
        {
            return;
        }

        lock (_sync)
        {
            _combatStartedAt ??= now;
            _lastCombatEventAt = now;
            _inCombat = true;

            if (healthChange <= 0)
            {
                if (causerInParty)
                {
                    var causer = GetOrAdd(causerId, now);
                    causer.Damage += roundedChange;
                    causer.HitCount++;
                    causer.LastSeenAt = now;
                }

                if (affectedInParty)
                {
                    var affected = GetOrAdd(affectedId, now);
                    affected.TakenDamage += roundedChange;
                    affected.LastSeenAt = now;
                }
                return;
            }

            if (!causerInParty)
            {
                return;
            }

            var healer = GetOrAdd(causerId, now);
            if (IsMaxHealthReached(affectedId, newHealthValue))
            {
                healer.Overheal += roundedChange;
            }
            else
            {
                healer.Heal += roundedChange;
            }

            healer.HitCount++;
            healer.LastSeenAt = now;
        }
    }

    public DpsMeterSnapshot GetSnapshot(int maxEntries = 12)
    {
        lock (_sync)
        {
            var entries = _entries.Values
                .Select(ToEntry)
                .OrderByDescending(x => x.Damage)
                .ThenByDescending(x => x.Heal)
                .Take(maxEntries)
                .ToList();

            return new DpsMeterSnapshot(
                _inCombat,
                _combatStartedAt,
                _lastCombatEventAt,
                entries.Sum(x => x.Damage),
                entries.Sum(x => x.Heal),
                entries.Sum(x => x.Overheal),
                entries.Sum(x => x.TakenDamage),
                entries);
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _entries.Clear();
            _lastPlayerHealth.Clear();
            _inCombat = false;
            _combatStartedAt = null;
            _lastCombatEventAt = null;
        }
    }

    private MutableDpsEntry GetOrAdd(long entityId, DateTimeOffset now)
    {
        if (_entries.TryGetValue(entityId, out var entry))
        {
            return entry;
        }

        entry = new MutableDpsEntry(entityId, now);
        _entries[entityId] = entry;
        return entry;
    }

    private static DpsMeterEntry ToEntry(MutableDpsEntry entry)
    {
        var duration = Math.Max(1, (entry.LastSeenAt - entry.FirstSeenAt).TotalSeconds);
        return new DpsMeterEntry(
            entry.EntityId,
            entry.Damage,
            entry.Damage / duration,
            entry.Heal,
            entry.Heal / duration,
            entry.Overheal,
            entry.TakenDamage,
            entry.HitCount,
            entry.FirstSeenAt,
            entry.LastSeenAt);
    }

    private sealed class MutableDpsEntry
    {
        public MutableDpsEntry(long entityId, DateTimeOffset now)
        {
            EntityId = entityId;
            FirstSeenAt = now;
            LastSeenAt = now;
        }

        public long EntityId { get; }
        public long Damage { get; set; }
        public long Heal { get; set; }
        public long Overheal { get; set; }
        public long TakenDamage { get; set; }
        public int HitCount { get; set; }
        public DateTimeOffset FirstSeenAt { get; }
        public DateTimeOffset LastSeenAt { get; set; }
    }

    private bool IsMaxHealthReached(long objectId, double newHealthValue)
    {
        if (_lastPlayerHealth.TryGetValue(objectId, out var lastHealth) && lastHealth.CompareTo(newHealthValue) == 0)
        {
            return true;
        }

        _lastPlayerHealth[objectId] = newHealthValue;
        return false;
    }
}
