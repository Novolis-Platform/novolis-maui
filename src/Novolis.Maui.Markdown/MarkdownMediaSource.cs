namespace Novolis.Maui.Markdown;

/// <summary>Creates an <see cref="ImageSource"/> from a CSP-safe <c>data:</c> URI.</summary>
public static class MarkdownMediaSource
{
    /// <summary>Decodes a <c>data:</c> URI into a stream image source.</summary>
    public static ImageSource FromDataUri(string dataUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataUri);
        var comma = dataUri.IndexOf(',');
        if (comma < 0 || comma == dataUri.Length - 1)
            throw new InvalidDataException("Preview image was not a data URI.");

        var bytes = Convert.FromBase64String(dataUri[(comma + 1)..]);
        return ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
    }
}
