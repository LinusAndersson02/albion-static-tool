namespace StatisticsAnalysisTool.Tui;

public sealed record ParserStatsSnapshot(
    long EventCount,
    long RequestCount,
    long ResponseCount);
