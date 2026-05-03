namespace StatisticsAnalysisTool.Tui;

public sealed record LootLogSnapshot(
    long TotalEvents,
    long TotalSilver,
    long TotalEstimatedValue,
    IReadOnlyList<LootLogEntry> RecentEntries);
