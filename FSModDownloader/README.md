# FS Mod Downloader - Developer Documentation

Technical documentation for developers contributing to or building FS Mod Downloader.

> **For end-user documentation**, see the [main README](../README.md).

---

## 🛠️ Technology Stack

- **Framework**: .NET 8.0 (Windows)
- **UI**: WPF (Windows Presentation Foundation)
- **Pattern**: MVVM (Model-View-ViewModel)
- **MVVM Toolkit**: CommunityToolkit.Mvvm
- **Logging**: Serilog
- **HTML Parsing**: HtmlAgilityPack
- **Data Source**: mod-network.com (web scraping)

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
│   └── ModVersion.cs        # Mod version info
├── Services/                # Business logic
│   ├── GamePathDetector.cs  # Auto-detect FS installations
│   ├── IModDownloader.cs    # Download interface
│   ├── IModManager.cs       # Mod management interface
│   ├── IModRepository.cs    # Repository interface
│   ├── ModDownloader.cs     # Download implementation
│   ├── ModManager.cs        # Mod management (install/uninstall)
│   ├── ModRepository.cs     # Web scraper for mod-network.com
│   └── SettingsService.cs   # Settings persistence
├── Utilities/               # Helper classes
│   ├── FileHelper.cs
│   └── PathHelper.cs
├── ViewModels/              # MVVM ViewModels
│   └── MainWindowViewModel.cs
├── Views/                   # WPF UI
│   ├── AddGameInstanceDialog.xaml/.cs
│   ├── MainWindow.xaml/.cs
│   └── SettingsWindow.xaml/.cs
├── App.xaml/.cs             # Application entry point
├── AppSettings.cs           # Settings model
└── FSModDownloader.csproj   # Project file
```

---

## 🔧 Building

### Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or VS Code with C# extension
- Windows 10/11

### Build Commands

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Run the application
dotnet run

# Publish self-contained executable
dotnet publish -c Release -r win-x64 --self-contained true
```

### Creating a Release

```bash
# Single-file executable (requires .NET runtime)
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true

# Self-contained single-file (no runtime needed, larger file)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 🏗️ Architecture

### MVVM Pattern

- **Models**: Pure data classes (`Mod`, `GameInstance`, `ModVersion`)
- **ViewModels**: Handle UI logic and state (`MainWindowViewModel`)
- **Views**: XAML UI with data bindings (`MainWindow.xaml`)

### Key Services

| Service | Purpose |
|---------|---------|
| `ModRepository` | Scrapes mod-network.com for mod listings |
| `ModManager` | Installs/uninstalls mods to game folders |
| `ModDownloader` | Downloads mod files with progress tracking |
| `GamePathDetector` | Auto-detects FS installations (Steam, GIANTS, Documents) |
| `SettingsService` | Persists settings to `%AppData%\FSModDownloader\settings.json` |

### Game Detection

The `GamePathDetector` scans for installations in:
1. `Documents\My Games\FarmingSimulatorXXXX`
2. Steam library folders (via registry + libraryfolders.vdf)
3. GIANTS Software registry entries
4. Common paths (Program Files, D:\Games, etc.)

Supports: FS15, FS17, FS19, FS22, FS25

---

## 🌐 Mod Data Source

Mods are scraped from **mod-network.com** using HtmlAgilityPack:
- Parses JSON-LD structured data when available
- Falls back to HTML parsing
- Extracts: name, author, description, image URL, download URL

---

## 📝 Settings Storage

Settings are stored in JSON format at:
```
%AppData%\FSModDownloader\settings.json
```

Includes:
- Configured game instances
- Download path
- UI preferences

---

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Code Style

- Use C# 12 features where appropriate
- Follow Microsoft naming conventions
- Add XML documentation comments to public APIs
- Keep ViewModels thin, put logic in Services

---

## 📋 Planned Features

- [ ] Mod dependency resolution
- [ ] Version compatibility checking
- [ ] Mod update notifications
- [ ] Conflict detection
- [ ] Mod load order management
- [ ] Backup/restore mod configurations
- [ ] Multiple mod sources (ModHub, etc.)

---

## 📄 License

MIT License - See [LICENSE](../LICENSE) for details.

## Planned Features

- [ ] Mod dependency resolution
- [ ] Version compatibility checking
- [ ] Mod load order management
- [ ] Conflict detection and resolution
- [ ] Mod configuration UI
- [ ] Cloud sync for mod configurations
- [ ] Community ratings and reviews
- [ ] Multi-language support
