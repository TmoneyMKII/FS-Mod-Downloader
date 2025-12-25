using FSModDownloader.Models;

namespace FSModDownloader.Services;

/// <summary>
/// Interface for loading and validating modlist manifests.
/// </summary>
public interface IManifestService
{
    /// <summary>
    /// Loads a manifest from a JSON file path.
    /// </summary>
    /// <param name="filePath">Path to the manifest JSON file.</param>
    /// <returns>The loaded manifest and any validation errors.</returns>
    Task<ManifestLoadResult> LoadFromFileAsync(string filePath);

    /// <summary>
    /// Loads a manifest from a JSON string.
    /// </summary>
    /// <param name="json">JSON content of the manifest.</param>
    /// <returns>The loaded manifest and any validation errors.</returns>
    ManifestLoadResult LoadFromJson(string json);

    /// <summary>
    /// Saves a manifest to a JSON file.
    /// </summary>
    /// <param name="manifest">The manifest to save.</param>
    /// <param name="filePath">Path to save the JSON file.</param>
    Task SaveToFileAsync(ModListManifest manifest, string filePath);

    /// <summary>
    /// Serializes a manifest to a JSON string.
    /// </summary>
    /// <param name="manifest">The manifest to serialize.</param>
    /// <returns>JSON string representation of the manifest.</returns>
    string ToJson(ModListManifest manifest);

    /// <summary>
    /// Creates a new empty manifest with a generated ListId.
    /// </summary>
    /// <param name="name">Name for the manifest.</param>
    /// <param name="game">Target game (e.g., "FS25").</param>
    /// <returns>A new manifest instance.</returns>
    ModListManifest CreateNew(string name, string game);

    /// <summary>
    /// Compares two manifests to determine what changed.
    /// </summary>
    /// <param name="oldManifest">The older/current manifest.</param>
    /// <param name="newManifest">The newer manifest.</param>
    /// <returns>Comparison result with added, removed, and changed mods.</returns>
    ManifestComparisonResult Compare(ModListManifest oldManifest, ModListManifest newManifest);
}

/// <summary>
/// Result of loading a manifest.
/// </summary>
public class ManifestLoadResult
{
    /// <summary>The loaded manifest (null if loading failed).</summary>
    public ModListManifest? Manifest { get; set; }

    /// <summary>Whether loading was successful.</summary>
    public bool Success => Manifest != null && ValidationErrors.Count == 0;

    /// <summary>Error message if loading failed.</summary>
    public string? Error { get; set; }

    /// <summary>Validation errors found in the manifest.</summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>Creates a successful result.</summary>
    public static ManifestLoadResult Ok(ModListManifest manifest) => new()
    {
        Manifest = manifest,
        ValidationErrors = manifest.Validate()
    };

    /// <summary>Creates a failed result.</summary>
    public static ManifestLoadResult Failed(string error) => new()
    {
        Error = error
    };
}

/// <summary>
/// Result of comparing two manifests.
/// </summary>
public class ManifestComparisonResult
{
    /// <summary>Mods that exist in new but not in old.</summary>
    public List<ManifestModEntry> Added { get; set; } = new();

    /// <summary>Mods that exist in old but not in new.</summary>
    public List<ManifestModEntry> Removed { get; set; } = new();

    /// <summary>Mods that exist in both but have different hashes.</summary>
    public List<(ManifestModEntry Old, ManifestModEntry New)> Changed { get; set; } = new();

    /// <summary>Mods that are identical in both manifests.</summary>
    public List<ManifestModEntry> Unchanged { get; set; } = new();

    /// <summary>Whether there are any differences.</summary>
    public bool HasChanges => Added.Count > 0 || Removed.Count > 0 || Changed.Count > 0;

    /// <summary>The revision difference (new - old).</summary>
    public int RevisionDelta { get; set; }
}
