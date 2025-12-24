namespace FSModDownloader.Services;

/// <summary>
/// Interface for downloading mod files.
/// </summary>
public interface IModDownloader
{
    Task<string?> DownloadModAsync(string downloadUrl, string modName);
    Task CancelDownloadAsync();
    event EventHandler<DownloadProgressEventArgs>? DownloadProgressChanged;
}

/// <summary>
/// Event arguments for download progress updates.
/// </summary>
public class DownloadProgressEventArgs : EventArgs
{
    public long BytesReceived { get; set; }
    public long TotalBytesToReceive { get; set; }
    public double ProgressPercentage => TotalBytesToReceive > 0 
        ? (BytesReceived / (double)TotalBytesToReceive) * 100 
        : 0;
}
