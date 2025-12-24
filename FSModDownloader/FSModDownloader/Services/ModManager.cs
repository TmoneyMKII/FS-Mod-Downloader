namespace FSModDownloader.Services;

using FSModDownloader.Models;
using Serilog;

/// <summary>
/// Handles mod installation, removal, and management operations.
/// </summary>
public class ModManager : IModManager
{
    private readonly ILogger _logger = Log.ForContext<ModManager>();
    private readonly IModDownloader _downloader;

    public ModManager(IModDownloader downloader)
    {
        _downloader = downloader;
    }

    /// <summary>
    /// Installs a specific mod version to the destination path.
    /// </summary>
    public async Task<bool> InstallModAsync(Mod mod, ModVersion version, string destinationPath)
    {
        try
        {
            _logger.Information("Installing mod {ModId} version {Version} to {Path}", 
                mod.Id, version.Version, destinationPath);

            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }

            // Download the mod
            var downloadPath = await _downloader.DownloadModAsync(version.DownloadUrl, mod.Name);
            
            if (string.IsNullOrEmpty(downloadPath))
            {
                _logger.Error("Failed to download mod {ModId}", mod.Id);
                return false;
            }

            // Extract mod to destination
            // TODO: Implement extraction logic (handles .zip, .rar, etc.)
            
            _logger.Information("Successfully installed mod {ModId}", mod.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error installing mod {ModId}", mod.Id);
            return false;
        }
    }

    /// <summary>
    /// Uninstalls a mod from the mods directory.
    /// </summary>
    public async Task<bool> UninstallModAsync(Mod mod, string modsPath)
    {
        try
        {
            _logger.Information("Uninstalling mod {ModId} from {ModsPath}", mod.Id, modsPath);
            
            var modPath = Path.Combine(modsPath, mod.Name);
            if (Directory.Exists(modPath))
            {
                Directory.Delete(modPath, recursive: true);
            }

            _logger.Information("Successfully uninstalled mod {ModId}", mod.Id);
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error uninstalling mod {ModId}", mod.Id);
            return false;
        }
    }

    /// <summary>
    /// Updates a mod to a new version.
    /// </summary>
    public async Task<bool> UpdateModAsync(Mod mod, ModVersion newVersion, string modsPath)
    {
        try
        {
            _logger.Information("Updating mod {ModId} to version {Version}", mod.Id, newVersion.Version);
            
            // Uninstall old version
            await UninstallModAsync(mod, modsPath);
            
            // Install new version
            return await InstallModAsync(mod, newVersion, modsPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating mod {ModId}", mod.Id);
            return false;
        }
    }

    /// <summary>
    /// Gets list of installed mods.
    /// </summary>
    public async Task<List<Mod>> GetInstalledModsAsync(string modsPath)
    {
        try
        {
            _logger.Information("Retrieving installed mods from {ModsPath}", modsPath);
            
            var installedMods = new List<Mod>();
            
            if (!Directory.Exists(modsPath))
            {
                return installedMods;
            }

            // TODO: Scan mod directories and parse metadata
            
            return await Task.FromResult(installedMods);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving installed mods");
            return new List<Mod>();
        }
    }

    /// <summary>
    /// Enables a mod (opposite of disable).
    /// </summary>
    public async Task<bool> EnableModAsync(string modId, string modsPath)
    {
        try
        {
            _logger.Information("Enabling mod {ModId}", modId);
            
            // TODO: Implement enable logic (might involve renaming files or updating config)
            
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error enabling mod {ModId}", modId);
            return false;
        }
    }

    /// <summary>
    /// Disables a mod without uninstalling it.
    /// </summary>
    public async Task<bool> DisableModAsync(string modId, string modsPath)
    {
        try
        {
            _logger.Information("Disabling mod {ModId}", modId);
            
            // TODO: Implement disable logic
            
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error disabling mod {ModId}", modId);
            return false;
        }
    }
}
