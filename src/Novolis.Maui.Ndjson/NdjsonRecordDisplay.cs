using System.Text.Json;
using Novolis.IO.Ndjson;

namespace Novolis.Maui.Ndjson;

/// <summary>Presentation values for one NDJSON record row.</summary>
public sealed class NdjsonRecordDisplay
{
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
    };

    /// <summary>Creates row presentation from a parsed record.</summary>
    public NdjsonRecordDisplay(NdjsonRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Record = record;
        IsValid = record.IsValid;
        Status = IsValid ? "VALID" : "MALFORMED";
        Preview = CreatePreview(record);
        Details = CreateDetails(record);
        CopyText = record.Json is { } json ? json.GetRawText() : record.Raw ?? string.Empty;
    }

    /// <summary>The source record.</summary>
    public NdjsonRecord Record { get; }

    /// <summary>Record number in the file.</summary>
    public long Number => Record.Number;

    /// <summary>Absolute byte offset of the line.</summary>
    public long ByteOffset => Record.ByteOffset;

    /// <summary>Whether the line parsed successfully.</summary>
    public bool IsValid { get; }

    /// <summary>Short status label.</summary>
    public string Status { get; }

    /// <summary>Compact row preview.</summary>
    public string Preview { get; }

    /// <summary>Expanded formatted JSON or malformed raw line.</summary>
    public string Details { get; }

    /// <summary>Text copied to the system clipboard.</summary>
    public string CopyText { get; }

    private static string CreatePreview(NdjsonRecord record)
    {
        if (record.Json is { } json)
            return json.GetRawText();

        return record.Raw ?? string.Empty;
    }

    private static string CreateDetails(NdjsonRecord record)
    {
        if (record.Json is { } json)
            return JsonSerializer.Serialize(json, PrettyJson);

        return $"{record.Raw ?? string.Empty}\n\n{record.Error?.Message}";
    }
}
