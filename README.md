# FS Mod Downloader

A desktop application for managing and downloading Farming Simulator mods, similar to CKAN for Kerbal Space Program.

## Features

- **Mod Discovery**: Browse and search for available mods from the repository
- **One-Click Installation**: Easy mod installation with automatic dependency management
- **Mod Management**: Install, uninstall, enable, and disable mods
- **Multiple Game Versions**: Support for different Farming Simulator versions
- **Auto-Detection**: Automatically detects Farming Simulator installations
- **Download Management**: Track and manage mod downloads with progress indicators
- **Mod Categories**: Filter mods by category (Vehicles, Maps, Equipment, etc.)
- **Version Control**: Manage multiple versions of mods

## Architecture

The application is built using C# and WPF with the following structure:

- **Models**: Data models for mods, game instances, and versions
- **Services**: Core business logic for mod management, downloading, and repository access
- **ViewModels**: MVVM pattern for UI state management
- **Views**: WPF UI components

## Building

Requirements:
- .NET 8.0 SDK or later
- Visual Studio 2022 or Visual Studio Code

```bash
dotnet build
```

## Running

```bash
dotnet run
```

## Project Structure

```
FSModDownloader/
├── Models/              # Data models
├── Services/            # Business logic
├── ViewModels/          # MVVM ViewModels
├── Views/               # WPF UI
├── Utilities/           # Helper utilities
├── App.xaml             # Application resources
└── AppSettings.cs       # Configuration
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Author

**SWR Entertainment**  
Created by Tyler Bradham

- GitHub: [@TmoneyMKII](https://github.com/TmoneyMKII)
- Discord: tmoneymkii

## License

This project is licensed under the MIT License.

## Planned Features

- [ ] Mod dependency resolution
- [ ] Version compatibility checking
- [ ] Mod load order management
- [ ] Conflict detection and resolution
- [ ] Mod configuration UI
- [ ] Cloud sync for mod configurations
- [ ] Community ratings and reviews
- [ ] Multi-language support
