namespace FSModDownloader.Services;

using FSModDownloader.Models;

/// <summary>
/// Interface for mod management operations.
/// </summary>
public interface IModManager
{
    Task<bool> InstallModAsync(Mod mod, ModVersion version, string destinationPath);
    Task<bool> UninstallModAsync(Mod mod, string modsPath);
    Task<bool> UpdateModAsync(Mod mod, ModVersion newVersion, string modsPath);
    Task<List<Mod>> GetInstalledModsAsync(string modsPath);
    Task<bool> EnableModAsync(string modId, string modsPath);
    Task<bool> DisableModAsync(string modId, string modsPath);
}
