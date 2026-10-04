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
        TableValues = CreateTableValues(record);
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

    /// <summary>
    /// Compact top-level values used by the table row. Object and array values
    /// remain JSON, while scalar values are displayed without JSON quoting.
    /// </summary>
    public IReadOnlyDictionary<string, string> TableValues { get; }

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

    private static IReadOnlyDictionary<string, string> CreateTableValues(NdjsonRecord record)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (record.Json is not { } json)
        {
            values["raw"] = Truncate(record.Raw ?? string.Empty);
            if (record.Error is { Message: { Length: > 0 } message })
                values["error"] = Truncate(message);
            return values;
        }

        if (json.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in json.EnumerateObject())
                values[property.Name] = FormatTableValue(property.Value);
        }
        else
        {
            values["$value"] = FormatTableValue(json);
        }

        return values;
    }

    private static string FormatTableValue(JsonElement value)
    {
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Null => "null",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText(),
        };
        return Truncate(text);
    }

    private static string Truncate(string text) =>
        text.Length <= 512 ? text : $"{text[..512]}…";
}
