namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed record ParserStatsSnapshot(
    long EventCount,
    long RequestCount,
    long ResponseCount);
