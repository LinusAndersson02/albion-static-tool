namespace StatisticsAnalysisTool.Tui;

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
    long FactionPoints,
    double FactionPointsPerHour,
    long Might,
    double MightPerHour,
    long Favor,
    double FavorPerHour);
