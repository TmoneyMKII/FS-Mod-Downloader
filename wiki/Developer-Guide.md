# Developer Guide

Technical documentation for contributing to FS Mod Downloader.

---

## 🛠️ Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 8.0 (Windows) |
| UI | WPF (Windows Presentation Foundation) |
| Pattern | MVVM (Model-View-ViewModel) |
| MVVM Toolkit | CommunityToolkit.Mvvm |
| Logging | Serilog |
| HTML Parsing | HtmlAgilityPack |
| Data Source | Web scraping (mod-network.com, etc.) |

---

## 📁 Project Structure

```
FSModDownloader/
├── Assets/                  # App icons and images
│   ├── favicon.ico
│   └── Logo.png
├── Models/                  # Data models
│   ├── GameInstance.cs      # Game installation representation
│   ├── Mod.cs               # Mod data model
│   ├── ModVersion.cs        # Mod version info
│   └── ModListManifest.cs   # Modlist export format
├── Services/                # Business logic
│   ├── GamePathDetector.cs  # Auto-detect FS installations
│   ├── IModDownloader.cs    # Download interface
│   ├── IModManager.cs       # Mod management interface
│   ├── IModRepository.cs    # Repository interface
│   ├── ModDownloader.cs     # Download implementation
│   ├── ModManager.cs        # Install/uninstall logic
│   ├── ModRepository.cs     # Web scraper (multi-source)
│   ├── ManifestService.cs   # Modlist import/export
│   └── SettingsService.cs   # Settings persistence
├── Utilities/               # Helper classes
│   ├── FileHelper.cs        # File operations
│   └── PathHelper.cs        # Path utilities
├── ViewModels/              # MVVM ViewModels
│   └── MainWindowViewModel.cs
├── Views/                   # WPF UI
│   ├── MainWindow.xaml/.cs
│   ├── SettingsWindow.xaml/.cs
│   ├── AddGameInstanceDialog.xaml/.cs
│   └── ManifestInstallDialog.xaml/.cs
├── App.xaml/.cs             # Application entry point
└── AppSettings.cs           # Settings model
```

---

## 🔧 Building from Source

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or VS Code with C# extension
- Windows 10/11

### Build Commands

```bash
# Clone the repository
git clone https://github.com/TmoneyMKII/FS-Mod-Downloader.git
cd FS-Mod-Downloader

# Restore dependencies
dotnet restore

# Debug build
dotnet build

# Release build
dotnet build -c Release

# Run the application
dotnet run --project FSModDownloader/FSModDownloader.csproj

# Publish self-contained executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 🏗️ Architecture

### MVVM Pattern

```
┌─────────────┐     ┌──────────────────┐     ┌────────────┐
│    View     │────▶│    ViewModel     │────▶│   Model    │
│   (XAML)    │◀────│  (Observable)    │◀────│   (Data)   │
└─────────────┘     └──────────────────┘     └────────────┘
                            │
                            ▼
                    ┌──────────────┐
                    │   Services   │
                    │ (Business)   │
                    └──────────────┘
```

### Key Components

**ViewModels** use `[ObservableProperty]` from CommunityToolkit.Mvvm:

```csharp
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private List<Mod> availableMods = new();

    [ObservableProperty]
    private bool isLoading = false;
}
```

**Services** follow interface patterns for testability:

```csharp
public interface IModRepository
{
    Task<List<Mod>> SearchModsAsync(string query, string? category = null);
    Task<Mod?> GetModDetailsAsync(string modId);
}
```

### Data Flow

1. **Mod Discovery**
   ```
   ModRepository.SearchModsAsync()
        ↓
   HTTP GET to mod website
        ↓
   HtmlAgilityPack parses HTML
        ↓
   Returns List<Mod>
        ↓
   ViewModel updates AvailableMods
        ↓
   UI updates via data binding
   ```

2. **Mod Installation**
   ```
   User clicks Install
        ↓
   ModManager.InstallModAsync()
        ↓
   ModDownloader.DownloadModAsync()
        ↓
   Extract to mods folder
        ↓
   Cleanup temp files
        ↓
   Refresh installed mods list
   ```

---

## 🎯 Key Services

### ModRepository

The heart of mod discovery. Scrapes multiple websites:

```csharp
public class ModRepository : IModRepository
{
    // Cache to reduce HTTP requests
    private readonly Dictionary<string, (Mod mod, DateTime cachedAt)> _modCache = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(10);

    // Configure sources per game version
    private List<ModSource> GetSourcesForGame(string gameVersion)
    {
        return gameVersion switch
        {
            "FS25" => new List<ModSource>
            {
                new("mod-network", "https://mod-network.com", ...),
                new("fs25mods", "https://farmingsimulator25mods.com", ...),
            },
            // ...
        };
    }
}
```

### GamePathDetector

Finds FS installations across multiple locations:

```csharp
public class GamePathDetector
{
    public List<GameInstance> ScanForGameInstallations()
    {
        // 1. Check Documents\My Games
        var documentsPath = GetDocumentsModsPath(gameInfo.FolderName);
        
        // 2. Check Steam library
        var steamPath = GetSteamGamePath(gameInfo.SteamAppId);
        
        // 3. Check GIANTS registry
        var giantsPath = GetGiantsRegistryPath(gameInfo.RegistryName);
    }
}
```

### SettingsService

Persists settings to JSON:

```csharp
public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FSModDownloader", "settings.json");

    public static AppSettings Load() { ... }
    public static void Save(AppSettings settings) { ... }
}
```

---

## 📝 Code Conventions

### Async Patterns

- Use `async Task` for async methods
- Fire-and-forget in ViewModels: `_ = InitializeAsync();`
- Never use `.Result` or `.Wait()` (causes deadlocks in WPF)

### Logging

Use Serilog with structured logging:

```csharp
private readonly ILogger _logger = Log.ForContext<MyClass>();

_logger.Information("Installing mod {ModId} version {Version}", mod.Id, version);
_logger.Error(ex, "Failed to download {ModName}", modName);
```

### Error Handling

- Catch exceptions at service boundaries
- Log errors with context
- Return meaningful results (bool success, null on failure)

```csharp
public async Task<bool> InstallModAsync(Mod mod, ...)
{
    try
    {
        // ... installation logic
        return true;
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "Error installing mod {ModId}", mod.Id);
        return false;
    }
}
```

### Null Safety

- Use nullable annotations: `string?`, `List<T>?`
- Check for null before operations
- Use null-coalescing: `value ?? defaultValue`

---

## 🧪 Testing

Tests live in `FSModDownloader.Tests/`:

```bash
# Run tests
dotnet test
```

### What to Test

- Service logic (ModRepository parsing, GamePathDetector)
- Utility functions (FileHelper, PathHelper)
- ViewModel commands (if complex logic)

### Test Example

```csharp
[Fact]
public void FormatFileSize_ReturnsCorrectString()
{
    Assert.Equal("1 KB", FileHelper.FormatFileSize(1024));
    Assert.Equal("1.5 MB", FileHelper.FormatFileSize(1572864));
}
```

---

## 🤝 Contributing

### Workflow

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Make your changes
4. Test thoroughly
5. Commit with clear messages: `git commit -m 'feat: Add amazing feature'`
6. Push: `git push origin feature/amazing-feature`
7. Open a Pull Request

### Commit Messages

Follow conventional commits:
- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation
- `refactor:` Code restructuring
- `test:` Tests
- `chore:` Maintenance

### Code Style

- Follow Microsoft C# naming conventions
- Use XML documentation comments on public APIs
- Keep ViewModels thin, put logic in Services
- Use `var` when type is obvious

---

## 📚 Resources

- [CommunityToolkit.Mvvm Documentation](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [WPF Documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
- [Serilog Documentation](https://github.com/serilog/serilog/wiki)
- [HtmlAgilityPack Documentation](https://html-agility-pack.net/)

---

## ❓ Questions?

- Open a [GitHub Discussion](https://github.com/TmoneyMKII/FS-Mod-Downloader/discussions)
- Check existing [Issues](https://github.com/TmoneyMKII/FS-Mod-Downloader/issues)
