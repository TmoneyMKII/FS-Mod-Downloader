namespace FSModDownloader;

/// <summary>
/// Configuration settings for the application.
/// </summary>
public class AppSettings
{
    public string RepositoryUrl { get; set; } = "https://api.modhub.com";
    public string GameInstallationPath { get; set; } = string.Empty;
    public bool AutoCheckForUpdates { get; set; } = true;
    public bool NotifyOnModUpdates { get; set; } = true;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxConcurrentDownloads { get; set; } = 3;
    public string? ProxyUrl { get; set; }
}
