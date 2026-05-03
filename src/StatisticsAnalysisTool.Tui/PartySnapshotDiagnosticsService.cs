using System.Collections;

namespace StatisticsAnalysisTool.Tui;

public sealed record PartySnapshotDiagnostic(
    DateTimeOffset Time,
    string Source,
    int CandidateCount,
    string Shape);

public sealed class PartySnapshotDiagnosticsService
{
    private const int MaxEntries = 40;
    private readonly object _sync = new();
    private readonly Queue<PartySnapshotDiagnostic> _entries = new();

    public void Record(string source, IReadOnlyDictionary<byte, object> parameters, int candidateCount)
    {
        lock (_sync)
        {
            _entries.Enqueue(new PartySnapshotDiagnostic(
                DateTimeOffset.Now,
                source,
                candidateCount,
                FormatDictionary(parameters, 0)));

            while (_entries.Count > MaxEntries)
            {
                _entries.Dequeue();
            }
        }
    }

    public IReadOnlyList<PartySnapshotDiagnostic> GetRecent()
    {
        lock (_sync)
        {
            return _entries.Reverse().ToArray();
        }
    }

    private static string FormatValue(object? value, int depth)
    {
        if (value is null)
        {
            return "null";
        }

        if (depth >= 3)
        {
            return value.GetType().Name;
        }

        if (value is string text)
        {
            return $"string:{Trim(text)}";
        }

        if (value is byte[] bytes)
        {
            return $"byte[{bytes.Length}]";
        }

        if (value is IDictionary dictionary)
        {
            return FormatDictionary(dictionary, depth + 1);
        }

        if (value is IEnumerable enumerable)
        {
            var values = enumerable.Cast<object?>().Take(12).Select(x => FormatValue(x, depth + 1));
            return $"{value.GetType().Name}[{string.Join(",", values)}]";
        }

        return $"{value.GetType().Name}:{Trim(value.ToString() ?? string.Empty)}";
    }

    private static string FormatDictionary(IDictionary dictionary, int depth)
    {
        var parts = new List<string>();
        foreach (DictionaryEntry entry in dictionary)
        {
            parts.Add($"{entry.Key}={FormatValue(entry.Value, depth + 1)}");
        }

        return "{" + string.Join(" ", parts.Take(40)) + "}";
    }

    private static string FormatDictionary(IReadOnlyDictionary<byte, object> dictionary, int depth)
    {
        return "{" + string.Join(" ", dictionary.OrderBy(x => x.Key).Select(x => $"{x.Key}={FormatValue(x.Value, depth + 1)}")) + "}";
    }

    private static string Trim(string value)
    {
        value = value.Replace('\r', ' ').Replace('\n', ' ');
        return value.Length <= 80 ? value : value[..80] + "...";
    }
}
