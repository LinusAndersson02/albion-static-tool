namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed record SessionTimelineEvent(
    long Id,
    DateTimeOffset Time,
    string Type,
    string Text,
    string PlayerName,
    long? Silver);

public sealed record LootSplitShare(string PlayerName, long Silver, double ActiveSeconds);

public sealed record TimelineValueEvent(DateTimeOffset Time, long Value, string Label);

public sealed record LootValueSplitShare(string PlayerName, long Value);

public sealed record LootSplitInterval(
    DateTimeOffset From,
    DateTimeOffset To,
    long SilverDelta,
    IReadOnlyList<string> Participants,
    long SharePerParticipant);

public sealed record SessionTimelineSnapshot(
    IReadOnlyList<SessionTimelineEvent> Events,
    IReadOnlyList<string> ActivePlayers,
    long? LatestSilver,
    long SplitSilver,
    long SplitLootValue,
    IReadOnlyList<LootSplitShare> Shares,
    IReadOnlyList<LootValueSplitShare> ValueShares,
    IReadOnlyList<LootSplitInterval> Intervals);

public sealed class SessionTimelineService
{
    private readonly object _sync = new();
    private readonly List<SessionTimelineEvent> _events = [];
    private readonly Dictionary<string, DateTimeOffset> _activeSince = new(StringComparer.OrdinalIgnoreCase);
    private long _nextId = 1;

    public void SetLocalPlayer(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        lock (_sync)
        {
            EnsureActiveLocked(name.Trim(), DateTimeOffset.Now, addEvent: true);
        }
    }

    public void SetParty(IReadOnlyCollection<string> names)
    {
        var now = DateTimeOffset.Now;
        lock (_sync)
        {
            var cleanNames = names
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var name in cleanNames)
            {
                EnsureActiveLocked(name, now, addEvent: true);
            }

            foreach (var activeName in _activeSince.Keys.ToArray())
            {
                if (!cleanNames.Contains(activeName, StringComparer.OrdinalIgnoreCase))
                {
                    RemoveActiveLocked(activeName, now);
                }
            }
        }
    }

    public void PlayerJoined(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        lock (_sync)
        {
            EnsureActiveLocked(name.Trim(), DateTimeOffset.Now, addEvent: true);
        }
    }

    public void PlayerLeft(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        lock (_sync)
        {
            RemoveActiveLocked(name.Trim(), DateTimeOffset.Now);
        }
    }

    public void PartyDisbanded()
    {
        var now = DateTimeOffset.Now;
        lock (_sync)
        {
            foreach (var name in _activeSince.Keys.ToArray())
            {
                RemoveActiveLocked(name, now);
            }

            AddEventLocked(now, "party", "Party disbanded", string.Empty, null);
        }
    }

    public void AddSilverCheckpoint(long silver, string? note)
    {
        lock (_sync)
        {
            var text = string.IsNullOrWhiteSpace(note)
                ? $"Silver loot: {silver:N0}"
                : $"Silver loot: {silver:N0} - {note.Trim()}";
            AddEventLocked(DateTimeOffset.Now, "silver", text, string.Empty, silver);
        }
    }

    public SessionTimelineSnapshot GetSnapshot(int maxEvents = 80, IReadOnlyList<TimelineValueEvent>? valueEvents = null)
    {
        lock (_sync)
        {
            var shares = CalculateSharesLocked(out var intervals, out var splitSilver);
            var valueShares = CalculateValueSharesLocked(valueEvents ?? [], out var splitLootValue);
            return new SessionTimelineSnapshot(
                _events.OrderByDescending(x => x.Time).Take(maxEvents).ToArray(),
                _activeSince.Keys.OrderBy(x => x).ToArray(),
                _events.LastOrDefault(x => x.Silver is not null)?.Silver,
                splitSilver,
                splitLootValue,
                shares,
                valueShares,
                intervals);
        }
    }

    public void Reset(string? localPlayerName)
    {
        lock (_sync)
        {
            _events.Clear();
            _activeSince.Clear();
            _nextId = 1;
        }

        SetLocalPlayer(localPlayerName);
    }

    private void EnsureActiveLocked(string name, DateTimeOffset now, bool addEvent)
    {
        if (_activeSince.ContainsKey(name))
        {
            return;
        }

        _activeSince[name] = now;
        if (addEvent)
        {
            AddEventLocked(now, "join", $"{name} joined", name, null);
        }
    }

    private void RemoveActiveLocked(string name, DateTimeOffset now)
    {
        if (!_activeSince.Remove(name))
        {
            return;
        }

        AddEventLocked(now, "leave", $"{name} left", name, null);
    }

    private void AddEventLocked(DateTimeOffset time, string type, string text, string playerName, long? silver)
    {
        _events.Add(new SessionTimelineEvent(_nextId++, time, type, text, playerName, silver));
    }

    private IReadOnlyList<LootSplitShare> CalculateSharesLocked(out IReadOnlyList<LootSplitInterval> intervals, out long splitSilver)
    {
        var silverEvents = _events
            .Where(x => x.Silver is > 0)
            .OrderBy(x => x.Time)
            .ToArray();

        if (silverEvents.Length == 0)
        {
            intervals = [];
            splitSilver = 0;
            return [];
        }

        var shares = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var intervalList = new List<LootSplitInterval>();
        splitSilver = 0;

        foreach (var silverEvent in silverEvents)
        {
            var participants = GetActiveAtLocked(silverEvent.Time);
            if (participants.Count == 0)
            {
                continue;
            }

            var silver = silverEvent.Silver!.Value;
            var share = silver / participants.Count;

            if (share <= 0)
            {
                continue;
            }

            splitSilver += share * participants.Count;
            foreach (var participant in participants)
            {
                shares[participant] = shares.GetValueOrDefault(participant) + share;
            }

            intervalList.Add(new LootSplitInterval(
                silverEvent.Time,
                silverEvent.Time,
                silver,
                participants,
                share));
        }

        intervals = intervalList.OrderByDescending(x => x.To).ToArray();
        return shares
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Select(x => new LootSplitShare(x.Key, x.Value, 0d))
            .ToArray();
    }

    private IReadOnlyList<LootValueSplitShare> CalculateValueSharesLocked(IReadOnlyList<TimelineValueEvent> valueEvents, out long splitLootValue)
    {
        var shares = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        splitLootValue = 0;

        foreach (var valueEvent in valueEvents.Where(x => x.Value > 0).OrderBy(x => x.Time))
        {
            var participants = GetActiveAtLocked(valueEvent.Time);
            if (participants.Count == 0)
            {
                continue;
            }

            var share = valueEvent.Value / participants.Count;
            if (share <= 0)
            {
                continue;
            }

            splitLootValue += share * participants.Count;
            foreach (var participant in participants)
            {
                shares[participant] = (shares.TryGetValue(participant, out var current) ? current : 0) + share;
            }
        }

        return shares
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Select(x => new LootValueSplitShare(x.Key, x.Value))
            .ToArray();
    }

    private IReadOnlyDictionary<string, double> GetParticipantSecondsForIntervalLocked(DateTimeOffset from, DateTimeOffset to)
    {
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seconds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var orderedEvents = _events.OrderBy(x => x.Time).ToArray();

        foreach (var timelineEvent in orderedEvents.Where(x => x.Time <= from))
        {
            ApplyActiveEvent(active, timelineEvent);
        }

        var cursor = from;
        foreach (var timelineEvent in orderedEvents.Where(x => x.Time > from && x.Time < to))
        {
            AddSeconds(seconds, active, (timelineEvent.Time - cursor).TotalSeconds);
            ApplyActiveEvent(active, timelineEvent);
            cursor = timelineEvent.Time;
        }

        AddSeconds(seconds, active, (to - cursor).TotalSeconds);
        return seconds;
    }

    private IReadOnlyList<string> GetActiveAtLocked(DateTimeOffset time)
    {
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var timelineEvent in _events.OrderBy(x => x.Time).Where(x => x.Time <= time))
        {
            ApplyActiveEvent(active, timelineEvent);
        }

        return active.OrderBy(x => x).ToArray();
    }

    private static void AddSeconds(IDictionary<string, double> seconds, IEnumerable<string> active, double durationSeconds)
    {
        if (durationSeconds <= 0)
        {
            return;
        }

        foreach (var name in active)
        {
            seconds[name] = (seconds.TryGetValue(name, out var current) ? current : 0) + durationSeconds;
        }
    }

    private static void ApplyActiveEvent(ISet<string> active, SessionTimelineEvent timelineEvent)
    {
        if (string.IsNullOrWhiteSpace(timelineEvent.PlayerName))
        {
            return;
        }

        if (timelineEvent.Type == "join")
        {
            active.Add(timelineEvent.PlayerName);
        }
        else if (timelineEvent.Type == "leave")
        {
            active.Remove(timelineEvent.PlayerName);
        }
    }
}
