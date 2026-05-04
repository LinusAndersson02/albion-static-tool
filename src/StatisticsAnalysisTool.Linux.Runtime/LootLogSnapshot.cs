namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed record LootLogSnapshot(
    long TotalEvents,
    long TotalSilver,
    long TotalEstimatedValue,
    IReadOnlyList<LootLogEntry> RecentEntries);
