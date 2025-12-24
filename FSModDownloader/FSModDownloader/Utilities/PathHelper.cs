namespace FSModDownloader.Utilities;

/// <summary>
/// Helper methods for path operations.
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// Gets the configuration directory for the application.
    /// </summary>
    public static string GetAppConfigDirectory()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FSModDownloader");

        if (!Directory.Exists(configDir))
            Directory.CreateDirectory(configDir);

        return configDir;
    }

    /// <summary>
    /// Gets the mods cache directory.
    /// </summary>
    public static string GetModsCacheDirectory()
    {
        var cacheDir = Path.Combine(GetAppConfigDirectory(), "cache");

        if (!Directory.Exists(cacheDir))
            Directory.CreateDirectory(cacheDir);

        return cacheDir;
    }

    /// <summary>
    /// Gets the logs directory.
    /// </summary>
    public static string GetLogsDirectory()
    {
        var logsDir = Path.Combine(GetAppConfigDirectory(), "logs");

        if (!Directory.Exists(logsDir))
            Directory.CreateDirectory(logsDir);

        return logsDir;
    }

    /// <summary>
    /// Ensures a directory exists and is writable.
    /// </summary>
    public static bool IsDirectoryWritable(string path)
    {
        try
        {
            var testFile = Path.Combine(path, ".write_test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
