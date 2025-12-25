namespace FSModDownloader.Services;

using FSModDownloader.Models;

/// <summary>
/// Interface for mod repository operations.
/// </summary>
public interface IModRepository
{
    /// <summary>
    /// Search for mods across all configured sources.
    /// </summary>
    /// <param name="query">Search query (filters by name/author)</param>
    /// <param name="category">Optional category filter</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Maximum mods to return</param>
    Task<List<Mod>> SearchModsAsync(string query, string? category = null, int page = 1, int pageSize = 50);
    
    /// <summary>
    /// Get detailed information about a specific mod.
    /// </summary>
    Task<Mod?> GetModDetailsAsync(string modId);
    
    /// <summary>
    /// Scan local mods folder for installed mods.
    /// </summary>
    Task<List<Mod>> GetInstalledModsAsync(string modsPath);
    
    /// <summary>
    /// Validate that a mod meets requirements.
    /// </summary>
    Task<bool> ValidateModAsync(Mod mod);
}
