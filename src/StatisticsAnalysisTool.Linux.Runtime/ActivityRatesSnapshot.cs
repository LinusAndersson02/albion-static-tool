namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed record ActivityRatesSnapshot(
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastEventAt,
    double ActiveHours,
    long Silver,
    double SilverPerHour,
    long Fame,
    double FamePerHour,
    long ReSpecPoints,
    double ReSpecPointsPerHour,
    long PaidSilverForReSpec,
    double PaidSilverForReSpecPerHour,
    long FactionPoints,
    double FactionPointsPerHour,
    long FactionStanding,
    double FactionStandingPerHour,
    long Might,
    double MightPerHour,
    long Favor,
    double FavorPerHour);
