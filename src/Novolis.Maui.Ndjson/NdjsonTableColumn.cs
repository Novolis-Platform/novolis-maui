namespace Novolis.Maui.Ndjson;

/// <summary>A visible column in the bounded NDJSON table.</summary>
public sealed record NdjsonTableColumn(string Name, double Width)
{
    /// <summary>Width of the record-number column.</summary>
    public const double RecordWidth = 88;

    /// <summary>Width of the parse-status column.</summary>
    public const double StatusWidth = 106;

    /// <summary>Width of the row-actions column.</summary>
    public const double ActionsWidth = 154;

    /// <summary>Creates a readable field column with a bounded width.</summary>
    public static NdjsonTableColumn ForField(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var width = name.Length switch
        {
            <= 8 => 160,
            <= 18 => 190,
            _ => 240,
        };
        return new NdjsonTableColumn(name, width);
    }
}
