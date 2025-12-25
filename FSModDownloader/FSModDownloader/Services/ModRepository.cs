using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace FSModDownloader.Services;

using FSModDownloader.Models;
using Serilog;

/// <summary>
/// Scraping-based mod repository for mod-network.com.
/// </summary>
public class ModRepository : IModRepository
{
    private readonly ILogger _logger = Log.ForContext<ModRepository>();
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "https://mod-network.com";
    private readonly string _gameSlug;

    // Cache to reduce requests
    private readonly Dictionary<string, (Mod mod, DateTime cachedAt)> _modCache = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(10);

    public ModRepository(string gameTitle = "fs2025")
    {
        // Map game title to mod-network slug
        _gameSlug = gameTitle.ToLower() switch
        {
            "fs2025" or "fs25" => "farming-simulator-25-mods",
            "fs2022" or "fs22" => "farming-simulator-22-mods",
            "fs2019" or "fs19" => "farming-simulator-19",
            _ => "farming-simulator-25-mods"
        };

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Searches for mods on mod-network.com by scraping the website.
    /// </summary>
    public async Task<List<Mod>> SearchModsAsync(string query, string? category = null, int page = 1, int pageSize = 20)
    {
        try
        {
            _logger.Information("Searching mod-network for: {Query}, Page: {Page}", query, page);

            var mods = new List<Mod>();
            var url = $"{_baseUrl}/en/{_gameSlug}/{page}";

            _logger.Debug("Fetching URL: {Url}", url);

            var html = await FetchHtmlAsync(url);
            if (string.IsNullOrEmpty(html))
            {
                _logger.Warning("No HTML content returned from mod-network");
                return mods;
            }

            // Parse JSON-LD structured data from the page
            mods = ParseJsonLdMods(html);

            // If JSON-LD parsing didn't work, fall back to HTML parsing
            if (mods.Count == 0)
            {
                _logger.Information("JSON-LD parsing returned no results, trying HTML parsing");
                mods = ParseHtmlMods(html);
            }

            // Filter by query if provided
            if (!string.IsNullOrWhiteSpace(query))
            {
                mods = mods.Where(m => 
                    m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    m.Author.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            _logger.Information("Found {Count} mods", mods.Count);
            return mods.Take(pageSize).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error searching for mods");
            return new List<Mod>();
        }
    }

    /// <summary>
    /// Parses mods from JSON-LD structured data embedded in the page.
    /// </summary>
    private List<Mod> ParseJsonLdMods(string html)
    {
        var mods = new List<Mod>();

        try
        {
            // Find JSON-LD script tags
            var jsonLdPattern = @"<script[^>]*type=""application/ld\+json""[^>]*>(.*?)</script>";
            var matches = Regex.Matches(html, jsonLdPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                var jsonContent = match.Groups[1].Value.Trim();
                
                try
                {
                    using var doc = JsonDocument.Parse(jsonContent);
                    var root = doc.RootElement;

                    // Check if it's an ItemList (mod listing)
                    if (root.TryGetProperty("@type", out var typeElement) && 
                        typeElement.GetString() == "ItemList" &&
                        root.TryGetProperty("itemListElement", out var items))
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            try
                            {
                                var mod = ParseJsonLdItem(item);
                                if (mod != null)
                                {
                                    mods.Add(mod);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Warning(ex, "Failed to parse JSON-LD item");
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.Debug(ex, "Failed to parse JSON-LD block");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error parsing JSON-LD data");
        }

        return mods;
    }

    private Mod? ParseJsonLdItem(JsonElement item)
    {
        // Extract URL to get mod ID
        if (!item.TryGetProperty("url", out var urlElement))
            return null;

        var url = urlElement.GetString() ?? "";
        
        // Extract mod ID from URL like "/en/fs25-mods/detail/113603/ground-type-auto-mapper-v10"
        var idMatch = Regex.Match(url, @"/detail/(\d+)/");
        if (!idMatch.Success)
            return null;

        var modId = idMatch.Groups[1].Value;

        // Extract name
        var name = "Unknown Mod";
        if (item.TryGetProperty("name", out var nameElement))
        {
            name = nameElement.GetString() ?? name;
            // Remove "fs25-mods" prefix if present
            name = Regex.Replace(name, @"^fs\d+-mods\s+", "", RegexOptions.IgnoreCase).Trim();
        }

        // Extract image
        string? imageUrl = null;
        if (item.TryGetProperty("image", out var imageElement))
        {
            imageUrl = imageElement.GetString();
        }

        _logger.Debug("Parsed mod from JSON-LD: {ModId} - {Name}", modId, name);

        return new Mod
        {
            Id = modId,
            Name = name,
            Author = "Unknown", // JSON-LD doesn't include author, would need to fetch detail page
            ImageUrl = imageUrl,
            RepositoryUrl = _baseUrl + url,
            GameVersions = new List<string> { _gameSlug.Contains("25") ? "FS25" : _gameSlug.Contains("22") ? "FS22" : "FS19" }
        };
    }

    /// <summary>
    /// Falls back to HTML parsing if JSON-LD is not available.
    /// </summary>
    private List<Mod> ParseHtmlMods(string html)
    {
        var mods = new List<Mod>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Look for mod links in the page
            var modLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, '/detail/')]");
            
            if (modLinks == null)
            {
                _logger.Warning("No mod links found in HTML");
                return mods;
            }

            var processedIds = new HashSet<string>();

            foreach (var link in modLinks)
            {
                var href = link.GetAttributeValue("href", "");
                var idMatch = Regex.Match(href, @"/detail/(\d+)/([^/""]+)");
                
                if (!idMatch.Success) continue;

                var modId = idMatch.Groups[1].Value;
                
                // Skip duplicates
                if (processedIds.Contains(modId)) continue;
                processedIds.Add(modId);

                var slug = idMatch.Groups[2].Value;
                var name = slug.Replace("-", " ");
                // Capitalize first letter of each word
                name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);

                // Try to find image
                var img = link.SelectSingleNode(".//img") ?? link.ParentNode?.SelectSingleNode(".//img");
                var imageUrl = img?.GetAttributeValue("src", null);

                mods.Add(new Mod
                {
                    Id = modId,
                    Name = name,
                    Author = "Unknown",
                    ImageUrl = imageUrl,
                    RepositoryUrl = _baseUrl + href,
                    GameVersions = new List<string> { _gameSlug.Contains("25") ? "FS25" : "FS22" }
                });

                _logger.Debug("Parsed mod from HTML: {ModId} - {Name}", modId, name);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error parsing HTML mods");
        }

        return mods;
    }

    /// <summary>
    /// Retrieves detailed information about a specific mod.
    /// </summary>
    public async Task<Mod?> GetModDetailsAsync(string modId)
    {
        try
        {
            // Check cache first
            if (_modCache.TryGetValue(modId, out var cached) && 
                DateTime.UtcNow - cached.cachedAt < _cacheExpiry)
            {
                return cached.mod;
            }

            _logger.Information("Fetching details for mod: {ModId}", modId);

            // We need to find the mod URL first - search for it
            var searchResults = await SearchModsAsync(string.Empty, null, 1, 100);
            var mod = searchResults.FirstOrDefault(m => m.Id == modId);

            if (mod == null)
            {
                _logger.Warning("Mod {ModId} not found", modId);
                return null;
            }

            // Fetch the detail page for more info
            if (!string.IsNullOrEmpty(mod.RepositoryUrl))
            {
                var html = await FetchHtmlAsync(mod.RepositoryUrl);
                if (!string.IsNullOrEmpty(html))
                {
                    EnrichModFromDetailPage(mod, html);
                }
            }

            _modCache[modId] = (mod, DateTime.UtcNow);
            return mod;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error fetching mod details for {ModId}", modId);
            return null;
        }
    }

    private void EnrichModFromDetailPage(Mod mod, string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Try to find description
            var descNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'description')]|//div[contains(@class, 'mod-description')]|//p[contains(@class, 'description')]");
            if (descNode != null)
            {
                mod.Description = HtmlEntity.DeEntitize(descNode.InnerText?.Trim() ?? "");
            }

            // Try to find download link
            var downloadNode = doc.DocumentNode.SelectSingleNode("//a[contains(@href, 'download')]|//a[contains(@class, 'download')]");
            var downloadUrl = downloadNode?.GetAttributeValue("href", null);

            if (!string.IsNullOrEmpty(downloadUrl))
            {
                if (!downloadUrl.StartsWith("http"))
                {
                    downloadUrl = _baseUrl + downloadUrl;
                }

                mod.Versions.Add(new ModVersion
                {
                    Version = mod.Version ?? "1.0",
                    DownloadUrl = downloadUrl,
                    SupportedGameVersions = mod.GameVersions,
                    ReleaseDate = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error enriching mod details");
        }
    }

    /// <summary>
    /// Gets list of installed mods from the mods directory.
    /// </summary>
    public async Task<List<Mod>> GetInstalledModsAsync(string modsPath)
    {
        try
        {
            _logger.Information("Scanning for installed mods in: {ModsPath}", modsPath);

            if (!Directory.Exists(modsPath))
            {
                _logger.Warning("Mods directory does not exist: {ModsPath}", modsPath);
                return new List<Mod>();
            }

            var installedMods = new List<Mod>();

            // FS mods are .zip files in the mods folder
            var modFiles = Directory.GetFiles(modsPath, "*.zip", SearchOption.TopDirectoryOnly);
            
            foreach (var modFile in modFiles)
            {
                try
                {
                    var mod = await ParseInstalledModAsync(modFile);
                    if (mod != null)
                    {
                        mod.IsInstalled = true;
                        installedMods.Add(mod);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to parse mod file: {ModFile}", modFile);
                }
            }

            // Also check for unzipped mod folders
            var modFolders = Directory.GetDirectories(modsPath);
            foreach (var modFolder in modFolders)
            {
                try
                {
                    var modDescPath = Path.Combine(modFolder, "modDesc.xml");
                    if (File.Exists(modDescPath))
                    {
                        var mod = ParseModDescriptor(modDescPath);
                        if (mod != null)
                        {
                            mod.IsInstalled = true;
                            mod.Id = Path.GetFileName(modFolder);
                            installedMods.Add(mod);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to parse mod folder: {ModFolder}", modFolder);
                }
            }

            _logger.Information("Found {Count} installed mods", installedMods.Count);
            return installedMods;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting installed mods from {ModsPath}", modsPath);
            return new List<Mod>();
        }
    }

    /// <summary>
    /// Validates if a mod meets requirements.
    /// </summary>
    public async Task<bool> ValidateModAsync(Mod mod)
    {
        try
        {
            _logger.Information("Validating mod: {ModId}", mod.Id);

            if (string.IsNullOrEmpty(mod.Name))
            {
                return false;
            }

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error validating mod {ModId}", mod.Id);
            return false;
        }
    }

    /// <summary>
    /// Gets available mod categories.
    /// </summary>
    public List<(string Id, string Name)> GetCategories()
    {
        return new List<(string, string)>
        {
            ("tractors", "Tractors"),
            ("trucks", "Trucks"),
            ("trailers", "Trailers"),
            ("harvesters", "Harvesters"),
            ("tools", "Tools"),
            ("maps", "Maps"),
            ("buildings", "Buildings"),
            ("objects", "Objects"),
            ("vehicles", "Vehicles"),
            ("other", "Other")
        };
    }

    #region Private Helper Methods

    private async Task<string?> FetchHtmlAsync(string url)
    {
        try
        {
            _logger.Debug("Fetching URL: {Url}", url);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            _logger.Debug("Received {Length} bytes", content.Length);
            return content;
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "HTTP error fetching {Url}: {Message}", url, ex.Message);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.Error(ex, "Request timeout for {Url}", url);
            return null;
        }
    }

    private async Task<Mod?> ParseInstalledModAsync(string zipFilePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipFilePath);
            var modDescEntry = archive.GetEntry("modDesc.xml");
            
            if (modDescEntry == null)
            {
                modDescEntry = archive.Entries.FirstOrDefault(e => 
                    e.Name.Equals("modDesc.xml", StringComparison.OrdinalIgnoreCase));
            }

            if (modDescEntry == null)
            {
                _logger.Warning("No modDesc.xml found in {ZipFile}", zipFilePath);
                return null;
            }

            using var stream = modDescEntry.Open();
            using var reader = new StreamReader(stream);
            var xmlContent = await reader.ReadToEndAsync();
            
            return ParseModDescriptorXml(xmlContent, Path.GetFileNameWithoutExtension(zipFilePath));
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error parsing zip file: {ZipFile}", zipFilePath);
            return null;
        }
    }

    private Mod? ParseModDescriptor(string modDescPath)
    {
        try
        {
            var xmlContent = File.ReadAllText(modDescPath);
            return ParseModDescriptorXml(xmlContent, Path.GetFileName(Path.GetDirectoryName(modDescPath) ?? ""));
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error parsing modDesc.xml: {Path}", modDescPath);
            return null;
        }
    }

    private Mod? ParseModDescriptorXml(string xmlContent, string fallbackId)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var modDesc = doc.Root;
            
            if (modDesc == null) return null;

            var titleElement = modDesc.Descendants("title").FirstOrDefault();
            var title = titleElement?.Elements().FirstOrDefault()?.Value ?? 
                        titleElement?.Value ?? 
                        fallbackId;

            var descElement = modDesc.Descendants("description").FirstOrDefault();
            var description = descElement?.Elements().FirstOrDefault()?.Value ?? 
                              descElement?.Value ?? "";

            var author = modDesc.Descendants("author").FirstOrDefault()?.Value ?? "Unknown";
            var version = modDesc.Attribute("descVersion")?.Value ?? 
                          modDesc.Descendants("version").FirstOrDefault()?.Value ?? "1.0";

            var iconPath = modDesc.Descendants("iconFilename").FirstOrDefault()?.Value;

            return new Mod
            {
                Id = fallbackId,
                Name = title,
                Description = description,
                Author = author,
                Version = version,
                ImageUrl = iconPath,
                InstalledVersion = version,
                IsInstalled = true
            };
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error parsing modDesc XML");
            return null;
        }
    }

    #endregion
}
