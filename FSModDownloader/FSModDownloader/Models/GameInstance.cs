namespace FSModDownloader.Models;

/// <summary>
/// Represents a Farming Simulator game instance installation.
/// </summary>
public class GameInstance
{
    /// <summary>Unique identifier for this instance.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>Display name (e.g., "Farming Simulator 25").</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Game identifier (e.g., "FS25", "FS22", "FS19", "FS17", "FS15").</summary>
    public string GameId { get; set; } = string.Empty;
    
    /// <summary>Path to the game installation folder.</summary>
    public string GamePath { get; set; } = string.Empty;
    
    /// <summary>Path to the mods folder.</summary>
    public string ModsPath { get; set; } = string.Empty;
    
    /// <summary>How this instance was discovered (Documents, Steam, GIANTS, Manual, Detected).</summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>Game version if detected.</summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>When the mods folder was last modified.</summary>
    public DateTime LastModified { get; set; }
    
    /// <summary>Whether this instance is valid/accessible.</summary>
    public bool IsValid { get; set; }
    
    /// <summary>Whether this was manually added by the user.</summary>
    public bool IsManual { get; set; }
    
    /// <summary>Display string for UI.</summary>
    public string DisplayName => string.IsNullOrEmpty(Source) || Source == "Manual" 
        ? Name 
        : $"{Name} ({Source})";
}
