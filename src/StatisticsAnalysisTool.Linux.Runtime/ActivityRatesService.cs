namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed class ActivityRatesService
{
    private readonly object _sync = new();
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _lastEventAt;
    private long _silver;
    private long _fame;
    private long _reSpecPoints;
    private long _paidSilverForReSpec;
    private long _factionPoints;
    private long _factionStanding;
    private long _might;
    private long _favor;

    public void AddSilver(long silver, DateTimeOffset now) => Add(ref _silver, silver, now);

    public void AddFame(long fame, DateTimeOffset now) => Add(ref _fame, fame, now);

    public void AddReSpecPoints(long points, DateTimeOffset now) => Add(ref _reSpecPoints, points, now);

    public void AddPaidSilverForReSpec(long silver, DateTimeOffset now) => Add(ref _paidSilverForReSpec, silver, now);

    public void AddFactionPoints(long points, DateTimeOffset now) => Add(ref _factionPoints, points, now);

    public void AddFactionStanding(long standing, DateTimeOffset now) => Add(ref _factionStanding, standing, now);

    public void AddMight(long might, DateTimeOffset now) => Add(ref _might, might, now);

    public void AddFavor(long favor, DateTimeOffset now) => Add(ref _favor, favor, now);

    public ActivityRatesSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            var hours = GetActiveHours();
            return new ActivityRatesSnapshot(
                _startedAt,
                _lastEventAt,
                hours,
                _silver,
                PerHour(_silver, hours),
                _fame,
                PerHour(_fame, hours),
                _reSpecPoints,
                PerHour(_reSpecPoints, hours),
                _paidSilverForReSpec,
                PerHour(_paidSilverForReSpec, hours),
                _factionPoints,
                PerHour(_factionPoints, hours),
                _factionStanding,
                PerHour(_factionStanding, hours),
                _might,
                PerHour(_might, hours),
                _favor,
                PerHour(_favor, hours));
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _startedAt = null;
            _lastEventAt = null;
            _silver = 0;
            _fame = 0;
            _reSpecPoints = 0;
            _paidSilverForReSpec = 0;
            _factionPoints = 0;
            _factionStanding = 0;
            _might = 0;
            _favor = 0;
        }
    }

    private void Add(ref long target, long value, DateTimeOffset now)
    {
        if (value <= 0)
        {
            return;
        }

        lock (_sync)
        {
            _startedAt ??= now;
            _lastEventAt = now;
            target += value;
        }
    }

    private double GetActiveHours()
    {
        if (_startedAt is null)
        {
            return 0;
        }

        var end = _lastEventAt ?? DateTimeOffset.Now;
        return Math.Max((end - _startedAt.Value).TotalHours, 1.0 / 3600.0);
    }

    private static double PerHour(long value, double hours)
    {
        return hours <= 0 ? 0 : value / hours;
    }
}
