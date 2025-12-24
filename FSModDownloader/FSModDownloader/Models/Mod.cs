namespace FSModDownloader.Models;

/// <summary>
/// Represents a Farming Simulator mod with metadata and version information.
/// </summary>
public class Mod
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? RepositoryUrl { get; set; }
    public List<string> GameVersions { get; set; } = new();
    public List<ModVersion> Versions { get; set; } = new();
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public int DownloadCount { get; set; }
    public bool IsInstalled { get; set; }
    public string? InstalledVersion { get; set; }
}
