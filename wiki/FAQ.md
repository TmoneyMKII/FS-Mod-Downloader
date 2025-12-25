# Frequently Asked Questions

Common questions about FS Mod Downloader.

---

## General Questions

### What is FS Mod Downloader?

FS Mod Downloader is a free, open-source desktop application for managing Farming Simulator mods. It lets you browse, search, install, and organize mods for FS15 through FS25 from one convenient interface.

### Is it free?

Yes! FS Mod Downloader is completely free and open source under the MIT License.

### Is it safe?

Yes. The application:
- Downloads mods from established mod hosting sites
- Doesn't modify your game files
- Doesn't require admin rights for normal operation
- Is open source - you can review the code yourself

### Why does Windows show a security warning?

The app isn't code-signed with a paid certificate, which causes Windows SmartScreen to show a warning. Click "More info" → "Run anyway" to proceed. This is normal for independent software.

---

## Compatibility

### Which games are supported?

| Game | Supported |
|------|-----------|
| Farming Simulator 25 | ✅ Yes |
| Farming Simulator 22 | ✅ Yes |
| Farming Simulator 19 | ✅ Yes |
| Farming Simulator 17 | ✅ Yes |
| Farming Simulator 15 | ✅ Yes |

### Does it work with Steam and non-Steam versions?

Yes! The app works with both Steam and standalone (GIANTS) installations.

### Does it work on Mac or Linux?

Not currently. FS Mod Downloader is Windows-only (Windows 10/11 64-bit). Since Farming Simulator is primarily a Windows game, this covers most users.

### Will mods from one FS version work in another?

No. FS25 mods only work in FS25, FS22 mods only in FS22, etc. Always download mods for your specific game version.

---

## Mods & Sources

### Where do the mods come from?

Mods are sourced from popular mod hosting websites:
- mod-network.com
- farmingsimulator25mods.com
- And others depending on game version

We aggregate from multiple sources to give you the widest selection.

### Are these official mods?

The app includes both official (GIANTS ModHub) and community mods. Check mod descriptions for details about the source and author.

### Can I add custom mod sources?

Not in the current version, but this is on the roadmap for future releases.

### Why can't I find a specific mod?

Possible reasons:
- The mod isn't on our supported sources
- The mod was removed by its author
- Search with different keywords
- The mod might be exclusive to ModHub (in-game store)

### Are the mods virus-free?

We source from reputable mod sites, but we can't guarantee every mod. Use common sense:
- Stick to popular, well-reviewed mods
- Be cautious of mods with very few downloads
- Your antivirus should scan downloaded files

---

## Installation & Usage

### Where are mods installed?

Mods are installed directly to your game's mods folder:
```
Documents\My Games\FarmingSimulatorXXXX\mods
```

### Do I need to restart the game after installing mods?

Yes. Farming Simulator loads mods at startup, so you need to restart the game to see new mods.

### Can I install multiple mods at once?

Yes! Use the [Modlist](Modlists) feature to import and install multiple mods at once.

### How do I uninstall a mod?

1. Find the mod in the "Installed Mods" section
2. Click the Uninstall button
3. The mod files are deleted from your mods folder

### Can I disable a mod without uninstalling it?

This feature is planned but not yet implemented. For now, you can manually rename the mod file (add `.disabled` to the filename) to prevent it from loading.

---

## Multiplayer

### Can I use this for multiplayer server mods?

Absolutely! This is one of the best use cases:

1. Server admin exports their modlist
2. Share the modlist file with players
3. Players import the modlist
4. Everyone has matching mods

### Will it work with dedicated servers?

Yes, you can use it to manage mods for dedicated server installations by manually configuring the server's mods folder path.

---

## Technical Questions

### What is the .NET requirement?

The standalone package includes everything needed. The smaller package requires .NET 8.0 Desktop Runtime, which you can download from [Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0).

### Where are settings stored?

Settings are saved to:
```
%AppData%\FSModDownloader\settings.json
```

### Where are log files?

Logs are saved to:
```
%AppData%\FSModDownloader\logs\
```

### How much disk space does it need?

- Application: ~150MB (standalone) or ~5MB (with .NET)
- Plus space for downloaded mods (varies)

### Does it use the internet?

Yes, an internet connection is required to:
- Browse available mods
- Download mods
- Check for updates

Viewing installed mods works offline.

---

## Troubleshooting Quick Reference

| Problem | Quick Fix |
|---------|-----------|
| App won't start | Install .NET 8.0 or use standalone package |
| No games found | Settings → Scan for Games, or add manually |
| Download fails | Check internet, increase timeout in Settings |
| Mod not in game | Verify mods folder path, restart game |
| Permission denied | Run as Administrator |

For detailed solutions, see the [Troubleshooting](Troubleshooting) page.

---

## Feature Requests

### How can I request a feature?

Open an issue on [GitHub](https://github.com/TmoneyMKII/FS-Mod-Downloader/issues) with the "enhancement" label.

### What features are planned?

See the [roadmap](https://github.com/TmoneyMKII/FS-Mod-Downloader#planned-features) for upcoming features:
- Mod dependency resolution
- Version compatibility checking
- Conflict detection
- Mod load order management
- And more!

---

## Contributing

### Can I contribute to the project?

Yes! We welcome contributions:
- Report bugs
- Suggest features
- Submit pull requests
- Improve documentation

See the [Developer Guide](Developer-Guide) to get started.

### Is the source code available?

Yes, the full source code is on [GitHub](https://github.com/TmoneyMKII/FS-Mod-Downloader) under the MIT License.
