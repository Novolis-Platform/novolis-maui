namespace Novolis.Maui.Agent;

/// <summary>Writes unhandled faults under LocalAppData without opening an editor window.</summary>
public static class CrashGuard
{
    private static string _appName = "Novolis.Maui";
    private static bool _installed;

    /// <summary>Directory for crash logs.</summary>
    public static string CrashDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            Sanitize(_appName),
            "crashes");

    /// <summary>Installs process-level handlers once.</summary>
    public static void Install(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        _appName = appName.Trim();
        if (_installed)
            return;
        _installed = true;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                ReportSilent(exception, "AppDomain.UnhandledException");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ReportSilent(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };
    }

    /// <summary>Writes a fault without interrupting the UI host.</summary>
    public static void ReportSilent(Exception exception, string source)
    {
        try
        {
            Directory.CreateDirectory(CrashDirectory);
            var path = Path.Combine(
                CrashDirectory,
                $"crash-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Sanitize(source)}.log");
            File.WriteAllText(path, $"{source}{Environment.NewLine}{exception}");
        }
        catch
        {
            // ignore secondary logging failures
        }
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray());
    }
}
