using System.Collections;
using System.Globalization;

namespace StatisticsAnalysisTool.Linux.Runtime;

internal static class PacketValueReader
{
    public static long? GetLong(IReadOnlyDictionary<byte, object> parameters, byte key)
    {
        return parameters.TryGetValue(key, out var value) ? ToLong(value) : null;
    }

    public static int? GetInt(IReadOnlyDictionary<byte, object> parameters, byte key)
    {
        return parameters.TryGetValue(key, out var value) ? ToInt(value) : null;
    }

    public static double? GetDouble(IReadOnlyDictionary<byte, object> parameters, byte key)
    {
        return parameters.TryGetValue(key, out var value) ? ToDouble(value) : null;
    }

    public static bool? GetBool(IReadOnlyDictionary<byte, object> parameters, byte key)
    {
        return parameters.TryGetValue(key, out var value) ? ToBool(value) : null;
    }

    public static string GetString(IReadOnlyDictionary<byte, object> parameters, byte key)
    {
        return parameters.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
    }

    public static Guid? GetGuid(IReadOnlyDictionary<byte, object> parameters, byte key)
    {
        return parameters.TryGetValue(key, out var value) ? ToGuid(value) : null;
    }

    public static List<Guid> GetGuidList(IReadOnlyDictionary<byte, object> parameters, byte key)
    {
        if (!parameters.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        if (value is byte[] bytes)
        {
            var guids = new List<Guid>();
            for (var i = 0; i + 16 <= bytes.Length; i += 16)
            {
                var guidBytes = new byte[16];
                Array.Copy(bytes, i, guidBytes, 0, 16);
                guids.Add(new Guid(guidBytes));
            }

            return guids;
        }

        if (value is IEnumerable enumerable and not string)
        {
            var guids = new List<Guid>();
            foreach (var item in enumerable)
            {
                if (ToGuid(item) is { } guid)
                {
                    guids.Add(guid);
                }
            }

            return guids;
        }

        return [];
    }

    public static List<string> GetStringList(IReadOnlyDictionary<byte, object> parameters, byte key)
    {
        if (!parameters.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        if (value is IEnumerable enumerable and not string)
        {
            var values = new List<string>();
            foreach (var item in enumerable)
            {
                values.Add(item?.ToString() ?? string.Empty);
            }

            return values;
        }

        return [value.ToString() ?? string.Empty];
    }

    public static List<T> GetIndexedValues<T>(IReadOnlyDictionary<byte, object> parameters, byte key, Func<object, T?> convert)
        where T : struct
    {
        if (!parameters.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        var values = new List<T>();
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (ToInt(entry.Key) is not { } index || index < 0)
                {
                    continue;
                }

                EnsureSize(values, index + 1);
                if (entry.Value is not null && convert(entry.Value) is { } typedValue)
                {
                    values[index] = typedValue;
                }
            }

            return values;
        }

        if (value is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                values.Add(item is null ? default : convert(item) ?? default);
            }
        }

        return values;
    }

    public static long? ToLong(object? value)
    {
        return value switch
        {
            null => null,
            byte x => x,
            short x => x,
            int x => x,
            long x => x,
            float x => checked((long)x),
            double x => checked((long)x),
            decimal x => checked((long)x),
            string x when long.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => TryConvert<long>(value)
        };
    }

    public static int? ToInt(object? value)
    {
        return value switch
        {
            null => null,
            byte x => x,
            short x => x,
            int x => x,
            long x when x is <= int.MaxValue and >= int.MinValue => (int)x,
            string x when int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => TryConvert<int>(value)
        };
    }

    public static double? ToDouble(object? value)
    {
        return value switch
        {
            null => null,
            byte x => x,
            short x => x,
            int x => x,
            long x => x,
            float x => x,
            double x => x,
            decimal x => (double)x,
            string x when double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => TryConvert<double>(value)
        };
    }

    public static Guid? ToGuid(object? value)
    {
        return value switch
        {
            null => null,
            Guid x => x,
            byte[] x when x.Length == 16 => new Guid(x),
            string x when Guid.TryParse(x, out var parsed) => parsed,
            _ => null
        };
    }

    private static bool? ToBool(object? value)
    {
        return value switch
        {
            null => null,
            bool x => x,
            byte x => x != 0,
            short x => x != 0,
            int x => x != 0,
            long x => x != 0,
            string x when bool.TryParse(x, out var parsed) => parsed,
            _ => TryConvert<bool>(value)
        };
    }

    private static T? TryConvert<T>(object value)
        where T : struct
    {
        try
        {
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureSize<T>(List<T> values, int count)
    {
        while (values.Count < count)
        {
            values.Add(default!);
        }
    }
}
