namespace StatisticsAnalysisTool.Tui;

public sealed class EstimatedItemValueService
{
    private const long FixPointScale = 10_000;
    private readonly object _sync = new();
    private readonly Dictionary<int, long> _unitValuesByItemIndex = [];

    public void Record(int? itemIndex, long? estimatedMarketValueInternal)
    {
        if (itemIndex is null or <= 0 || estimatedMarketValueInternal is null or <= 0)
        {
            return;
        }

        var value = estimatedMarketValueInternal.Value / FixPointScale;
        if (value <= 0)
        {
            return;
        }

        lock (_sync)
        {
            _unitValuesByItemIndex[itemIndex.Value] = value;
        }
    }

    public long GetUnitValue(int itemIndex)
    {
        if (itemIndex <= 0)
        {
            return 0;
        }

        lock (_sync)
        {
            return _unitValuesByItemIndex.GetValueOrDefault(itemIndex);
        }
    }
}
