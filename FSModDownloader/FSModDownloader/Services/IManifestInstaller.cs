using FSModDownloader.Models;

namespace FSModDownloader.Services;

/// <summary>
/// Interface for installing mods from a manifest.
/// Handles downloading, verification, and installation into game instances.
/// </summary>
public interface IManifestInstaller
{
    /// <summary>
    /// Installs all mods from a manifest into the specified game instance.
    /// </summary>
    /// <param name="manifest">The manifest containing mods to install.</param>
    /// <param name="gameInstance">Target game instance.</param>
    /// <param name="progress">Progress reporter for tracking installation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the installation operation.</returns>
    Task<ManifestInstallResult> InstallManifestAsync(
        ModListManifest manifest,
        GameInstance gameInstance,
        IProgress<ManifestInstallProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks which mods from a manifest need to be installed/updated.
    /// </summary>
    /// <param name="manifest">The manifest to check.</param>
    /// <param name="gameInstance">Target game instance.</param>
    /// <returns>Analysis of what needs to be installed.</returns>
    Task<ManifestAnalysis> AnalyzeManifestAsync(
        ModListManifest manifest,
        GameInstance gameInstance);

    /// <summary>
    /// Verifies a single file against its expected hash.
    /// </summary>
    /// <param name="filePath">Path to the file to verify.</param>
    /// <param name="expectedSha256">Expected SHA-256 hash (64 hex characters).</param>
    /// <returns>True if the hash matches, false otherwise.</returns>
    Task<bool> VerifyFileHashAsync(string filePath, string expectedSha256);

    /// <summary>
    /// Computes the SHA-256 hash of a file.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>SHA-256 hash as lowercase hex string.</returns>
    Task<string> ComputeFileHashAsync(string filePath);

    /// <summary>
    /// Event raised when a mod installation starts.
    /// </summary>
    event EventHandler<ModInstallEventArgs>? ModInstallStarted;

    /// <summary>
    /// Event raised when a mod installation completes.
    /// </summary>
    event EventHandler<ModInstallEventArgs>? ModInstallCompleted;

    /// <summary>
    /// Event raised when a mod download progress updates.
    /// </summary>
    event EventHandler<ModDownloadProgressEventArgs>? ModDownloadProgress;
}

/// <summary>
/// Result of a manifest installation operation.
/// </summary>
public class ManifestInstallResult
{
    /// <summary>Whether the installation was successful overall.</summary>
    public bool Success { get; set; }

    /// <summary>Total number of mods that were successfully installed.</summary>
    public int InstalledCount { get; set; }

    /// <summary>Number of mods that were skipped (already up-to-date).</summary>
    public int SkippedCount { get; set; }

    /// <summary>Number of mods that failed to install.</summary>
    public int FailedCount { get; set; }

    /// <summary>Number of mods that were replaced (hash mismatch).</summary>
    public int ReplacedCount { get; set; }

    /// <summary>Details of any failures.</summary>
    public List<ModInstallFailure> Failures { get; set; } = new();

    /// <summary>Overall error message if the entire operation failed.</summary>
    public string? Error { get; set; }

    /// <summary>Whether the operation was cancelled.</summary>
    public bool WasCancelled { get; set; }
}

/// <summary>
/// Details of a failed mod installation.
/// </summary>
public class ModInstallFailure
{
    /// <summary>The mod entry that failed.</summary>
    public required ManifestModEntry ModEntry { get; set; }

    /// <summary>The error message.</summary>
    public required string Error { get; set; }

    /// <summary>The failure reason category.</summary>
    public ModInstallFailureReason Reason { get; set; }
}

/// <summary>
/// Reason categories for mod installation failures.
/// </summary>
public enum ModInstallFailureReason
{
    /// <summary>Failed to download the mod.</summary>
    DownloadFailed,

    /// <summary>Downloaded file hash does not match expected.</summary>
    HashMismatch,

    /// <summary>Failed to move/copy file to mods folder.</summary>
    InstallFailed,

    /// <summary>File size does not match expected.</summary>
    SizeMismatch,

    /// <summary>The source URL is invalid or inaccessible.</summary>
    InvalidUrl,

    /// <summary>Unknown error occurred.</summary>
    Unknown
}

/// <summary>
/// Analysis of a manifest against an existing game instance.
/// </summary>
public class ManifestAnalysis
{
    /// <summary>Mods that need to be downloaded (missing).</summary>
    public List<ManifestModEntry> ToDownload { get; set; } = new();

    /// <summary>Mods that exist but need to be replaced (hash mismatch).</summary>
    public List<(ManifestModEntry Entry, string ExistingPath)> ToReplace { get; set; } = new();

    /// <summary>Mods that are already installed and up-to-date.</summary>
    public List<ManifestModEntry> UpToDate { get; set; } = new();

    /// <summary>Mods that couldn't be verified.</summary>
    public List<(ManifestModEntry Entry, string Error)> VerificationErrors { get; set; } = new();

    /// <summary>Total bytes to download.</summary>
    public long TotalBytesToDownload => ToDownload.Sum(m => m.SizeBytes) + ToReplace.Sum(m => m.Entry.SizeBytes);

    /// <summary>Total number of mods that need action.</summary>
    public int TotalModsToProcess => ToDownload.Count + ToReplace.Count;

    /// <summary>Whether any mods need to be installed.</summary>
    public bool HasModsToInstall => TotalModsToProcess > 0;
}

/// <summary>
/// Progress information for manifest installation.
/// </summary>
public class ManifestInstallProgress
{
    /// <summary>Current phase of installation.</summary>
    public ManifestInstallPhase Phase { get; set; }

    /// <summary>Current mod being processed.</summary>
    public ManifestModEntry? CurrentMod { get; set; }

    /// <summary>Index of current mod (1-based).</summary>
    public int CurrentModIndex { get; set; }

    /// <summary>Total number of mods to process.</summary>
    public int TotalMods { get; set; }

    /// <summary>Overall progress percentage (0-100).</summary>
    public double OverallProgress { get; set; }

    /// <summary>Current operation progress percentage (0-100).</summary>
    public double CurrentOperationProgress { get; set; }

    /// <summary>Bytes downloaded for current mod.</summary>
    public long BytesDownloaded { get; set; }

    /// <summary>Total bytes for current mod.</summary>
    public long BytesTotal { get; set; }

    /// <summary>Human-readable status message.</summary>
    public string StatusMessage { get; set; } = string.Empty;
}

/// <summary>
/// Phases of manifest installation.
/// </summary>
public enum ManifestInstallPhase
{
    /// <summary>Analyzing existing mods.</summary>
    Analyzing,

    /// <summary>Downloading a mod.</summary>
    Downloading,

    /// <summary>Verifying downloaded file.</summary>
    Verifying,

    /// <summary>Installing mod to game folder.</summary>
    Installing,

    /// <summary>Backing up existing file.</summary>
    BackingUp,

    /// <summary>Installation complete.</summary>
    Complete,

    /// <summary>Installation failed.</summary>
    Failed
}

/// <summary>
/// Event args for mod installation events.
/// </summary>
public class ModInstallEventArgs : EventArgs
{
    /// <summary>The mod entry being installed.</summary>
    public required ManifestModEntry ModEntry { get; set; }

    /// <summary>Index of the mod (1-based).</summary>
    public int ModIndex { get; set; }

    /// <summary>Total number of mods.</summary>
    public int TotalMods { get; set; }

    /// <summary>Whether the operation succeeded (for completed events).</summary>
    public bool Success { get; set; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Event args for mod download progress.
/// </summary>
public class ModDownloadProgressEventArgs : EventArgs
{
    /// <summary>The mod being downloaded.</summary>
    public required ManifestModEntry ModEntry { get; set; }

    /// <summary>Bytes received so far.</summary>
    public long BytesReceived { get; set; }

    /// <summary>Total bytes expected.</summary>
    public long TotalBytes { get; set; }

    /// <summary>Download progress percentage.</summary>
    public double ProgressPercentage => TotalBytes > 0 ? (BytesReceived / (double)TotalBytes) * 100 : 0;
}
