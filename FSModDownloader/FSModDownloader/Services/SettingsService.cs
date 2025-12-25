using System.IO;
using System.Text.Json;
using FSModDownloader.Models;
using Serilog;

namespace FSModDownloader.Services;

/// <summary>
/// Manages application settings persistence.
/// </summary>
public class SettingsService
{
    private static readonly ILogger _logger = Log.ForContext<SettingsService>();
    
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FSModDownloader", "settings.json");

    private static AppSettings? _cachedSettings;

    /// <summary>
    /// Loads settings from disk, or returns defaults if none exist.
    /// </summary>
    public static AppSettings Load()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                _logger.Information("Loaded settings with {Count} game instances", _cachedSettings.GameInstances.Count);
                return _cachedSettings;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load settings, using defaults");
        }

        _cachedSettings = new AppSettings();
        return _cachedSettings;
    }

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    public static void Save(AppSettings settings)
    {
        try
        {
            var settingsDir = Path.GetDirectoryName(SettingsFilePath)!;
            if (!Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
            _cachedSettings = settings;
            _logger.Information("Saved settings with {Count} game instances", settings.GameInstances.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save settings");
            throw;
        }
    }

    /// <summary>
    /// Clears the cached settings to force a reload.
    /// </summary>
    public static void ClearCache()
    {
        _cachedSettings = null;
    }

    /// <summary>
    /// Gets the configured game instances from settings.
    /// </summary>
    public static List<GameInstance> GetGameInstances()
    {
        return Load().GameInstances;
    }

    /// <summary>
    /// Updates game instances in settings.
    /// </summary>
    public static void UpdateGameInstances(List<GameInstance> instances)
    {
        var settings = Load();
        settings.GameInstances = instances;
        Save(settings);
    }
}
