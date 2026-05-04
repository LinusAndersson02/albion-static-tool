namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed record DpsMeterEntry(
    long EntityId,
    long Damage,
    double Dps,
    long Heal,
    double Hps,
    long Overheal,
    long TakenDamage,
    int HitCount,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt);
