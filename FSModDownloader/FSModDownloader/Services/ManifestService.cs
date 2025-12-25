using System.IO;
using System.Text.Json;
using FSModDownloader.Models;
using Serilog;

namespace FSModDownloader.Services;

/// <summary>
/// Service for loading, validating, and managing modlist manifests.
/// </summary>
public class ManifestService : IManifestService
{
    private readonly ILogger _logger = Log.ForContext<ManifestService>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Loads a manifest from a JSON file path.
    /// </summary>
    public async Task<ManifestLoadResult> LoadFromFileAsync(string filePath)
    {
        try
        {
            _logger.Information("Loading manifest from {FilePath}", filePath);

            if (!File.Exists(filePath))
            {
                _logger.Warning("Manifest file not found: {FilePath}", filePath);
                return ManifestLoadResult.Failed($"File not found: {filePath}");
            }

            var json = await File.ReadAllTextAsync(filePath);
            return LoadFromJson(json);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading manifest from {FilePath}", filePath);
            return ManifestLoadResult.Failed($"Error reading file: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads a manifest from a JSON string.
    /// </summary>
    public ManifestLoadResult LoadFromJson(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return ManifestLoadResult.Failed("JSON content is empty.");
            }

            var manifest = JsonSerializer.Deserialize<ModListManifest>(json, JsonOptions);
            
            if (manifest == null)
            {
                return ManifestLoadResult.Failed("Failed to deserialize manifest - result was null.");
            }

            var result = ManifestLoadResult.Ok(manifest);
            
            if (result.ValidationErrors.Count > 0)
            {
                _logger.Warning("Manifest loaded with {Count} validation errors", result.ValidationErrors.Count);
                foreach (var error in result.ValidationErrors)
                {
                    _logger.Warning("  - {Error}", error);
                }
            }
            else
            {
                _logger.Information("Successfully loaded manifest '{Name}' (revision {Revision}) with {ModCount} mods",
                    manifest.Name, manifest.Revision, manifest.Mods.Count);
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "JSON parsing error");
            return ManifestLoadResult.Failed($"Invalid JSON format: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error parsing manifest");
            return ManifestLoadResult.Failed($"Error parsing manifest: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves a manifest to a JSON file.
    /// </summary>
    public async Task SaveToFileAsync(ModListManifest manifest, string filePath)
    {
        try
        {
            _logger.Information("Saving manifest '{Name}' to {FilePath}", manifest.Name, filePath);

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = ToJson(manifest);
            await File.WriteAllTextAsync(filePath, json);

            _logger.Information("Successfully saved manifest to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving manifest to {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Serializes a manifest to a JSON string.
    /// </summary>
    public string ToJson(ModListManifest manifest)
    {
        return JsonSerializer.Serialize(manifest, JsonOptions);
    }

    /// <summary>
    /// Creates a new empty manifest with a generated ListId.
    /// </summary>
    public ModListManifest CreateNew(string name, string game)
    {
        return new ModListManifest
        {
            ListId = Guid.NewGuid(),
            Name = name,
            Game = game.ToUpperInvariant(),
            Revision = 1,
            UpdatedAtUtc = DateTime.UtcNow,
            Mods = new List<ManifestModEntry>()
        };
    }

    /// <summary>
    /// Compares two manifests to determine what changed.
    /// </summary>
    public ManifestComparisonResult Compare(ModListManifest oldManifest, ModListManifest newManifest)
    {
        var result = new ManifestComparisonResult
        {
            RevisionDelta = newManifest.Revision - oldManifest.Revision
        };

        var oldMods = oldManifest.Mods.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
        var newMods = newManifest.Mods.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

        // Find added and changed mods
        foreach (var newMod in newManifest.Mods)
        {
            if (oldMods.TryGetValue(newMod.Id, out var oldMod))
            {
                // Mod exists in both - check if changed
                if (!string.Equals(oldMod.Sha256, newMod.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    result.Changed.Add((oldMod, newMod));
                }
                else
                {
                    result.Unchanged.Add(newMod);
                }
            }
            else
            {
                // Mod only in new
                result.Added.Add(newMod);
            }
        }

        // Find removed mods
        foreach (var oldMod in oldManifest.Mods)
        {
            if (!newMods.ContainsKey(oldMod.Id))
            {
                result.Removed.Add(oldMod);
            }
        }

        _logger.Information("Manifest comparison: {Added} added, {Removed} removed, {Changed} changed, {Unchanged} unchanged",
            result.Added.Count, result.Removed.Count, result.Changed.Count, result.Unchanged.Count);

        return result;
    }
}
