using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace FSModDownloader.Views;

public partial class SettingsWindow : Window
{
    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public SettingsWindow()
    {
        InitializeComponent();
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
        // Load default paths
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        
        FS22PathTextBox.Text = Path.Combine(documentsPath, "My Games", "FarmingSimulator2022", "mods");
        FS25PathTextBox.Text = Path.Combine(documentsPath, "My Games", "FarmingSimulator2025", "mods");
        DownloadPathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "FSMods");
        
        // TODO: Load saved settings from config file
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

    private void BrowseFS22_Click(object sender, RoutedEventArgs e)
    {
        var path = BrowseForFolder(FS22PathTextBox.Text);
        if (!string.IsNullOrEmpty(path))
            FS22PathTextBox.Text = path;
    }

    private void BrowseFS25_Click(object sender, RoutedEventArgs e)
    {
        var path = BrowseForFolder(FS25PathTextBox.Text);
        if (!string.IsNullOrEmpty(path))
            FS25PathTextBox.Text = path;
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
            "Are you sure you want to reset all settings to their default values?",
            "Reset Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            LoadSettings();
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
        // Validate paths
        if (!string.IsNullOrEmpty(FS22PathTextBox.Text) && !Directory.Exists(FS22PathTextBox.Text))
        {
            var result = MessageBox.Show(
                "The FS22 mods folder does not exist. Do you want to create it?",
                "Create Folder",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try { Directory.CreateDirectory(FS22PathTextBox.Text); }
                catch { MessageBox.Show("Failed to create folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            }
        }

        if (!string.IsNullOrEmpty(FS25PathTextBox.Text) && !Directory.Exists(FS25PathTextBox.Text))
        {
            var result = MessageBox.Show(
                "The FS25 mods folder does not exist. Do you want to create it?",
                "Create Folder",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try { Directory.CreateDirectory(FS25PathTextBox.Text); }
                catch { MessageBox.Show("Failed to create folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            }
        }

        // TODO: Save settings to config file
        
        DialogResult = true;
        Close();
    }
}
