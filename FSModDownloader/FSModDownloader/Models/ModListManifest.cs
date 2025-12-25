using System.IO;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FSModDownloader.Models;

/// <summary>
/// Represents a shareable modlist manifest that defines a set of mods for a game instance.
/// CKAN-style JSON manifest that can be shared via Discord, Drive, email, etc.
/// </summary>
public class ModListManifest
{
    /// <summary>Schema version for future compatibility.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Unique identifier for this modlist (GUID).</summary>
    [JsonPropertyName("listId")]
    public Guid ListId { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable name for this modlist.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description of the modlist.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Target game (e.g., "FS25", "FS22", "FS19").</summary>
    [JsonPropertyName("game")]
    public string Game { get; set; } = string.Empty;

    /// <summary>Revision number - incremented when the list is updated.</summary>
    [JsonPropertyName("revision")]
    public int Revision { get; set; } = 1;

    /// <summary>When this manifest was last updated (UTC).</summary>
    [JsonPropertyName("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Optional author or group name.</summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>List of mods in this manifest.</summary>
    [JsonPropertyName("mods")]
    public List<ManifestModEntry> Mods { get; set; } = new();

    /// <summary>
    /// Validates the manifest and returns a list of validation errors.
    /// Returns an empty list if the manifest is valid.
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (ListId == Guid.Empty)
            errors.Add("ListId is required and must be a valid GUID.");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required.");

        if (string.IsNullOrWhiteSpace(Game))
            errors.Add("Game is required (e.g., 'FS25', 'FS22').");
        else if (!IsValidGame(Game))
            errors.Add($"Invalid game '{Game}'. Must be one of: FS15, FS17, FS19, FS22, FS25.");

        if (Revision < 1)
            errors.Add("Revision must be a positive integer.");

        if (Mods.Count == 0)
            errors.Add("Manifest must contain at least one mod.");

        for (int i = 0; i < Mods.Count; i++)
        {
            var modErrors = Mods[i].Validate();
            foreach (var error in modErrors)
            {
                errors.Add($"Mod[{i}] ({Mods[i].Id ?? "unknown"}): {error}");
            }
        }

        return errors;
    }

    /// <summary>
    /// Checks if the manifest is valid (no validation errors).
    /// </summary>
    public bool IsValid => Validate().Count == 0;

    private static bool IsValidGame(string game)
    {
        return game.ToUpperInvariant() switch
        {
            "FS15" or "FS17" or "FS19" or "FS22" or "FS25" => true,
            _ => false
        };
    }
}

/// <summary>
/// Represents a single mod entry in a manifest.
/// Contains all information needed to download, verify, and install a mod.
/// </summary>
public class ManifestModEntry
{
    /// <summary>Unique identifier for this mod in the manifest.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Optional human-readable title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Optional version string.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// Expected filename for the mod ZIP.
    /// If not specified, will be derived from the sourceUrl or id.
    /// </summary>
    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    /// <summary>SHA-256 hash of the mod file for verification (64 hex characters).</summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Expected file size in bytes.</summary>
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    /// <summary>Direct HTTP(S) URL to download the mod.</summary>
    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>Optional notes about this mod.</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>
    /// Gets the effective filename for this mod.
    /// Uses FileName if specified, otherwise derives from SourceUrl or Id.
    /// </summary>
    [JsonIgnore]
    public string EffectiveFileName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FileName))
                return FileName;

            if (!string.IsNullOrWhiteSpace(SourceUrl))
            {
                try
                {
                    var uri = new Uri(SourceUrl);
                    var pathFileName = Path.GetFileName(uri.LocalPath);
                    if (!string.IsNullOrWhiteSpace(pathFileName) && pathFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        return pathFileName;
                }
                catch { }
            }

            // Fallback to id-based filename
            var safeId = Regex.Replace(Id, @"[^a-zA-Z0-9_-]", "_");
            return $"{safeId}.zip";
        }
    }

    /// <summary>
    /// Gets a display name for this mod (title if available, otherwise id).
    /// </summary>
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? Id : Title;

    /// <summary>
    /// Validates this mod entry and returns a list of validation errors.
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
            errors.Add("Id is required.");

        if (string.IsNullOrWhiteSpace(Sha256))
            errors.Add("SHA-256 hash is required.");
        else if (!IsValidSha256(Sha256))
            errors.Add($"Invalid SHA-256 hash format. Expected 64 hexadecimal characters, got '{Sha256}'.");

        if (SizeBytes <= 0)
            errors.Add("SizeBytes must be a positive number.");

        if (string.IsNullOrWhiteSpace(SourceUrl))
            errors.Add("SourceUrl is required.");
        else if (!IsValidUrl(SourceUrl))
            errors.Add($"Invalid SourceUrl '{SourceUrl}'. Must be a valid HTTP(S) URL.");

        return errors;
    }

    /// <summary>
    /// Checks if this mod entry is valid (no validation errors).
    /// </summary>
    [JsonIgnore]
    public bool IsValid => Validate().Count == 0;

    private static bool IsValidSha256(string hash)
    {
        // SHA-256 produces 256 bits = 64 hexadecimal characters
        return !string.IsNullOrEmpty(hash) && 
               hash.Length == 64 && 
               Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$");
    }

    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

/// <summary>
/// Result of comparing a manifest mod entry against an existing file.
/// </summary>
public enum ModComparisonResult
{
    /// <summary>File does not exist - needs to be downloaded.</summary>
    Missing,
    
    /// <summary>File exists and hash matches - no action needed.</summary>
    UpToDate,
    
    /// <summary>File exists but hash differs - needs to be replaced.</summary>
    HashMismatch,
    
    /// <summary>Unable to verify (e.g., file read error).</summary>
    VerificationError
}
