using System.IO;

namespace FSModDownloader.Services;

using FSModDownloader.Models;
using Serilog;
using System.Runtime.InteropServices;
using Microsoft.Win32;

/// <summary>
/// Detects Farming Simulator game installations on the system.
/// </summary>
public class GamePathDetector
{
    private readonly ILogger _logger = Log.ForContext<GamePathDetector>();

    /// <summary>
    /// Attempts to find Farming Simulator installations.
    /// </summary>
    public List<GameInstance> DetectGameInstallations()
    {
        var gameInstances = new List<GameInstance>();

        try
        {
            // Check Steam registry
            var steamPath = GetSteamGamePath("Farming Simulator 22") 
                         ?? GetSteamGamePath("Farming Simulator 23")
                         ?? GetSteamGamePath("Farming Simulator 2025");

            if (!string.IsNullOrEmpty(steamPath))
            {
                gameInstances.Add(CreateGameInstance("Farming Simulator (Steam)", steamPath));
            }

            // Check common installation paths
            var commonPaths = new[]
            {
                @"C:\Program Files\Farming Simulator",
                @"C:\Program Files (x86)\Farming Simulator",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Farming Simulator"
            };

            foreach (var path in commonPaths)
            {
                if (Directory.Exists(path) && !gameInstances.Any(g => g.GamePath == path))
                {
                    gameInstances.Add(CreateGameInstance("Farming Simulator (Local)", path));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error detecting game installations");
        }

        return gameInstances;
    }

    private string? GetSteamGamePath(string gameName)
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return null;

            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                if (key == null)
                    return null;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    using (var subKey = key.OpenSubKey(subKeyName))
                    {
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
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading Steam game path for {GameName}", gameName);
        }

        return null;
    }

    private GameInstance CreateGameInstance(string name, string gamePath)
    {
        var modsPath = Path.Combine(gamePath, "mods");
        
        return new GameInstance
        {
            Name = name,
            GamePath = gamePath,
            ModsPath = modsPath,
            Version = DetectGameVersion(gamePath),
            LastModified = Directory.GetLastWriteTime(gamePath),
            IsValid = ValidateGamePath(gamePath)
        };
    }

    private bool ValidateGamePath(string path)
    {
        // Check for essential game files/directories
        var essentialPaths = new[] { "bin", "data" };
        return essentialPaths.Any(p => Directory.Exists(Path.Combine(path, p)));
    }

    private string DetectGameVersion(string gamePath)
    {
        // TODO: Implement version detection from game files
        // Could check game executable version or config files
        return "Unknown";
    }
}
