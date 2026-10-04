namespace Novolis.Maui.Ndjson;

/// <summary>One virtualized table row and its slice-level column schema.</summary>
public sealed record NdjsonTableRow(
    NdjsonRecordDisplay Record,
    IReadOnlyList<NdjsonTableColumn> Columns)
{
    /// <summary>Returns the display value for a column or an em dash when absent.</summary>
    public string ValueFor(string columnName) =>
        Record.TableValues.TryGetValue(columnName, out var value) && value.Length > 0
            ? value
            : "—";
}
