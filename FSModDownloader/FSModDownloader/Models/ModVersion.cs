namespace FSModDownloader.Models;

/// <summary>
/// Represents a specific version of a mod.
/// </summary>
public class ModVersion
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public List<string> SupportedGameVersions { get; set; } = new();
    public string? ChangeLog { get; set; }
    public DateTime ReleaseDate { get; set; }
    public long FileSize { get; set; }
    public string? Hash { get; set; }
}
