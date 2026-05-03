namespace StatisticsAnalysisTool.Tui;

public sealed record DpsMeterEntry(
    long EntityId,
    long Damage,
    double Dps,
    long Heal,
    double Hps,
    long TakenDamage,
    int HitCount,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt);
