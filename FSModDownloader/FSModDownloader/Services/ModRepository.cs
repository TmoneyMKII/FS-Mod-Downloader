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
/// Multi-source mod repository that aggregates mods from several websites.
/// Sources: mod-network.com, fs19.net, fs22.com, farmingsimulator25mods.com
/// </summary>
public class ModRepository : IModRepository
{
    private readonly ILogger _logger = Log.ForContext<ModRepository>();
    private readonly HttpClient _httpClient;
    private readonly string _gameVersion;

    // Cache to reduce requests
    private readonly Dictionary<string, (Mod mod, DateTime cachedAt)> _modCache = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(10);

    // Source configurations per game version
    private readonly List<ModSource> _sources;

    public ModRepository(string gameTitle = "fs2025")
    {
        _gameVersion = gameTitle.ToLower() switch
        {
            "fs2025" or "fs25" => "FS25",
            "fs2022" or "fs22" => "FS22",
            "fs2019" or "fs19" => "FS19",
            "fs2017" or "fs17" => "FS17",
            "fs2015" or "fs15" => "FS15",
            _ => "FS25"
        };

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        // Configure sources based on game version
        _sources = GetSourcesForGame(_gameVersion);
    }

    private List<ModSource> GetSourcesForGame(string gameVersion)
    {
        return gameVersion switch
        {
            "FS25" => new List<ModSource>
            {
                new ModSource("mod-network", "https://mod-network.com", "/en/farming-simulator-25-mods/{page}", ModSourceType.ModNetwork),
                new ModSource("fs25mods", "https://farmingsimulator25mods.com", "/page/{page}/", ModSourceType.FarmingSimulatorMods),
            },
            "FS22" => new List<ModSource>
            {
                new ModSource("mod-network", "https://mod-network.com", "/en/farming-simulator-22-mods/{page}", ModSourceType.ModNetwork),
                new ModSource("fs22", "https://fs22.com", "/page/{page}/", ModSourceType.FsXXNet),
            },
            "FS19" => new List<ModSource>
            {
                new ModSource("mod-network", "https://mod-network.com", "/en/farming-simulator-19/{page}", ModSourceType.ModNetwork),
                new ModSource("fs19", "https://fs19.net", "/page/{page}/", ModSourceType.FsXXNet),
            },
            _ => new List<ModSource>
            {
                new ModSource("mod-network", "https://mod-network.com", "/en/farming-simulator-25-mods/{page}", ModSourceType.ModNetwork),
            }
        };
    }

    /// <summary>
    /// Searches for mods across all configured sources.
    /// Fetches multiple pages from each source to maximize results.
    /// </summary>
    public async Task<List<Mod>> SearchModsAsync(string query, string? category = null, int page = 1, int pageSize = 50)
    {
        try
        {
            _logger.Information("Searching {Count} sources for: {Query}, Page: {Page}", _sources.Count, query, page);

            var allMods = new List<Mod>();
            var tasks = new List<Task<List<Mod>>>();

            // Fetch multiple pages from all sources in parallel to get more mods
            // Each source provides ~8-20 mods per page, so we fetch 5 pages from each
            var pagesToFetch = 5;
            
            foreach (var source in _sources)
            {
                for (int p = page; p < page + pagesToFetch; p++)
                {
                    tasks.Add(FetchModsFromSourceAsync(source, p));
                }
            }

            var results = await Task.WhenAll(tasks);
            
            foreach (var mods in results)
            {
                allMods.AddRange(mods);
            }

            // Deduplicate by name (case-insensitive, normalized)
            var uniqueMods = allMods
                .GroupBy(m => NormalizeModName(m.Name))
                .Select(g => g.First())
                .ToList();

            _logger.Information("Found {Total} mods total, {Unique} unique after deduplication", allMods.Count, uniqueMods.Count);

            // Filter by query if provided
            if (!string.IsNullOrWhiteSpace(query))
            {
                uniqueMods = uniqueMods.Where(m => 
                    m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    m.Author.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return uniqueMods.Take(pageSize).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error searching for mods");
            return new List<Mod>();
        }
    }

    private string NormalizeModName(string name)
    {
        // Remove version numbers and normalize for deduplication
        var normalized = Regex.Replace(name, @"\s*[vV]?\d+\.\d+(\.\d+)?(\.\d+)?\s*$", "");
        return normalized.ToLowerInvariant().Trim();
    }

    private async Task<List<Mod>> FetchModsFromSourceAsync(ModSource source, int page)
    {
        try
        {
            var url = source.BaseUrl + source.PagePattern.Replace("{page}", page.ToString());
            _logger.Debug("Fetching from {Source}: {Url}", source.Name, url);

            var html = await FetchHtmlAsync(url);
            if (string.IsNullOrEmpty(html))
            {
                _logger.Warning("No content from {Source}", source.Name);
                return new List<Mod>();
            }

            var mods = source.Type switch
            {
                ModSourceType.ModNetwork => ParseModNetworkPage(html, source),
                ModSourceType.FsXXNet => ParseFsXXNetPage(html, source),
                ModSourceType.FarmingSimulatorMods => ParseFarmingSimulatorModsPage(html, source),
                _ => new List<Mod>()
            };

            _logger.Information("Found {Count} mods from {Source}", mods.Count, source.Name);
            return mods;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error fetching from {Source}", source.Name);
            return new List<Mod>();
        }
    }

    #region Source-Specific Parsers

    /// <summary>
    /// Parses mod-network.com pages (uses JSON-LD and HTML).
    /// </summary>
    private List<Mod> ParseModNetworkPage(string html, ModSource source)
    {
        var mods = ParseJsonLdMods(html, source);
        
        if (mods.Count == 0)
        {
            _logger.Debug("JSON-LD parsing returned no results for {Source}, trying HTML", source.Name);
            mods = ParseModNetworkHtml(html, source);
        }

        return mods;
    }

    /// <summary>
    /// Parses fs19.net and fs22.com pages (WordPress-style sites).
    /// </summary>
    private List<Mod> ParseFsXXNetPage(string html, ModSource source)
    {
        var mods = new List<Mod>();
        
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // These sites use article/post structures with links to mod pages
            // Pattern: /farming-simulator-XX-mods/{category}/{mod-slug}/
            var modLinks = doc.DocumentNode.SelectNodes("//article//h2//a|//div[contains(@class,'post')]//h2//a|//a[contains(@href, '-mods/') and contains(@href, '/v')]");

            if (modLinks == null)
            {
                // Fallback: look for any links containing the mod pattern
                modLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, 'farming-simulator')]");
            }

            if (modLinks == null)
            {
                _logger.Warning("No mod links found for {Source}", source.Name);
                return mods;
            }

            var processedUrls = new HashSet<string>();

            foreach (var link in modLinks)
            {
                var href = link.GetAttributeValue("href", "");
                
                // Skip non-mod links
                if (string.IsNullOrEmpty(href) || 
                    href.Contains("/category/") || 
                    href.Contains("/tag/") ||
                    href.Contains("/page/") ||
                    href.Contains("#"))
                    continue;

                // Normalize URL
                if (!href.StartsWith("http"))
                {
                    href = source.BaseUrl + href;
                }

                if (processedUrls.Contains(href))
                    continue;
                processedUrls.Add(href);

                // Extract mod name from link text or URL
                var name = link.InnerText?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    // Extract from URL slug
                    var match = Regex.Match(href, @"/([^/]+)/?$");
                    if (match.Success)
                    {
                        name = match.Groups[1].Value.Replace("-", " ");
                        name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
                    }
                }

                if (string.IsNullOrEmpty(name) || name.Length < 3)
                    continue;

                // Try to get image
                var parentArticle = link.SelectSingleNode("ancestor::article") ?? link.ParentNode;
                var img = parentArticle?.SelectSingleNode(".//img");
                var imageUrl = img?.GetAttributeValue("src", null) ?? img?.GetAttributeValue("data-src", null);

                // Generate unique ID from URL
                var modId = $"{source.Name}_{href.GetHashCode():X8}";

                mods.Add(new Mod
                {
                    Id = modId,
                    Name = HtmlEntity.DeEntitize(name),
                    Author = "Unknown",
                    ImageUrl = imageUrl,
                    RepositoryUrl = href,
                    Source = source.Name,
                    GameVersions = new List<string> { _gameVersion }
                });
            }

            _logger.Debug("Parsed {Count} mods from {Source} using FsXX parser", mods.Count, source.Name);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error parsing {Source} page", source.Name);
        }

        return mods;
    }

    /// <summary>
    /// Parses farmingsimulator25mods.com pages.
    /// </summary>
    private List<Mod> ParseFarmingSimulatorModsPage(string html, ModSource source)
    {
        var mods = new List<Mod>();
        
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // WordPress structure - look for h2 elements with links that are mod titles
            // Pattern: farmingsimulator25mods.com/{mod-slug}/
            var modLinks = doc.DocumentNode.SelectNodes(
                "//h2/a[contains(@href, 'farmingsimulator25mods.com/')]|" +
                "//article//a[contains(@href, 'farmingsimulator25mods.com/') and h2]|" +
                "//div[contains(@class,'post')]//h2/a"
            );

            if (modLinks == null || modLinks.Count == 0)
            {
                // Broader fallback - get all links that look like mod pages
                modLinks = doc.DocumentNode.SelectNodes(
                    "//a[contains(@href, 'farmingsimulator25mods.com/') and " +
                    "not(contains(@href, '/category/')) and " +
                    "not(contains(@href, '/tag/')) and " +
                    "not(contains(@href, '/page/')) and " +
                    "not(contains(@href, '#'))]"
                );
            }

            if (modLinks == null)
            {
                _logger.Warning("No mod links found for {Source}", source.Name);
                return mods;
            }

            var processedUrls = new HashSet<string>();

            foreach (var link in modLinks)
            {
                var href = link.GetAttributeValue("href", "");
                
                // Skip non-mod links
                if (string.IsNullOrEmpty(href) || 
                    href.Contains("/category/") || 
                    href.Contains("/tag/") ||
                    href.Contains("/page/") ||
                    href.Contains("/how-to") ||
                    href.Contains("/contact") ||
                    href.Contains("/about") ||
                    href.Contains("#"))
                    continue;

                if (processedUrls.Contains(href))
                    continue;
                processedUrls.Add(href);

                var name = link.InnerText?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    var match = Regex.Match(href, @"/([^/]+)/?$");
                    if (match.Success)
                    {
                        name = match.Groups[1].Value.Replace("-", " ");
                        name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
                    }
                }

                if (string.IsNullOrEmpty(name) || name.Length < 3)
                    continue;

                // Get image
                var parentArticle = link.SelectSingleNode("ancestor::article") ?? link.ParentNode?.ParentNode;
                var img = parentArticle?.SelectSingleNode(".//img");
                var imageUrl = img?.GetAttributeValue("src", null) ?? img?.GetAttributeValue("data-src", null);

                var modId = $"{source.Name}_{href.GetHashCode():X8}";

                mods.Add(new Mod
                {
                    Id = modId,
                    Name = HtmlEntity.DeEntitize(name),
                    Author = "Unknown",
                    ImageUrl = imageUrl,
                    RepositoryUrl = href,
                    Source = source.Name,
                    GameVersions = new List<string> { _gameVersion }
                });
            }

            _logger.Debug("Parsed {Count} mods from {Source} using FS25Mods parser", mods.Count, source.Name);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error parsing {Source} page", source.Name);
        }

        return mods;
    }

    #endregion

    #region JSON-LD and ModNetwork Parsing

    /// <summary>
    /// Parses mods from JSON-LD structured data embedded in the page.
    /// </summary>
    private List<Mod> ParseJsonLdMods(string html, ModSource source)
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
                                var mod = ParseJsonLdItem(item, source);
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

    private Mod? ParseJsonLdItem(JsonElement item, ModSource source)
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
            Id = $"modnetwork_{modId}",
            Name = name,
            Author = "Unknown",
            ImageUrl = imageUrl,
            RepositoryUrl = source.BaseUrl + url,
            Source = source.Name,
            GameVersions = new List<string> { _gameVersion }
        };
    }

    /// <summary>
    /// Falls back to HTML parsing for mod-network if JSON-LD is not available.
    /// </summary>
    private List<Mod> ParseModNetworkHtml(string html, ModSource source)
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
                _logger.Warning("No mod links found in HTML for {Source}", source.Name);
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
                    Id = $"modnetwork_{modId}",
                    Name = name,
                    Author = "Unknown",
                    ImageUrl = imageUrl,
                    RepositoryUrl = source.BaseUrl + href,
                    Source = source.Name,
                    GameVersions = new List<string> { _gameVersion }
                });

                _logger.Debug("Parsed mod from HTML: {ModId} - {Name}", modId, name);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error parsing HTML mods for {Source}", source.Name);
        }

        return mods;
    }

    #endregion

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

            // Try to find description from various common patterns
            var descNode = doc.DocumentNode.SelectSingleNode(
                "//div[contains(@class, 'description')]|" +
                "//div[contains(@class, 'mod-description')]|" +
                "//div[contains(@class, 'entry-content')]|" +
                "//div[contains(@class, 'post-content')]|" +
                "//p[contains(@class, 'description')]");
            
            if (descNode != null)
            {
                mod.Description = HtmlEntity.DeEntitize(descNode.InnerText?.Trim() ?? "");
            }

            // Try to find download link from various patterns
            var downloadNode = doc.DocumentNode.SelectSingleNode(
                "//a[contains(@href, 'download')]|" +
                "//a[contains(@class, 'download')]|" +
                "//a[contains(text(), 'Download')]|" +
                "//a[contains(text(), 'DOWNLOAD')]");
            
            var downloadUrl = downloadNode?.GetAttributeValue("href", null);

            if (!string.IsNullOrEmpty(downloadUrl))
            {
                // Make absolute URL if needed
                if (!downloadUrl.StartsWith("http") && !string.IsNullOrEmpty(mod.RepositoryUrl))
                {
                    var baseUri = new Uri(mod.RepositoryUrl);
                    downloadUrl = new Uri(baseUri, downloadUrl).ToString();
                }

                mod.Versions.Add(new ModVersion
                {
                    Version = mod.Version ?? "1.0",
                    DownloadUrl = downloadUrl,
                    SupportedGameVersions = mod.GameVersions,
                    ReleaseDate = DateTime.UtcNow
                });
            }

            // Try to extract author
            var authorNode = doc.DocumentNode.SelectSingleNode(
                "//span[contains(@class, 'author')]|" +
                "//a[contains(@rel, 'author')]|" +
                "//*[contains(text(), 'Author')]/following-sibling::*|" +
                "//*[contains(text(), 'Credits')]/following-sibling::*");
            
            if (authorNode != null && mod.Author == "Unknown")
            {
                var author = HtmlEntity.DeEntitize(authorNode.InnerText?.Trim() ?? "");
                if (!string.IsNullOrEmpty(author) && author.Length < 100)
                {
                    mod.Author = author;
                }
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

#region Supporting Types

/// <summary>
/// Types of mod source websites with different parsing strategies.
/// </summary>
public enum ModSourceType
{
    /// <summary>mod-network.com - uses JSON-LD structured data</summary>
    ModNetwork,
    
    /// <summary>fs19.net, fs22.com - WordPress sites with article structure</summary>
    FsXXNet,
    
    /// <summary>farmingsimulator25mods.com - WordPress with different URL patterns</summary>
    FarmingSimulatorMods
}

/// <summary>
/// Configuration for a mod source website.
/// </summary>
public class ModSource
{
    public string Name { get; }
    public string BaseUrl { get; }
    public string PagePattern { get; }
    public ModSourceType Type { get; }

    public ModSource(string name, string baseUrl, string pagePattern, ModSourceType type)
    {
        Name = name;
        BaseUrl = baseUrl;
        PagePattern = pagePattern;
        Type = type;
    }
}

#endregion
