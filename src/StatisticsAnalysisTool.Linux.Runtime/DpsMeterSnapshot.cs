namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed record DpsMeterSnapshot(
    bool InCombat,
    DateTimeOffset? CombatStartedAt,
    DateTimeOffset? LastCombatEventAt,
    long TotalDamage,
    long TotalHeal,
    long TotalOverheal,
    long TotalTakenDamage,
    IReadOnlyList<DpsMeterEntry> Entries);
