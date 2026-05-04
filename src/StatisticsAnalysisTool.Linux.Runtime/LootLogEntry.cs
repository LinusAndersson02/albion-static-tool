namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed record LootLogEntry(
    DateTimeOffset Time,
    string LooterName,
    string SourceName,
    bool IsSilver,
    int ItemIndex,
    string ItemUniqueName,
    string ItemName,
    long Quantity,
    long EstimatedUnitValue,
    long EstimatedTotalValue,
    int Quality = 0);
