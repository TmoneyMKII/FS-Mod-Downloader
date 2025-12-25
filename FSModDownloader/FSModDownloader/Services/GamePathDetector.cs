using System.IO;

namespace FSModDownloader.Services;

using FSModDownloader.Models;
using Serilog;
using Microsoft.Win32;

/// <summary>
/// Detects Farming Simulator game installations on the system.
/// </summary>
public class GamePathDetector
{
    private readonly ILogger _logger = Log.ForContext<GamePathDetector>();

    // Supported game versions with their identifiers
    private static readonly Dictionary<string, GameInfo> SupportedGames = new()
    {
        { "FS25", new GameInfo("Farming Simulator 25", "FarmingSimulator2025", 2591070, "Farming Simulator 25") },
        { "FS22", new GameInfo("Farming Simulator 22", "FarmingSimulator2022", 1248130, "Farming Simulator 22") },
        { "FS19", new GameInfo("Farming Simulator 19", "FarmingSimulator2019", 787860, "Farming Simulator 19") },
        { "FS17", new GameInfo("Farming Simulator 17", "FarmingSimulator2017", 447020, "Farming Simulator 17") },
        { "FS15", new GameInfo("Farming Simulator 15", "FarmingSimulator2015", 313160, "Farming Simulator 15") },
    };

    private record GameInfo(string DisplayName, string FolderName, int SteamAppId, string RegistryName);

    /// <summary>
    /// Scans the system for all Farming Simulator installations (FS15-FS25).
    /// </summary>
    public List<GameInstance> ScanForGameInstallations()
    {
        var gameInstances = new List<GameInstance>();
        _logger.Information("Starting scan for Farming Simulator installations...");

        foreach (var (gameId, gameInfo) in SupportedGames)
        {
            try
            {
                // 1. Check Documents\My Games folder for mods directory
                var documentsModsPath = GetDocumentsModsPath(gameInfo.FolderName);
                if (!string.IsNullOrEmpty(documentsModsPath))
                {
                    var instance = new GameInstance
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = gameInfo.DisplayName,
                        GameId = gameId,
                        ModsPath = documentsModsPath,
                        GamePath = Path.GetDirectoryName(documentsModsPath) ?? string.Empty,
                        Source = "Documents",
                        IsValid = true,
                        LastModified = Directory.Exists(documentsModsPath) 
                            ? Directory.GetLastWriteTime(documentsModsPath) 
                            : DateTime.MinValue
                    };
                    
                    if (!gameInstances.Any(g => g.ModsPath.Equals(instance.ModsPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        gameInstances.Add(instance);
                        _logger.Information("Found {GameName} at {Path}", gameInfo.DisplayName, documentsModsPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error checking Documents path for {GameId}", gameId);
            }

            try
            {
                // 2. Check Steam installation
                var steamPath = GetSteamGamePath(gameInfo.SteamAppId, gameInfo.RegistryName);
                if (!string.IsNullOrEmpty(steamPath))
                {
                    var modsPath = Path.Combine(steamPath, "mods");
                    if (Directory.Exists(modsPath) && !gameInstances.Any(g => g.ModsPath.Equals(modsPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        gameInstances.Add(new GameInstance
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = $"{gameInfo.DisplayName} (Steam)",
                            GameId = gameId,
                            ModsPath = modsPath,
                            GamePath = steamPath,
                            Source = "Steam",
                            IsValid = true,
                            LastModified = Directory.GetLastWriteTime(modsPath)
                        });
                        _logger.Information("Found {GameName} Steam installation at {Path}", gameInfo.DisplayName, steamPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error checking Steam path for {GameId}", gameId);
            }

            try
            {
                // 3. Check registry for GIANTS installation
                var giantsPath = GetGiantsInstallerPath(gameInfo.RegistryName);
                if (!string.IsNullOrEmpty(giantsPath))
                {
                    var modsPath = Path.Combine(giantsPath, "mods");
                    if (Directory.Exists(modsPath) && !gameInstances.Any(g => g.ModsPath.Equals(modsPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        gameInstances.Add(new GameInstance
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = $"{gameInfo.DisplayName} (GIANTS)",
                            GameId = gameId,
                            ModsPath = modsPath,
                            GamePath = giantsPath,
                            Source = "GIANTS",
                            IsValid = true,
                            LastModified = Directory.GetLastWriteTime(modsPath)
                        });
                        _logger.Information("Found {GameName} GIANTS installation at {Path}", gameInfo.DisplayName, giantsPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error checking GIANTS path for {GameId}", gameId);
            }
        }

        // Check common installation paths
        try
        {
            CheckCommonPaths(gameInstances);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error checking common paths");
        }

        _logger.Information("Scan complete. Found {Count} game installation(s)", gameInstances.Count);
        return gameInstances;
    }

    private string? GetDocumentsModsPath(string folderName)
    {
        try
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var modsPath = Path.Combine(documentsPath, "My Games", folderName, "mods");
            
            // Check if the parent game folder exists (even if mods folder doesn't)
            var gameFolderPath = Path.Combine(documentsPath, "My Games", folderName);
            if (Directory.Exists(gameFolderPath))
            {
                return modsPath; // Return mods path even if it doesn't exist yet
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error checking Documents path for {FolderName}", folderName);
        }
        return null;
    }

    private string? GetSteamGamePath(int appId, string gameName)
    {
        try
        {
            // Try to find Steam installation path
            var steamPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath)) return null;

            // Check main steamapps folder
            var manifestPath = Path.Combine(steamPath, "steamapps", $"appmanifest_{appId}.acf");
            if (File.Exists(manifestPath))
            {
                var gamePath = Path.Combine(steamPath, "steamapps", "common", gameName);
                if (Directory.Exists(gamePath)) return gamePath;
            }

            // Check library folders
            var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(libraryFoldersPath))
            {
                var content = File.ReadAllText(libraryFoldersPath);
                var paths = ParseLibraryFolders(content);
                
                foreach (var libPath in paths)
                {
                    manifestPath = Path.Combine(libPath, "steamapps", $"appmanifest_{appId}.acf");
                    if (File.Exists(manifestPath))
                    {
                        var gamePath = Path.Combine(libPath, "steamapps", "common", gameName);
                        if (Directory.Exists(gamePath)) return gamePath;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error checking Steam path for {GameName}", gameName);
        }
        return null;
    }

    private string? GetSteamInstallPath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                         ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
            return key?.GetValue("InstallPath") as string;
        }
        catch
        {
            return null;
        }
    }

    private List<string> ParseLibraryFolders(string content)
    {
        var paths = new List<string>();
        // Simple parsing for "path" entries in libraryfolders.vdf
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("\"path\""))
            {
                var parts = line.Split('"');
                if (parts.Length >= 4)
                {
                    var path = parts[3].Replace(@"\\", @"\");
                    if (Directory.Exists(path))
                    {
                        paths.Add(path);
                    }
                }
            }
        }
        return paths;
    }

    private string? GetGiantsInstallerPath(string gameName)
    {
        try
        {
            // Check both 32-bit and 64-bit registry
            var registryPaths = new[]
            {
                $@"SOFTWARE\GIANTS Software\{gameName}",
                $@"SOFTWARE\WOW6432Node\GIANTS Software\{gameName}",
            };

            foreach (var regPath in registryPaths)
            {
                using var key = Registry.LocalMachine.OpenSubKey(regPath);
                var installDir = key?.GetValue("InstallDir") as string;
                if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                {
                    return installDir;
                }
            }

            // Also check Uninstall registry
            using var uninstallKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstallKey != null)
            {
                foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                {
                    using var subKey = uninstallKey.OpenSubKey(subKeyName);
                    var displayName = subKey?.GetValue("DisplayName") as string;
                    if (displayName != null && displayName.Contains(gameName, StringComparison.OrdinalIgnoreCase))
                    {
                        var installLocation = subKey?.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                        {
                            return installLocation;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error checking GIANTS registry for {GameName}", gameName);
        }
        return null;
    }

    private void CheckCommonPaths(List<GameInstance> gameInstances)
    {
        var commonBasePaths = new[]
        {
            @"C:\Program Files",
            @"C:\Program Files (x86)",
            @"D:\Games",
            @"D:\SteamLibrary\steamapps\common",
            @"E:\Games",
            @"E:\SteamLibrary\steamapps\common",
        };

        foreach (var (gameId, gameInfo) in SupportedGames)
        {
            foreach (var basePath in commonBasePaths)
            {
                var gamePath = Path.Combine(basePath, gameInfo.RegistryName);
                var modsPath = Path.Combine(gamePath, "mods");
                
                if (Directory.Exists(modsPath) && !gameInstances.Any(g => g.ModsPath.Equals(modsPath, StringComparison.OrdinalIgnoreCase)))
                {
                    gameInstances.Add(new GameInstance
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = gameInfo.DisplayName,
                        GameId = gameId,
                        ModsPath = modsPath,
                        GamePath = gamePath,
                        Source = "Detected",
                        IsValid = true,
                        LastModified = Directory.GetLastWriteTime(modsPath)
                    });
                    _logger.Information("Found {GameName} at common path {Path}", gameInfo.DisplayName, gamePath);
                }
            }
        }
    }

    /// <summary>
    /// Validates that a game instance path is still valid.
    /// </summary>
    public bool ValidateGameInstance(GameInstance instance)
    {
        if (string.IsNullOrEmpty(instance.ModsPath))
            return false;

        // For Documents-based paths, just check if parent game folder exists
        var parentDir = Path.GetDirectoryName(instance.ModsPath);
        return Directory.Exists(parentDir);
    }

    /// <summary>
    /// Creates the mods folder for a game instance if it doesn't exist.
    /// </summary>
    public bool EnsureModsFolderExists(GameInstance instance)
    {
        try
        {
            if (!Directory.Exists(instance.ModsPath))
            {
                Directory.CreateDirectory(instance.ModsPath);
                _logger.Information("Created mods folder at {Path}", instance.ModsPath);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create mods folder at {Path}", instance.ModsPath);
            return false;
        }
    }

    /// <summary>
    /// Gets available game IDs for creating manual entries.
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetAvailableGameTypes()
    {
        return SupportedGames.ToDictionary(g => g.Key, g => g.Value.DisplayName);
    }
}
