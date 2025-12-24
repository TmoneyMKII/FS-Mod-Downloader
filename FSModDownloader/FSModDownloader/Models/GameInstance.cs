namespace FSModDownloader.Models;

/// <summary>
/// Represents a Farming Simulator game instance installation.
/// </summary>
public class GameInstance
{
    public string Name { get; set; } = string.Empty;
    public string GamePath { get; set; } = string.Empty;
    public string ModsPath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public bool IsValid { get; set; }
}
