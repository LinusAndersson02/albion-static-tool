namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed class LootLogService
{
    private readonly object _sync = new();
    private readonly Queue<LootLogEntry> _entries = new();
    private long _totalEvents;
    private long _totalSilver;
    private long _totalEstimatedValue;
    private const int MaxEntries = 200;

    public void Record(LootLogEntry entry)
    {
        lock (_sync)
        {
            _entries.Enqueue(entry);
            _totalEvents++;
            _totalEstimatedValue += Math.Max(0, entry.EstimatedTotalValue);

            while (_entries.Count > MaxEntries)
            {
                _entries.Dequeue();
            }
        }
    }

    public LootLogSnapshot GetSnapshot(int recentCount = 12)
    {
        lock (_sync)
        {
            return new LootLogSnapshot(
                _totalEvents,
                _totalSilver,
                _totalEstimatedValue,
                _entries
                    .Reverse()
                    .Take(recentCount)
                    .ToList());
        }
    }

    public IReadOnlyList<LootLogEntry> GetEntries()
    {
        lock (_sync)
        {
            return _entries.ToList();
        }
    }

    public IReadOnlyList<TimelineValueEvent> GetValueEvents()
    {
        lock (_sync)
        {
            return _entries
                .Where(x => x.EstimatedTotalValue > 0)
                .Select(x => new TimelineValueEvent(x.Time, x.EstimatedTotalValue, x.ItemName))
                .ToArray();
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _entries.Clear();
            _totalEvents = 0;
            _totalSilver = 0;
            _totalEstimatedValue = 0;
        }
    }
}
