using System.Collections;

namespace StatisticsAnalysisTool.Tui;

internal sealed record PartyEquipmentSnapshotCandidate(
    Guid? Guid,
    string Name,
    double? ItemPower,
    IReadOnlyList<int> Equipment);

internal static class PartyEquipmentSnapshotParser
{
    private const int EquipmentSlotCount = 10;
    private const int MaxDepth = 8;

    public static IReadOnlyList<PartyEquipmentSnapshotCandidate> Parse(IReadOnlyDictionary<byte, object> parameters)
    {
        var candidates = new List<PartyEquipmentSnapshotCandidate>();
        Scan(parameters, candidates, 0);

        return candidates
            .Where(x => x.Equipment.Count >= EquipmentSlotCount)
            .GroupBy(x => $"{x.Guid}:{x.Name}:{string.Join(",", x.Equipment.Take(EquipmentSlotCount))}")
            .Select(x => x.OrderByDescending(candidate => candidate.ItemPower.HasValue).First())
            .ToArray();
    }

    private static void Scan(object? value, ICollection<PartyEquipmentSnapshotCandidate> candidates, int depth)
    {
        if (value is null || depth > MaxDepth || value is string)
        {
            return;
        }

        if (value is IDictionary dictionary)
        {
            TryAddCandidate(dictionary, candidates);

            foreach (DictionaryEntry entry in dictionary)
            {
                Scan(entry.Value, candidates, depth + 1);
            }

            return;
        }

        if (value is IEnumerable enumerable and not byte[])
        {
            foreach (var item in enumerable)
            {
                Scan(item, candidates, depth + 1);
            }
        }
    }

    private static void TryAddCandidate(IDictionary dictionary, ICollection<PartyEquipmentSnapshotCandidate> candidates)
    {
        Guid? guid = null;
        var name = string.Empty;
        double? itemPower = null;
        List<int> equipment = [];

        foreach (DictionaryEntry entry in dictionary)
        {
            var value = entry.Value;
            guid ??= PacketValueReader.ToGuid(value);

            if (string.IsNullOrWhiteSpace(name) && IsLikelyPlayerName(value))
            {
                name = value?.ToString()?.Trim() ?? string.Empty;
            }

            if (equipment.Count == 0 && TryReadEquipment(value, out var values))
            {
                equipment = values;
            }

            itemPower ??= TryReadItemPower(entry.Key, value);
        }

        if ((guid is not null || !string.IsNullOrWhiteSpace(name)) && equipment.Count >= EquipmentSlotCount)
        {
            candidates.Add(new PartyEquipmentSnapshotCandidate(guid, name, itemPower, equipment));
        }
    }

    private static double? TryReadItemPower(object key, object? value)
    {
        if (value is null || value is string || value is IDictionary || value is IEnumerable)
        {
            return null;
        }

        if (PacketValueReader.ToGuid(value) is not null)
        {
            return null;
        }

        var itemPower = PacketValueReader.ToDouble(value);
        if (itemPower is null or < 100 or > 3_000)
        {
            return null;
        }

        // Inspect and equipment snapshot payloads normally keep item power in a small scalar field.
        // This preference keeps unrelated role/order flags out while still accepting changed key ids.
        if (PacketValueReader.ToInt(key) is 3 or 4 or 5 or 6 or 7 or 8 or 9)
        {
            return itemPower;
        }

        return value is float or double or decimal ? itemPower : null;
    }

    private static bool TryReadEquipment(object? value, out List<int> equipment)
    {
        equipment = [];
        if (value is null || value is string)
        {
            return false;
        }

        if (PacketValueReader.ToGuid(value) is not null)
        {
            return false;
        }

        if (value is IDictionary dictionary)
        {
            var indexed = new List<int>();
            foreach (DictionaryEntry entry in dictionary)
            {
                if (PacketValueReader.ToInt(entry.Key) is not { } index || index < 0)
                {
                    return false;
                }

                while (indexed.Count <= index)
                {
                    indexed.Add(0);
                }

                indexed[index] = PacketValueReader.ToInt(entry.Value) ?? 0;
            }

            return AcceptEquipment(indexed, out equipment);
        }

        if (value is IEnumerable enumerable)
        {
            var values = new List<int>();
            foreach (var item in enumerable)
            {
                values.Add(PacketValueReader.ToInt(item) ?? 0);
            }

            return AcceptEquipment(values, out equipment);
        }

        return false;
    }

    private static bool AcceptEquipment(List<int> values, out List<int> equipment)
    {
        equipment = [];
        if (values.Count < EquipmentSlotCount)
        {
            return false;
        }

        var firstSlots = values.Take(EquipmentSlotCount).ToArray();
        var positiveCount = firstSlots.Count(x => x > 0);
        if (positiveCount < 3 || firstSlots.Any(x => x < -1 || x > 200_000))
        {
            return false;
        }

        equipment = firstSlots.Select(x => Math.Max(0, x)).ToList();
        return true;
    }

    private static bool IsLikelyPlayerName(object? value)
    {
        if (value is not string text)
        {
            return false;
        }

        text = text.Trim();
        return text.Length is >= 3 and <= 32
            && text.All(x => char.IsLetterOrDigit(x) || x is '_' or '-')
            && text.Any(char.IsLetter);
    }
}
