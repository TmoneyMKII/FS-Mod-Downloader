# FS Mod Downloader v1.0.0 - Initial Release 🎉

**Release Date:** December 25, 2025  
**Author:** Tyler Bradham / SWR Entertainment

---

## 🚀 Overview

The first official release of FS Mod Downloader - a desktop application for managing and downloading Farming Simulator mods, similar to CKAN for Kerbal Space Program.

---

## ✨ Features

### 🎮 Multi-Game Support
- **Supports all modern Farming Simulator titles:** FS15, FS17, FS19, FS22, and FS25
- Automatic game detection via Documents folder, Steam library, and GIANTS registry
- Manual game instance configuration for custom setups

### 🔍 Mod Discovery
- **Multi-source mod aggregation** - Browse mods from multiple websites:
  - mod-network.com
  - farmingsimulator25mods.com
  - And more sources per game version
- Search mods by name, author, or keyword
- Category filtering (Tractors, Maps, Equipment, etc.)
- Mod details with images, descriptions, and version info

### 📦 Mod Management
- **One-click mod installation** directly to your game's mods folder
- Download progress tracking
- Automatic temp file cleanup after installation
- View installed mods per game instance

### 📋 Modlist Import/Export
- **Export your mod configuration** as shareable manifest files
- **Import modlists** from other users or your own backups
- Bulk mod installation from manifests
- Perfect for sharing server mod packs or migrating setups

### ⚙️ Settings & Configuration
- Persistent settings saved to `%AppData%\FSModDownloader\settings.json`
- Configurable download paths
- Auto-detection scan for game installations
- Dark, modern UI

---

## 💻 System Requirements

- **OS:** Windows 10/11 (64-bit)
- **Runtime:** .NET 8.0 (included in standalone package)
- **Internet:** Required for mod browsing and downloads

---

## 📥 Installation

1. Download `FSModDownloader-v1.0.0-win-x64-standalone.zip` (recommended)
2. Extract to any folder
3. Run `FSModDownloader.exe`
4. Click **Settings → Scan for Games** to detect your installations

---

## 🐛 Known Limitations

- Mod extraction is a work-in-progress (downloads work, extraction may require manual unzip)
- Enable/Disable mod features are placeholders
- No mod dependency resolution yet
- Web scraping may break if source websites change their structure

---

## 🔮 Planned for Future Releases

- [ ] Complete mod extraction implementation
- [ ] Mod dependency resolution
- [ ] Version compatibility checking  
- [ ] Conflict detection
- [ ] Mod load order management
- [ ] Auto-update checking
- [ ] Backup/restore mod configurations

---

## 🙏 Acknowledgments

Built with:
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM framework
- [HtmlAgilityPack](https://html-agility-pack.net/) - HTML parsing
- [Serilog](https://serilog.net/) - Structured logging

---

## 📝 Full Changelog

- `feat:` Initial application structure with MVVM pattern
- `feat:` Game path detection (Documents, Steam, GIANTS registry)
- `feat:` Multi-source mod repository with caching
- `feat:` Mod search and category filtering
- `feat:` Settings persistence service
- `feat:` Manual game instance configuration dialog
- `feat:` Modlist manifest import/export functionality
- `feat:` Application icon and branding assets
- `fix:` Temp file cleanup after mod installation

---

**Download:** [GitHub Releases](https://github.com/TmoneyMKII/FS-Mod-Downloader/releases/tag/v1.0.0)
