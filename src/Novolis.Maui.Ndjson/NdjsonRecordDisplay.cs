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
    public string Details => _details ??= CreateDetails(Record);

    /// <summary>Text copied to the system clipboard.</summary>
    public string CopyText { get; }

    /// <summary>Text used by the bounded, current-slice filter.</summary>
    public string SearchText => CopyText;

    /// <summary>Approximate UTF-8 byte size of the source record.</summary>
    public int ByteLength =>
        System.Text.Encoding.UTF8.GetByteCount(CopyText);

    private string? _details;

    private static string CreatePreview(NdjsonRecord record)
    {
        var text = record.Json is { } json
            ? json.GetRawText()
            : record.Raw ?? string.Empty;
        return text.Length <= 640 ? text : $"{text[..640]}…";
    }

    private static string CreateDetails(NdjsonRecord record)
    {
        if (record.Json is { } json)
            return JsonSerializer.Serialize(json, PrettyJson);

        return $"{record.Raw ?? string.Empty}\n\n{record.Error?.Message}";
    }
}
