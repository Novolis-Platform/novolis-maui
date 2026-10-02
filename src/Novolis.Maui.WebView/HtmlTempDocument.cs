namespace Novolis.Maui.WebView;

/// <summary>Writes HTML to a temp file so WebView2 can load documents that exceed NavigateToString limits.</summary>
public static class HtmlTempDocument
{
    /// <summary>Directory under the user temp folder used for viewer documents.</summary>
    public static string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "Novolis", "html-preview");

    /// <summary>Writes <paramref name="html"/> and returns the <c>file:</c> URI.</summary>
    public static string Write(string html)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + ".html");
        File.WriteAllText(path, html);
        return new Uri(path).AbsoluteUri;
    }

    /// <summary>Deletes a previous temp document created by <see cref="Write"/>.</summary>
    public static void TryDelete(string? fileUri)
    {
        if (string.IsNullOrWhiteSpace(fileUri)
            || !Uri.TryCreate(fileUri, UriKind.Absolute, out var uri)
            || !uri.IsFile)
        {
            return;
        }

        try
        {
            if (File.Exists(uri.LocalPath))
                File.Delete(uri.LocalPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>True when <paramref name="url"/> is the current temp document.</summary>
    public static bool IsSameDocument(string? url, string? fileUri)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(fileUri))
            return false;
        if (string.Equals(url, fileUri, StringComparison.OrdinalIgnoreCase))
            return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var left)
            && Uri.TryCreate(fileUri, UriKind.Absolute, out var right)
            && left.Equals(right);
    }
}
