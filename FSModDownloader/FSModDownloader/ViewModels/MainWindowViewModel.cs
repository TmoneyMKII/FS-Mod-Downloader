using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FSModDownloader.Models;
using FSModDownloader.Services;
using Serilog;
using System.Collections.ObjectModel;

namespace FSModDownloader.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IModRepository _modRepository;
    private readonly IModManager _modManager;
    private readonly GamePathDetector _gamePathDetector;
    private readonly ILogger _logger = Log.ForContext<MainWindowViewModel>();

    [ObservableProperty]
    private List<Mod> availableMods = new();

    [ObservableProperty]
    private List<Mod> installedMods = new();

    [ObservableProperty]
    private List<GameInstance> gameInstances = new();

    [ObservableProperty]
    private GameInstance? selectedGameInstance;

    [ObservableProperty]
    private Mod? selectedMod;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string statusMessage = "Ready";

    public MainWindowViewModel()
    {
        _gamePathDetector = new GamePathDetector();
        _modRepository = new ModRepository("fs2025"); // Use FS25 by default
        
        var downloader = new ModDownloader();
        _modManager = new ModManager(downloader);

        // Fire and forget with proper error handling
        _ = InitializeAsync();
    }

    /// <summary>
    /// Initializes the application on startup.
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading settings...";

            // Load game instances from saved settings
            var settings = SettingsService.Load();
            GameInstances = settings.GameInstances;

            if (GameInstances.Count > 0)
            {
                // Select the previously selected instance, or the first one
                if (!string.IsNullOrEmpty(settings.SelectedGameInstanceId))
                {
                    SelectedGameInstance = GameInstances.FirstOrDefault(g => g.Id == settings.SelectedGameInstanceId) 
                                          ?? GameInstances[0];
                }
                else
                {
                    SelectedGameInstance = GameInstances[0];
                }
                
                // Load installed mods for the selected game
                await RefreshInstalledModsAsync();
            }

            // Load latest mods from ModHub
            StatusMessage = "Loading mods from ModHub...";
            await LoadLatestModsAsync();

            StatusMessage = $"Found {GameInstances.Count} game installation(s), {AvailableMods.Count} mods available";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error initializing application");
            StatusMessage = "Ready - Error loading mods";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Reloads game instances from settings. Call after settings are saved.
    /// </summary>
    public void ReloadGameInstances()
    {
        SettingsService.ClearCache();
        var settings = SettingsService.Load();
        GameInstances = settings.GameInstances;
        
        if (GameInstances.Count > 0)
        {
            // Try to keep the same selection
            if (SelectedGameInstance != null)
            {
                var current = GameInstances.FirstOrDefault(g => g.Id == SelectedGameInstance.Id);
                SelectedGameInstance = current ?? GameInstances[0];
            }
            else
            {
                SelectedGameInstance = GameInstances[0];
            }
        }
        else
        {
            SelectedGameInstance = null;
        }
        
        StatusMessage = $"Found {GameInstances.Count} game installation(s), {AvailableMods.Count} mods available";
    }

    /// <summary>
    /// Loads the latest mods from the repository.
    /// </summary>
    private async Task LoadLatestModsAsync()
    {
        try
        {
            AvailableMods = await _modRepository.SearchModsAsync(string.Empty, null, 1, 50);
            _logger.Information("Loaded {Count} mods from repository", AvailableMods.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading latest mods");
        }
    }

    /// <summary>
    /// Searches for mods in the repository.
    /// </summary>
    [RelayCommand]
    public async Task SearchModsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = $"Searching for '{SearchQuery}'...";

            AvailableMods = await _modRepository.SearchModsAsync(SearchQuery);

            StatusMessage = $"Found {AvailableMods.Count} mod(s)";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error searching mods");
            StatusMessage = "Error searching mods";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Refreshes the list of installed mods.
    /// </summary>
    [RelayCommand]
    public async Task RefreshInstalledModsAsync()
    {
        try
        {
            if (SelectedGameInstance == null)
                return;

            IsLoading = true;
            StatusMessage = "Scanning for installed mods...";

            InstalledMods = await _modManager.GetInstalledModsAsync(SelectedGameInstance.ModsPath);

            StatusMessage = $"Found {InstalledMods.Count} installed mod(s)";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error refreshing installed mods");
            StatusMessage = "Error refreshing installed mods";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Installs the selected mod.
    /// </summary>
    [RelayCommand]
    public async Task InstallModAsync()
    {
        try
        {
            if (SelectedMod == null || SelectedGameInstance == null)
            {
                StatusMessage = "Please select a mod and game instance";
                return;
            }

            IsLoading = true;
            StatusMessage = $"Installing {SelectedMod.Name}...";

            var latestVersion = SelectedMod.Versions.FirstOrDefault();
            if (latestVersion == null)
            {
                StatusMessage = "No versions available for this mod";
                return;
            }

            var success = await _modManager.InstallModAsync(
                SelectedMod, 
                latestVersion, 
                SelectedGameInstance.ModsPath);

            if (success)
            {
                StatusMessage = $"Successfully installed {SelectedMod.Name}";
                await RefreshInstalledModsAsync();
            }
            else
            {
                StatusMessage = $"Failed to install {SelectedMod.Name}";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error installing mod");
            StatusMessage = "Error installing mod";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Uninstalls the selected mod.
    /// </summary>
    [RelayCommand]
    public async Task UninstallModAsync()
    {
        try
        {
            if (SelectedMod == null || SelectedGameInstance == null)
            {
                StatusMessage = "Please select a mod and game instance";
                return;
            }

            IsLoading = true;
            StatusMessage = $"Uninstalling {SelectedMod.Name}...";

            var success = await _modManager.UninstallModAsync(
                SelectedMod, 
                SelectedGameInstance.ModsPath);

            if (success)
            {
                StatusMessage = $"Successfully uninstalled {SelectedMod.Name}";
                await RefreshInstalledModsAsync();
            }
            else
            {
                StatusMessage = $"Failed to uninstall {SelectedMod.Name}";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error uninstalling mod");
            StatusMessage = "Error uninstalling mod";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
