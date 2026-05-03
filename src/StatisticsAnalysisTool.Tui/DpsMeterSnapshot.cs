namespace StatisticsAnalysisTool.Tui;

public sealed record DpsMeterSnapshot(
    bool InCombat,
    DateTimeOffset? CombatStartedAt,
    DateTimeOffset? LastCombatEventAt,
    long TotalDamage,
    long TotalHeal,
    long TotalTakenDamage,
    IReadOnlyList<DpsMeterEntry> Entries);
