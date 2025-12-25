using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace FSModDownloader.Services;

using FSModDownloader.Models;
using Serilog;

/// <summary>
/// Scraping-based mod repository for the official GIANTS ModHub.
/// </summary>
public class ModRepository : IModRepository
{
    private readonly ILogger _logger = Log.ForContext<ModRepository>();
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "https://www.farming-simulator.com";
    private readonly string _gameTitle;

    // Cache to reduce requests
    private readonly Dictionary<string, (Mod mod, DateTime cachedAt)> _modCache = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(10);

    public ModRepository(string gameTitle = "fs2025")
    {
        _gameTitle = gameTitle;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "FSModDownloader/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Searches for mods on the GIANTS ModHub by scraping the website.
    /// </summary>
    public async Task<List<Mod>> SearchModsAsync(string query, string? category = null, int page = 1, int pageSize = 20)
    {
        try
        {
            _logger.Information("Searching ModHub for: {Query}, Category: {Category}, Page: {Page}", query, category, page);

            var mods = new List<Mod>();
            
            // Build the search URL
            // ModHub uses 0-based page indexing
            var pageIndex = Math.Max(0, page - 1);
            var url = $"{_baseUrl}/mods.php?title={_gameTitle}&filter=latest&page={pageIndex}";
            
            if (!string.IsNullOrWhiteSpace(category))
            {
                url = $"{_baseUrl}/mods.php?title={_gameTitle}&filter=category&category_id={GetCategoryId(category)}&page={pageIndex}";
            }

            var html = await FetchHtmlAsync(url);
            if (string.IsNullOrEmpty(html))
            {
                _logger.Warning("No HTML content returned from ModHub");
                return mods;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Parse mod cards from the page
            // ModHub uses a grid of mod items
            var modNodes = doc.DocumentNode.SelectNodes("//a[contains(@href, 'mod.php?mod_id=')]");
            
            if (modNodes == null)
            {
                _logger.Warning("No mod nodes found on page");
                return mods;
            }

            foreach (var node in modNodes.Take(pageSize))
            {
                try
                {
                    var mod = ParseModCard(node);
                    if (mod != null)
                    {
                        // Filter by query if provided
                        if (string.IsNullOrWhiteSpace(query) || 
                            mod.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            mod.Author.Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            mods.Add(mod);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to parse mod card");
                }
            }

            _logger.Information("Found {Count} mods matching criteria", mods.Count);
            return mods;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error searching for mods");
            throw;
        }
    }

    /// <summary>
    /// Gets the latest/featured mods from ModHub.
    /// </summary>
    public async Task<List<Mod>> GetLatestModsAsync(int page = 1, int pageSize = 20)
    {
        return await SearchModsAsync(string.Empty, null, page, pageSize);
    }

    /// <summary>
    /// Gets the most downloaded mods from ModHub.
    /// </summary>
    public async Task<List<Mod>> GetPopularModsAsync(int page = 1, int pageSize = 20)
    {
        try
        {
            _logger.Information("Fetching popular mods, Page: {Page}", page);

            var mods = new List<Mod>();
            var pageIndex = Math.Max(0, page - 1);
            var url = $"{_baseUrl}/mods.php?title={_gameTitle}&filter=mostDownloaded&page={pageIndex}";

            var html = await FetchHtmlAsync(url);
            if (string.IsNullOrEmpty(html)) return mods;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var modNodes = doc.DocumentNode.SelectNodes("//a[contains(@href, 'mod.php?mod_id=')]");
            if (modNodes == null) return mods;

            foreach (var node in modNodes.Take(pageSize))
            {
                try
                {
                    var mod = ParseModCard(node);
                    if (mod != null) mods.Add(mod);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to parse mod card");
                }
            }

            return mods;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error fetching popular mods");
            throw;
        }
    }

    /// <summary>
    /// Retrieves detailed information about a specific mod by scraping its detail page.
    /// </summary>
    public async Task<Mod?> GetModDetailsAsync(string modId)
    {
        try
        {
            // Check cache first
            if (_modCache.TryGetValue(modId, out var cached) && 
                DateTime.UtcNow - cached.cachedAt < _cacheExpiry)
            {
                _logger.Debug("Returning cached mod details for {ModId}", modId);
                return cached.mod;
            }

            _logger.Information("Fetching details for mod: {ModId}", modId);

            var url = $"{_baseUrl}/mod.php?mod_id={modId}&title={_gameTitle}";
            var html = await FetchHtmlAsync(url);
            
            if (string.IsNullOrEmpty(html))
            {
                _logger.Warning("No HTML content returned for mod {ModId}", modId);
                return null;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var mod = ParseModDetailPage(doc, modId);
            
            if (mod != null)
            {
                _modCache[modId] = (mod, DateTime.UtcNow);
            }

            return mod;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error fetching mod details for {ModId}", modId);
            throw;
        }
    }

    /// <summary>
    /// Gets list of installed mods from the mods directory by parsing mod descriptors.
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
            return await Task.FromResult(installedMods);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting installed mods from {ModsPath}", modsPath);
            throw;
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

            // Check if we can fetch the mod details
            var details = await GetModDetailsAsync(mod.Id);
            if (details == null)
            {
                _logger.Warning("Could not validate mod {ModId} - not found in repository", mod.Id);
                return false;
            }

            // Basic validation
            if (string.IsNullOrEmpty(details.Name))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error validating mod {ModId}", mod.Id);
            return false;
        }
    }

    /// <summary>
    /// Gets the download URL for a specific mod.
    /// </summary>
    public async Task<string?> GetModDownloadUrlAsync(string modId)
    {
        try
        {
            var mod = await GetModDetailsAsync(modId);
            if (mod?.Versions.Count > 0)
            {
                return mod.Versions[0].DownloadUrl;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting download URL for mod {ModId}", modId);
            return null;
        }
    }

    /// <summary>
    /// Gets available mod categories.
    /// </summary>
    public List<(string Id, string Name)> GetCategories()
    {
        // Known ModHub categories for FS25
        return new List<(string, string)>
        {
            ("1", "Tractors"),
            ("2", "Trucks"),
            ("3", "Trailers"),
            ("4", "Cutters"),
            ("5", "Forager"),
            ("6", "Harvesters"),
            ("7", "Plows"),
            ("8", "Cultivators"),
            ("9", "Disc Harrows"),
            ("10", "Seeders"),
            ("11", "Planters"),
            ("12", "Sprayers"),
            ("13", "Fertilizer Spreaders"),
            ("14", "Weeders"),
            ("15", "Mowers"),
            ("16", "Tedders"),
            ("17", "Windrowers"),
            ("18", "Loading Wagons"),
            ("19", "Balers"),
            ("20", "Bale Wrappers"),
            ("21", "Wheel Loaders"),
            ("22", "Telehandlers"),
            ("23", "Skid Steers"),
            ("24", "Forklifts"),
            ("25", "Weights"),
            ("26", "Front Loaders"),
            ("27", "Wood Harvesting"),
            ("28", "Placeables"),
            ("29", "Maps"),
            ("30", "Gameplay"),
            ("31", "Objects"),
            ("32", "Misc")
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
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "HTTP error fetching {Url}", url);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.Error(ex, "Request timeout for {Url}", url);
            return null;
        }
    }

    private Mod? ParseModCard(HtmlNode node)
    {
        // Extract mod ID from href
        var href = node.GetAttributeValue("href", "");
        var modIdMatch = Regex.Match(href, @"mod_id=(\d+)");
        if (!modIdMatch.Success) return null;

        var modId = modIdMatch.Groups[1].Value;

        // Try to extract mod name from various possible locations
        var nameNode = node.SelectSingleNode(".//h3|.//h4|.//div[contains(@class,'title')]|.//span[contains(@class,'name')]");
        var name = nameNode?.InnerText?.Trim() ?? "";
        
        // If name is empty, try the parent or sibling nodes
        if (string.IsNullOrEmpty(name))
        {
            var parent = node.ParentNode;
            nameNode = parent?.SelectSingleNode(".//h3|.//h4");
            name = nameNode?.InnerText?.Trim() ?? $"Mod {modId}";
        }

        // Extract author
        var authorNode = node.SelectSingleNode(".//span[contains(text(),'By:')]/following-sibling::text()|.//div[contains(@class,'author')]");
        var author = authorNode?.InnerText?.Trim().Replace("By:", "").Trim() ?? "Unknown";
        
        // Alternative author extraction
        if (author == "Unknown")
        {
            var byMatch = Regex.Match(node.ParentNode?.InnerText ?? "", @"By:\s*([^\n\r]+)");
            if (byMatch.Success)
            {
                author = byMatch.Groups[1].Value.Trim();
            }
        }

        // Extract image URL
        var imgNode = node.SelectSingleNode(".//img");
        var imageUrl = imgNode?.GetAttributeValue("src", null);
        if (imageUrl != null && !imageUrl.StartsWith("http"))
        {
            imageUrl = _baseUrl + imageUrl;
        }

        // Extract rating if available
        var ratingText = node.ParentNode?.InnerText ?? "";
        var ratingMatch = Regex.Match(ratingText, @"(\d+\.?\d*)\s*\((\d+)\)");
        var downloadCount = 0;
        if (ratingMatch.Success)
        {
            int.TryParse(ratingMatch.Groups[2].Value, out downloadCount);
        }

        return new Mod
        {
            Id = modId,
            Name = HtmlEntity.DeEntitize(name),
            Author = HtmlEntity.DeEntitize(author),
            ImageUrl = imageUrl,
            RepositoryUrl = $"{_baseUrl}/mod.php?mod_id={modId}&title={_gameTitle}",
            DownloadCount = downloadCount,
            GameVersions = new List<string> { _gameTitle.ToUpperInvariant() }
        };
    }

    private Mod? ParseModDetailPage(HtmlDocument doc, string modId)
    {
        var mod = new Mod { Id = modId };

        // Extract title - try multiple selectors
        var titleNode = doc.DocumentNode.SelectSingleNode("//h1|//h2[contains(@class,'mod-title')]|//div[contains(@class,'title')]//h1");
        mod.Name = HtmlEntity.DeEntitize(titleNode?.InnerText?.Trim() ?? $"Mod {modId}");

        // Extract description
        var descNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'description')]|//p[contains(@class,'description')]|//div[contains(@class,'mod-description')]");
        mod.Description = HtmlEntity.DeEntitize(descNode?.InnerText?.Trim() ?? "");

        // Extract author
        var authorNode = doc.DocumentNode.SelectSingleNode("//*[contains(text(),'By:')]|//span[contains(@class,'author')]");
        if (authorNode != null)
        {
            var authorMatch = Regex.Match(authorNode.InnerText, @"By:\s*(.+)");
            mod.Author = authorMatch.Success ? HtmlEntity.DeEntitize(authorMatch.Groups[1].Value.Trim()) : "Unknown";
        }

        // Extract image
        var imgNode = doc.DocumentNode.SelectSingleNode("//img[contains(@class,'mod-image')]|//div[contains(@class,'screenshot')]//img|//img[contains(@src,'screenshot')]");
        mod.ImageUrl = imgNode?.GetAttributeValue("src", null);
        if (mod.ImageUrl != null && !mod.ImageUrl.StartsWith("http"))
        {
            mod.ImageUrl = _baseUrl + mod.ImageUrl;
        }

        // Extract version info
        var versionNode = doc.DocumentNode.SelectSingleNode("//*[contains(text(),'Version')]");
        if (versionNode != null)
        {
            var versionMatch = Regex.Match(versionNode.InnerText, @"Version[:\s]*(\d+\.?\d*\.?\d*)");
            if (versionMatch.Success)
            {
                mod.Version = versionMatch.Groups[1].Value;
            }
        }

        // Look for download link
        var downloadNode = doc.DocumentNode.SelectSingleNode("//a[contains(@href,'download')]|//a[contains(@class,'download')]|//button[contains(@class,'download')]");
        var downloadUrl = downloadNode?.GetAttributeValue("href", null);
        
        if (!string.IsNullOrEmpty(downloadUrl))
        {
            if (!downloadUrl.StartsWith("http"))
            {
                downloadUrl = _baseUrl + downloadUrl;
            }

            mod.Versions.Add(new ModVersion
            {
                Version = mod.Version,
                DownloadUrl = downloadUrl,
                SupportedGameVersions = new List<string> { _gameTitle.ToUpperInvariant() },
                ReleaseDate = DateTime.UtcNow
            });
        }

        // Extract file size if available
        var sizeNode = doc.DocumentNode.SelectSingleNode("//*[contains(text(),'MB')]|//*[contains(text(),'Size')]");
        if (sizeNode != null && mod.Versions.Count > 0)
        {
            var sizeMatch = Regex.Match(sizeNode.InnerText, @"(\d+\.?\d*)\s*MB");
            if (sizeMatch.Success && double.TryParse(sizeMatch.Groups[1].Value, out var sizeMb))
            {
                mod.Versions[0].FileSize = (long)(sizeMb * 1024 * 1024);
            }
        }

        mod.RepositoryUrl = $"{_baseUrl}/mod.php?mod_id={modId}&title={_gameTitle}";
        mod.GameVersions = new List<string> { _gameTitle.ToUpperInvariant() };

        return mod;
    }

    private async Task<Mod?> ParseInstalledModAsync(string zipFilePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipFilePath);
            var modDescEntry = archive.GetEntry("modDesc.xml");
            
            if (modDescEntry == null)
            {
                // Try to find modDesc.xml in a subfolder
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

            // Get the first language title, or fall back to the ID
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

            // Get icon path
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

    private string GetCategoryId(string category)
    {
        var categories = GetCategories();
        var match = categories.FirstOrDefault(c => 
            c.Name.Equals(category, StringComparison.OrdinalIgnoreCase) ||
            c.Id.Equals(category, StringComparison.OrdinalIgnoreCase));
        
        return match.Id ?? "1";
    }

    #endregion
}
