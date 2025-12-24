namespace FSModDownloader.Services;

using FSModDownloader.Models;

/// <summary>
/// Interface for mod repository operations.
/// </summary>
public interface IModRepository
{
    Task<List<Mod>> SearchModsAsync(string query, string? category = null, int page = 1, int pageSize = 20);
    Task<Mod?> GetModDetailsAsync(string modId);
    Task<List<Mod>> GetInstalledModsAsync(string modsPath);
    Task<bool> ValidateModAsync(Mod mod);
}
