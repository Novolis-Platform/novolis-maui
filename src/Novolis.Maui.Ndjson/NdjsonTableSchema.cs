namespace Novolis.Maui.Ndjson;

/// <summary>Infers a bounded, log-oriented table schema from loaded records.</summary>
public static class NdjsonTableSchema
{
    private static readonly string[] PreferredFields =
    [
        "@timestamp", "timestamp", "time", "date", "level", "severity",
        "message", "msg", "event", "source", "logger", "id", "status",
    ];

    /// <summary>Maximum number of JSON fields shown as columns.</summary>
    public const int MaximumFieldColumns = 12;

    /// <summary>Returns the most useful fields in stable, frequency-aware order.</summary>
    public static IReadOnlyList<NdjsonTableColumn> Infer(
        IReadOnlyList<NdjsonRecordDisplay> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var stats = new Dictionary<string, FieldStats>(StringComparer.Ordinal);
        var position = 0;
        foreach (var record in records)
        {
            foreach (var name in record.TableValues.Keys)
            {
                if (stats.TryGetValue(name, out var current))
                {
                    stats[name] = current with { Count = current.Count + 1 };
                }
                else
                {
                    stats[name] = new FieldStats(1, position++);
                }
            }
        }

        return stats
            .OrderBy(static pair => PreferredIndex(pair.Key))
            .ThenByDescending(static pair => pair.Value.Count)
            .ThenBy(static pair => pair.Value.FirstSeen)
            .Take(MaximumFieldColumns)
            .Select(static pair => NdjsonTableColumn.ForField(pair.Key))
            .ToArray();
    }

    private static int PreferredIndex(string name)
    {
        for (var index = 0; index < PreferredFields.Length; index++)
        {
            if (string.Equals(name, PreferredFields[index], StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return PreferredFields.Length;
    }

    private readonly record struct FieldStats(int Count, int FirstSeen);
}
