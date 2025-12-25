using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using FSModDownloader.Models;
using FSModDownloader.Services;
using FSModDownloader.ViewModels;
using Microsoft.Win32;
using Serilog;

namespace FSModDownloader.Views;

public partial class MainWindow : Window
{
    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        
        // Apply dark title bar before window is shown
        SourceInitialized += MainWindow_SourceInitialized;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        EnableDarkTitleBar();
    }

    private void EnableDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            int value = 1; // Enable dark mode
            
            // Try the newer attribute first (Windows 10 20H1+)
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
            {
                // Fall back to older attribute for earlier Windows 10 versions
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, sizeof(int));
            }
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow
        {
            Owner = this
        };
        
        if (settingsWindow.ShowDialog() == true)
        {
            // Settings were saved, reload game instances
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.ReloadGameInstances();
            }
        }
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "FS Mod Downloader v1.0.0\n\n" +
            "A mod manager for Farming Simulator games.\n\n" +
            "© 2025 - Open Source Project",
            "About FS Mod Downloader",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void ImportManifestButton_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel?.SelectedGameInstance == null)
        {
            MessageBox.Show("Please select a game instance first.", "No Game Selected", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var openDialog = new OpenFileDialog
        {
            Title = "Import Modlist Manifest",
            Filter = "JSON Modlist (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (openDialog.ShowDialog() != true)
            return;

        try
        {
            var manifestService = new ManifestService();
            var loadResult = await manifestService.LoadFromFileAsync(openDialog.FileName);

            if (!loadResult.Success)
            {
                var errorMsg = loadResult.Error ?? "Unknown error";
                if (loadResult.ValidationErrors.Count > 0)
                {
                    errorMsg += "\n\nValidation errors:\n" + string.Join("\n", loadResult.ValidationErrors.Take(5));
                }
                MessageBox.Show($"Failed to load manifest:\n\n{errorMsg}", "Import Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var manifest = loadResult.Manifest!;

            // Confirm with user
            var confirmResult = MessageBox.Show(
                $"Import modlist '{manifest.Name}'?\n\n" +
                $"Game: {manifest.Game}\n" +
                $"Mods: {manifest.Mods.Count}\n" +
                $"Target: {viewModel.SelectedGameInstance.Name}",
                "Confirm Import",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            // Show install dialog
            var installDialog = new ManifestInstallDialog(manifest, viewModel.SelectedGameInstance)
            {
                Owner = this
            };

            if (installDialog.ShowDialog() == true)
            {
                // Refresh installed mods
                await viewModel.RefreshInstalledModsCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error importing manifest");
            MessageBox.Show($"Error importing manifest:\n\n{ex.Message}", "Import Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExportManifestButton_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel?.SelectedGameInstance == null)
        {
            MessageBox.Show("Please select a game instance first.", "No Game Selected", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (viewModel.InstalledMods.Count == 0)
        {
            MessageBox.Show("No mods are installed to export.", "No Mods", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = "Export Modlist Manifest",
            Filter = "JSON Modlist (*.json)|*.json",
            DefaultExt = ".json",
            FileName = $"{viewModel.SelectedGameInstance.Name}_modlist.json"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        try
        {
            var manifestService = new ManifestService();
            var installer = new ManifestInstaller();

            // Create manifest from installed mods
            var manifest = manifestService.CreateNew(
                name: $"{viewModel.SelectedGameInstance.Name} Modlist",
                game: viewModel.SelectedGameInstance.GameId);

            manifest.Description = $"Exported from {viewModel.SelectedGameInstance.Name} on {DateTime.UtcNow:yyyy-MM-dd}";

            // Add installed mods
            foreach (var mod in viewModel.InstalledMods)
            {
                // Try to find the mod file - use the first version's info or derive from mod name
                var latestVersion = mod.Versions.FirstOrDefault();
                var fileName = $"{mod.Name.Replace(" ", "_")}.zip";
                var modPath = System.IO.Path.Combine(viewModel.SelectedGameInstance.ModsPath, fileName);
                
                // Try common patterns if exact name doesn't work
                if (!System.IO.File.Exists(modPath))
                {
                    var possibleFiles = System.IO.Directory.GetFiles(viewModel.SelectedGameInstance.ModsPath, "*.zip")
                        .Where(f => System.IO.Path.GetFileName(f).Contains(mod.Name.Split(' ')[0], StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    if (possibleFiles.Count > 0)
                        modPath = possibleFiles[0];
                    else
                        continue; // Skip this mod if we can't find the file
                }

                var fileInfo = new System.IO.FileInfo(modPath);
                var hash = await installer.ComputeFileHashAsync(modPath);

                manifest.Mods.Add(new ManifestModEntry
                {
                    Id = mod.Id ?? mod.Name.ToLowerInvariant().Replace(" ", "-"),
                    Title = mod.Name,
                    Version = mod.Version,
                    FileName = fileInfo.Name,
                    Sha256 = hash,
                    SizeBytes = fileInfo.Length,
                    // Note: SourceUrl will be empty - user needs to add manually or use mod URLs
                    SourceUrl = latestVersion?.DownloadUrl ?? ""
                });
            }

            installer.Dispose();

            await manifestService.SaveToFileAsync(manifest, saveDialog.FileName);

            MessageBox.Show(
                $"Exported {manifest.Mods.Count} mods to manifest.\n\n" +
                "Note: You may need to manually add download URLs for mods\n" +
                "that were installed manually (not from a repository).",
                "Export Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error exporting manifest");
            MessageBox.Show($"Error exporting manifest:\n\n{ex.Message}", "Export Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
