using System.IO;
using FSModDownloader.Models;

namespace FSModDownloader;

/// <summary>
/// Configuration settings for the application.
/// </summary>
public class AppSettings
{
    public string RepositoryUrl { get; set; } = "https://mod-network.com";
    public string DownloadPath { get; set; } = string.Empty;
    public bool AutoCheckForUpdates { get; set; } = true;
    public bool NotifyOnModUpdates { get; set; } = true;
    public bool AutoInstallAfterDownload { get; set; } = true;
    public bool DeleteAfterInstall { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public bool MinimizeToTray { get; set; } = false;
    public bool ConfirmOnExit { get; set; } = true;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxConcurrentDownloads { get; set; } = 3;
    
    /// <summary>
    /// List of configured game instances (detected + manually added).
    /// </summary>
    public List<GameInstance> GameInstances { get; set; } = new();
    
    /// <summary>
    /// The currently selected game instance ID.
    /// </summary>
    public string? SelectedGameInstanceId { get; set; }
    
    /// <summary>
    /// Gets the default download path.
    /// </summary>
    public static string GetDefaultDownloadPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            "Downloads", 
            "FSMods");
    }
}
