using System.IO;
using System.Net.Http;

namespace FSModDownloader.Services;

using Serilog;

/// <summary>
/// Handles downloading mod files from remote servers.
/// </summary>
public class ModDownloader : IModDownloader
{
    private readonly ILogger _logger = Log.ForContext<ModDownloader>();
    private readonly string _downloadDirectory;
    private CancellationTokenSource? _cancellationTokenSource;

    public event EventHandler<DownloadProgressEventArgs>? DownloadProgressChanged;

    public ModDownloader(string downloadDirectory = "")
    {
        _downloadDirectory = string.IsNullOrEmpty(downloadDirectory) 
            ? Path.Combine(Path.GetTempPath(), "FSModDownloads")
            : downloadDirectory;

        if (!Directory.Exists(_downloadDirectory))
        {
            Directory.CreateDirectory(_downloadDirectory);
        }
    }

    /// <summary>
    /// Downloads a mod file from the specified URL.
    /// </summary>
    public async Task<string?> DownloadModAsync(string downloadUrl, string modName)
    {
        try
        {
            _logger.Information("Starting download of mod {ModName} from {Url}", modName, downloadUrl);
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            var fileName = Path.GetFileName(downloadUrl);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"{modName}.zip";
            }

            var filePath = Path.Combine(_downloadDirectory, fileName);

            using (var httpClient = new HttpClient())
            {
                using (var response = await httpClient.GetAsync(downloadUrl, 
                    HttpCompletionOption.ResponseHeadersRead, 
                    _cancellationTokenSource.Token))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.Error("Download failed with status code: {StatusCode}", response.StatusCode);
                        return null;
                    }

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = totalBytes != -1;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        var totalRead = 0L;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                _logger.Information("Download cancelled for {ModName}", modName);
                                return null;
                            }

                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (canReportProgress)
                            {
                                OnDownloadProgressChanged(new DownloadProgressEventArgs
                                {
                                    BytesReceived = totalRead,
                                    TotalBytesToReceive = totalBytes
                                });
                            }
                        }
                    }
                }
            }

            _logger.Information("Successfully downloaded mod {ModName} to {FilePath}", modName, filePath);
            return filePath;
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Download cancelled for {ModName}", modName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error downloading mod {ModName}", modName);
            return null;
        }
    }

    /// <summary>
    /// Cancels the current download operation.
    /// </summary>
    public async Task CancelDownloadAsync()
    {
        _cancellationTokenSource?.Cancel();
        await Task.CompletedTask;
    }

    protected virtual void OnDownloadProgressChanged(DownloadProgressEventArgs e)
    {
        DownloadProgressChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Cleans up a specific temp file after successful installation.
    /// </summary>
    public void CleanupTempFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.Information("Cleaned up temp file: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to cleanup temp file: {FilePath}", filePath);
        }
    }

    /// <summary>
    /// Cleans up all temp files in the download directory.
    /// </summary>
    public void CleanupAllTempFiles()
    {
        try
        {
            if (!Directory.Exists(_downloadDirectory))
                return;

            var files = Directory.GetFiles(_downloadDirectory);
            var deletedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to delete temp file: {File}", file);
                }
            }

            _logger.Information("Cleaned up {Count} temp files from {Directory}", deletedCount, _downloadDirectory);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error cleaning up temp directory: {Directory}", _downloadDirectory);
        }
    }
}
