using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using FSModDownloader.Models;
using Serilog;

namespace FSModDownloader.Services;

/// <summary>
/// Service for installing mods from a manifest.
/// Handles downloading, SHA-256 verification, and atomic installation.
/// </summary>
public class ManifestInstaller : IManifestInstaller, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<ManifestInstaller>();
    private readonly HttpClient _httpClient;
    private readonly string _tempDirectory;
    private readonly string _backupDirectory;
    private bool _disposed;

    public event EventHandler<ModInstallEventArgs>? ModInstallStarted;
    public event EventHandler<ModInstallEventArgs>? ModInstallCompleted;
    public event EventHandler<ModDownloadProgressEventArgs>? ModDownloadProgress;

    public ManifestInstaller()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "FSModDownloader/1.0");
        _httpClient.Timeout = TimeSpan.FromMinutes(30); // Long timeout for large mods

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FSModDownloader");

        _tempDirectory = Path.Combine(appDataPath, "temp");
        _backupDirectory = Path.Combine(appDataPath, "backups");

        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_backupDirectory);
    }

    /// <summary>
    /// Installs all mods from a manifest into the specified game instance.
    /// </summary>
    public async Task<ManifestInstallResult> InstallManifestAsync(
        ModListManifest manifest,
        GameInstance gameInstance,
        IProgress<ManifestInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ManifestInstallResult();

        try
        {
            _logger.Information("Starting manifest installation: {ManifestName} ({ModCount} mods) to {GameInstance}",
                manifest.Name, manifest.Mods.Count, gameInstance.Name);

            // Validate game instance
            if (string.IsNullOrEmpty(gameInstance.ModsPath) || !Directory.Exists(gameInstance.ModsPath))
            {
                result.Error = $"Invalid mods directory: {gameInstance.ModsPath}";
                result.Success = false;
                return result;
            }

            // Analyze what needs to be installed
            ReportProgress(progress, ManifestInstallPhase.Analyzing, null, 0, manifest.Mods.Count, 0, "Analyzing existing mods...");
            
            var analysis = await AnalyzeManifestAsync(manifest, gameInstance);

            _logger.Information("Analysis complete: {ToDownload} to download, {ToReplace} to replace, {UpToDate} up-to-date",
                analysis.ToDownload.Count, analysis.ToReplace.Count, analysis.UpToDate.Count);

            result.SkippedCount = analysis.UpToDate.Count;

            // Get all mods that need action
            var modsToProcess = new List<(ManifestModEntry Entry, string? ExistingPath)>();
            modsToProcess.AddRange(analysis.ToDownload.Select(m => (m, (string?)null)));
            modsToProcess.AddRange(analysis.ToReplace.Select(m => (m.Entry, (string?)m.ExistingPath)));

            if (modsToProcess.Count == 0)
            {
                _logger.Information("All mods are up-to-date, nothing to install");
                result.Success = true;
                ReportProgress(progress, ManifestInstallPhase.Complete, null, 0, 0, 100, "All mods are up-to-date!");
                return result;
            }

            // Process each mod
            int processedCount = 0;
            foreach (var (modEntry, existingPath) in modsToProcess)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Information("Installation cancelled by user");
                    result.WasCancelled = true;
                    break;
                }

                processedCount++;
                var isReplacement = existingPath != null;

                OnModInstallStarted(modEntry, processedCount, modsToProcess.Count);

                try
                {
                    var installResult = await InstallSingleModAsync(
                        modEntry,
                        gameInstance.ModsPath,
                        existingPath,
                        processedCount,
                        modsToProcess.Count,
                        progress,
                        cancellationToken);

                    if (installResult.Success)
                    {
                        if (isReplacement)
                            result.ReplacedCount++;
                        else
                            result.InstalledCount++;

                        OnModInstallCompleted(modEntry, processedCount, modsToProcess.Count, true, null);
                    }
                    else
                    {
                        result.FailedCount++;
                        result.Failures.Add(new ModInstallFailure
                        {
                            ModEntry = modEntry,
                            Error = installResult.Error ?? "Unknown error",
                            Reason = installResult.FailureReason
                        });

                        OnModInstallCompleted(modEntry, processedCount, modsToProcess.Count, false, installResult.Error);
                    }
                }
                catch (OperationCanceledException)
                {
                    result.WasCancelled = true;
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error installing mod {ModId}", modEntry.Id);
                    result.FailedCount++;
                    result.Failures.Add(new ModInstallFailure
                    {
                        ModEntry = modEntry,
                        Error = ex.Message,
                        Reason = ModInstallFailureReason.Unknown
                    });

                    OnModInstallCompleted(modEntry, processedCount, modsToProcess.Count, false, ex.Message);
                }
            }

            result.Success = result.FailedCount == 0 && !result.WasCancelled;

            var phase = result.WasCancelled ? ManifestInstallPhase.Failed :
                       result.Success ? ManifestInstallPhase.Complete : ManifestInstallPhase.Failed;
            var message = result.WasCancelled ? "Installation cancelled" :
                         result.Success ? $"Successfully installed {result.InstalledCount + result.ReplacedCount} mods" :
                         $"Completed with {result.FailedCount} failures";

            ReportProgress(progress, phase, null, processedCount, modsToProcess.Count, 100, message);

            _logger.Information("Manifest installation complete: {Installed} installed, {Replaced} replaced, {Skipped} skipped, {Failed} failed",
                result.InstalledCount, result.ReplacedCount, result.SkippedCount, result.FailedCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Fatal error during manifest installation");
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Installs a single mod: download, verify, and move to mods folder.
    /// </summary>
    private async Task<SingleModInstallResult> InstallSingleModAsync(
        ManifestModEntry modEntry,
        string modsFolder,
        string? existingFilePath,
        int modIndex,
        int totalMods,
        IProgress<ManifestInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var tempFilePath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}_{modEntry.EffectiveFileName}");

        try
        {
            // Download
            ReportProgress(progress, ManifestInstallPhase.Downloading, modEntry, modIndex, totalMods, 0,
                $"Downloading {modEntry.DisplayName}...");

            var downloadSuccess = await DownloadModAsync(modEntry, tempFilePath, modIndex, totalMods, progress, cancellationToken);

            if (!downloadSuccess)
            {
                return SingleModInstallResult.Fail("Download failed", ModInstallFailureReason.DownloadFailed);
            }

            // Verify size
            var fileInfo = new FileInfo(tempFilePath);
            if (fileInfo.Length != modEntry.SizeBytes)
            {
                _logger.Warning("Size mismatch for {ModId}: expected {Expected}, got {Actual}",
                    modEntry.Id, modEntry.SizeBytes, fileInfo.Length);
                return SingleModInstallResult.Fail(
                    $"Size mismatch: expected {modEntry.SizeBytes} bytes, got {fileInfo.Length} bytes",
                    ModInstallFailureReason.SizeMismatch);
            }

            // Verify hash
            ReportProgress(progress, ManifestInstallPhase.Verifying, modEntry, modIndex, totalMods, 90,
                $"Verifying {modEntry.DisplayName}...");

            var hashMatches = await VerifyFileHashAsync(tempFilePath, modEntry.Sha256);
            if (!hashMatches)
            {
                _logger.Warning("Hash mismatch for {ModId}", modEntry.Id);
                return SingleModInstallResult.Fail("Downloaded file hash does not match expected", ModInstallFailureReason.HashMismatch);
            }

            // Backup existing file if needed
            if (!string.IsNullOrEmpty(existingFilePath) && File.Exists(existingFilePath))
            {
                ReportProgress(progress, ManifestInstallPhase.BackingUp, modEntry, modIndex, totalMods, 95,
                    $"Backing up existing {modEntry.EffectiveFileName}...");

                await BackupFileAsync(existingFilePath);
            }

            // Install (atomic move)
            ReportProgress(progress, ManifestInstallPhase.Installing, modEntry, modIndex, totalMods, 98,
                $"Installing {modEntry.DisplayName}...");

            var targetPath = Path.Combine(modsFolder, modEntry.EffectiveFileName);
            await InstallFileAtomicAsync(tempFilePath, targetPath);

            _logger.Information("Successfully installed mod {ModId} to {TargetPath}", modEntry.Id, targetPath);
            return SingleModInstallResult.Ok();
        }
        finally
        {
            // Cleanup temp file
            try
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
            catch { /* Ignore cleanup errors */ }
        }
    }

    /// <summary>
    /// Downloads a mod file with progress reporting.
    /// </summary>
    private async Task<bool> DownloadModAsync(
        ManifestModEntry modEntry,
        string targetPath,
        int modIndex,
        int totalMods,
        IProgress<ManifestInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(modEntry.SourceUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? modEntry.SizeBytes;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long bytesRead = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                bytesRead += read;

                // Report download progress
                var downloadProgress = totalBytes > 0 ? (bytesRead / (double)totalBytes) * 80 : 0; // 0-80% for download
                ReportProgress(progress, ManifestInstallPhase.Downloading, modEntry, modIndex, totalMods, downloadProgress,
                    $"Downloading {modEntry.DisplayName}... {FormatBytes(bytesRead)} / {FormatBytes(totalBytes)}");

                OnModDownloadProgress(modEntry, bytesRead, totalBytes);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to download {ModId} from {Url}", modEntry.Id, modEntry.SourceUrl);
            return false;
        }
    }

    /// <summary>
    /// Backs up an existing file to the backup directory.
    /// </summary>
    private async Task BackupFileAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(_backupDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}{Path.GetExtension(fileName)}");

        await Task.Run(() => File.Copy(filePath, backupPath, overwrite: true));
        _logger.Information("Backed up {FileName} to {BackupPath}", fileName, backupPath);
    }

    /// <summary>
    /// Atomically moves a file from temp to target location.
    /// </summary>
    private async Task InstallFileAtomicAsync(string sourcePath, string targetPath)
    {
        // Delete target if exists (we've already backed it up)
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        // Try atomic move first
        try
        {
            File.Move(sourcePath, targetPath);
        }
        catch (IOException)
        {
            // If move fails (different drives), fall back to copy+delete
            await Task.Run(() => File.Copy(sourcePath, targetPath, overwrite: true));
            File.Delete(sourcePath);
        }
    }

    /// <summary>
    /// Analyzes a manifest to determine which mods need to be installed.
    /// </summary>
    public async Task<ManifestAnalysis> AnalyzeManifestAsync(ModListManifest manifest, GameInstance gameInstance)
    {
        var analysis = new ManifestAnalysis();

        foreach (var mod in manifest.Mods)
        {
            var expectedPath = Path.Combine(gameInstance.ModsPath, mod.EffectiveFileName);

            if (!File.Exists(expectedPath))
            {
                // File doesn't exist - needs download
                analysis.ToDownload.Add(mod);
            }
            else
            {
                // File exists - check hash
                try
                {
                    var hashMatches = await VerifyFileHashAsync(expectedPath, mod.Sha256);
                    if (hashMatches)
                    {
                        analysis.UpToDate.Add(mod);
                    }
                    else
                    {
                        analysis.ToReplace.Add((mod, expectedPath));
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Error verifying {ModId}", mod.Id);
                    analysis.VerificationErrors.Add((mod, ex.Message));
                }
            }
        }

        return analysis;
    }

    /// <summary>
    /// Verifies a file against its expected SHA-256 hash.
    /// </summary>
    public async Task<bool> VerifyFileHashAsync(string filePath, string expectedSha256)
    {
        var actualHash = await ComputeFileHashAsync(filePath);
        return string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Computes the SHA-256 hash of a file.
    /// </summary>
    public async Task<string> ComputeFileHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    #region Progress and Event Helpers

    private void ReportProgress(
        IProgress<ManifestInstallProgress>? progress,
        ManifestInstallPhase phase,
        ManifestModEntry? currentMod,
        int currentIndex,
        int totalMods,
        double currentProgress,
        string message)
    {
        progress?.Report(new ManifestInstallProgress
        {
            Phase = phase,
            CurrentMod = currentMod,
            CurrentModIndex = currentIndex,
            TotalMods = totalMods,
            CurrentOperationProgress = currentProgress,
            OverallProgress = totalMods > 0 ? ((currentIndex - 1 + currentProgress / 100.0) / totalMods) * 100 : 0,
            StatusMessage = message
        });
    }

    private void OnModInstallStarted(ManifestModEntry mod, int index, int total)
    {
        ModInstallStarted?.Invoke(this, new ModInstallEventArgs
        {
            ModEntry = mod,
            ModIndex = index,
            TotalMods = total
        });
    }

    private void OnModInstallCompleted(ManifestModEntry mod, int index, int total, bool success, string? error)
    {
        ModInstallCompleted?.Invoke(this, new ModInstallEventArgs
        {
            ModEntry = mod,
            ModIndex = index,
            TotalMods = total,
            Success = success,
            Error = error
        });
    }

    private void OnModDownloadProgress(ManifestModEntry mod, long bytesReceived, long totalBytes)
    {
        ModDownloadProgress?.Invoke(this, new ModDownloadProgressEventArgs
        {
            ModEntry = mod,
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes
        });
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Result of installing a single mod.
/// </summary>
internal class SingleModInstallResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public ModInstallFailureReason FailureReason { get; set; }

    public static SingleModInstallResult Ok() => new() { Success = true };
    public static SingleModInstallResult Fail(string error, ModInstallFailureReason reason) => new()
    {
        Success = false,
        Error = error,
        FailureReason = reason
    };
}
