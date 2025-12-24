namespace FSModDownloader.Utilities;

/// <summary>
/// Helper methods for file operations.
/// </summary>
public static class FileHelper
{
    /// <summary>
    /// Gets the human-readable file size string.
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Extracts archive files (zip, rar, etc.).
    /// </summary>
    public static bool ExtractArchive(string archivePath, string destinationPath)
    {
        try
        {
            if (!File.Exists(archivePath))
                return false;

            if (!Directory.Exists(destinationPath))
                Directory.CreateDirectory(destinationPath);

            // TODO: Implement extraction logic using SharpZipLib or similar
            
            return true;
        }
        catch
        {
            return false;
        }
    }
}
