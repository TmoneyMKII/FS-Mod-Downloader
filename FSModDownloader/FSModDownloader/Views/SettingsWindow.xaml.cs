using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using FSModDownloader.Models;
using FSModDownloader.Services;
using Microsoft.Win32;

namespace FSModDownloader.Views;

public partial class SettingsWindow : Window
{
    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private readonly GamePathDetector _gamePathDetector = new();
    private readonly ObservableCollection<GameInstance> _gameInstances = new();
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FSModDownloader", "settings.json");

    public ObservableCollection<GameInstance> GameInstances => _gameInstances;

    public SettingsWindow()
    {
        InitializeComponent();
        
        GameInstancesList.ItemsSource = _gameInstances;
        _gameInstances.CollectionChanged += (s, e) => UpdateEmptyState();
        
        LoadSettings();
        CalculateCacheSize();
        
        SourceInitialized += SettingsWindow_SourceInitialized;
    }

    private void SettingsWindow_SourceInitialized(object? sender, EventArgs e)
    {
        EnableDarkTitleBar();
    }

    private void EnableDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            int value = 1;
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, sizeof(int));
            }
        }
    }

    private void LoadSettings()
    {
        try
        {
            // Load from settings file if exists
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                
                if (settings != null)
                {
                    DownloadPathTextBox.Text = settings.DownloadPath;
                    AutoInstallCheckBox.IsChecked = settings.AutoInstallAfterDownload;
                    DeleteAfterInstallCheckBox.IsChecked = settings.DeleteAfterInstall;
                    AutoCheckUpdatesCheckBox.IsChecked = settings.AutoCheckForUpdates;
                    NotifyUpdatesCheckBox.IsChecked = settings.NotifyOnModUpdates;
                    StartMinimizedCheckBox.IsChecked = settings.StartMinimized;
                    MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTray;
                    ConfirmExitCheckBox.IsChecked = settings.ConfirmOnExit;
                    
                    foreach (var instance in settings.GameInstances)
                    {
                        _gameInstances.Add(instance);
                    }
                }
            }
            
            // Set defaults if empty
            if (string.IsNullOrEmpty(DownloadPathTextBox.Text))
            {
                DownloadPathTextBox.Text = AppSettings.GetDefaultDownloadPath();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading settings: {ex.Message}", "Warning", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            // Use defaults
            DownloadPathTextBox.Text = AppSettings.GetDefaultDownloadPath();
        }
        
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        NoGamesMessage.Visibility = _gameInstances.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ScanForGames_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var foundInstances = _gamePathDetector.ScanForGameInstallations();
            var newCount = 0;
            
            foreach (var instance in foundInstances)
            {
                // Don't add duplicates
                if (!_gameInstances.Any(g => g.ModsPath.Equals(instance.ModsPath, StringComparison.OrdinalIgnoreCase)))
                {
                    _gameInstances.Add(instance);
                    newCount++;
                }
            }
            
            if (newCount > 0)
            {
                MessageBox.Show($"Found {newCount} new game installation(s)!", "Scan Complete", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (foundInstances.Count == 0)
            {
                MessageBox.Show("No Farming Simulator installations found.\n\nYou can manually add game instances using the 'Add Manual Entry' button.", 
                    "Scan Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("All detected game installations are already in the list.", 
                    "Scan Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error scanning for games: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddManualEntry_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddGameInstanceDialog();
        dialog.Owner = this;
        
        if (dialog.ShowDialog() == true && dialog.GameInstance != null)
        {
            // Check for duplicates
            if (_gameInstances.Any(g => g.ModsPath.Equals(dialog.GameInstance.ModsPath, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("This mods folder is already configured.", "Duplicate Entry", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            _gameInstances.Add(dialog.GameInstance);
        }
    }

    private void BrowseGamePath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string instanceId)
            return;

        var instance = _gameInstances.FirstOrDefault(g => g.Id == instanceId);
        if (instance == null) return;

        var path = BrowseForFolder(instance.ModsPath);
        if (!string.IsNullOrEmpty(path))
        {
            instance.ModsPath = path;
            // Refresh the list
            var index = _gameInstances.IndexOf(instance);
            _gameInstances.RemoveAt(index);
            _gameInstances.Insert(index, instance);
        }
    }

    private void RemoveGameInstance_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string instanceId)
            return;

        var instance = _gameInstances.FirstOrDefault(g => g.Id == instanceId);
        if (instance == null) return;

        var result = MessageBox.Show(
            $"Remove '{instance.Name}' from the list?\n\nThis will not delete any files.",
            "Remove Game Instance",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _gameInstances.Remove(instance);
        }
    }

    private void CalculateCacheSize()
    {
        try
        {
            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FSModDownloader", "cache");

            if (Directory.Exists(cachePath))
            {
                var size = GetDirectorySize(cachePath);
                CacheSizeText.Text = FormatBytes(size);
            }
            else
            {
                CacheSizeText.Text = "0 MB";
            }
        }
        catch
        {
            CacheSizeText.Text = "Unable to calculate";
        }
    }

    private long GetDirectorySize(string path)
    {
        long size = 0;
        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            size += info.Length;
        }
        return size;
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private void BrowseDownload_Click(object sender, RoutedEventArgs e)
    {
        var path = BrowseForFolder(DownloadPathTextBox.Text);
        if (!string.IsNullOrEmpty(path))
            DownloadPathTextBox.Text = path;
    }

    private string? BrowseForFolder(string initialPath)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Folder",
            InitialDirectory = Directory.Exists(initialPath) ? initialPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            return dialog.FolderName;
        }
        return null;
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear the cache? This will remove all cached mod data and images.",
            "Clear Cache",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var cachePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FSModDownloader", "cache");

                if (Directory.Exists(cachePath))
                {
                    Directory.Delete(cachePath, true);
                    Directory.CreateDirectory(cachePath);
                }

                CacheSizeText.Text = "0 MB";
                MessageBox.Show("Cache cleared successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing cache: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset all settings to their default values?\n\nThis will clear all configured game instances.",
            "Reset Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _gameInstances.Clear();
            DownloadPathTextBox.Text = AppSettings.GetDefaultDownloadPath();
            AutoInstallCheckBox.IsChecked = true;
            DeleteAfterInstallCheckBox.IsChecked = true;
            AutoCheckUpdatesCheckBox.IsChecked = true;
            NotifyUpdatesCheckBox.IsChecked = true;
            StartMinimizedCheckBox.IsChecked = false;
            MinimizeToTrayCheckBox.IsChecked = false;
            ConfirmExitCheckBox.IsChecked = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Ensure settings directory exists
            var settingsDir = Path.GetDirectoryName(SettingsFilePath)!;
            if (!Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }

            // Create download folder if needed
            if (!string.IsNullOrEmpty(DownloadPathTextBox.Text) && !Directory.Exists(DownloadPathTextBox.Text))
            {
                var result = MessageBox.Show(
                    "The download folder does not exist. Do you want to create it?",
                    "Create Folder",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Directory.CreateDirectory(DownloadPathTextBox.Text);
                }
            }

            // Save settings
            var settings = new AppSettings
            {
                DownloadPath = DownloadPathTextBox.Text,
                AutoInstallAfterDownload = AutoInstallCheckBox.IsChecked ?? true,
                DeleteAfterInstall = DeleteAfterInstallCheckBox.IsChecked ?? true,
                AutoCheckForUpdates = AutoCheckUpdatesCheckBox.IsChecked ?? true,
                NotifyOnModUpdates = NotifyUpdatesCheckBox.IsChecked ?? true,
                StartMinimized = StartMinimizedCheckBox.IsChecked ?? false,
                MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? false,
                ConfirmOnExit = ConfirmExitCheckBox.IsChecked ?? true,
                GameInstances = _gameInstances.ToList()
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
            
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
