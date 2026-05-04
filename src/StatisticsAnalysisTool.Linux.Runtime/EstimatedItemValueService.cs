namespace StatisticsAnalysisTool.Linux.Runtime;

public sealed class EstimatedItemValueService
{
    private const long FixPointScale = 10_000;
    private readonly object _sync = new();
    private readonly Dictionary<int, long> _unitValuesByItemIndex = [];
    private readonly Dictionary<(int ItemIndex, int Quality), long> _unitValuesByItemAndQuality = [];

    public void Record(int? itemIndex, long? estimatedMarketValueInternal)
    {
        Record(itemIndex, estimatedMarketValueInternal, quality: null);
    }

    public void Record(int? itemIndex, long? estimatedMarketValueInternal, int? quality)
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
            if (quality is > 0)
            {
                _unitValuesByItemAndQuality[(itemIndex.Value, quality.Value)] = value;
            }
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

    public long GetUnitValue(int itemIndex, int quality)
    {
        if (itemIndex <= 0)
        {
            return 0;
        }

        lock (_sync)
        {
            return quality > 0 && _unitValuesByItemAndQuality.TryGetValue((itemIndex, quality), out var value)
                ? value
                : _unitValuesByItemIndex.GetValueOrDefault(itemIndex);
        }
    }
}
