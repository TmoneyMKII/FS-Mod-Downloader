using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using FSModDownloader.Models;
using FSModDownloader.Services;
using Microsoft.Win32;

namespace FSModDownloader.Views;

public partial class AddGameInstanceDialog : Window
{
    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public GameInstance? GameInstance { get; private set; }

    public AddGameInstanceDialog()
    {
        InitializeComponent();
        LoadGameTypes();
        
        SourceInitialized += Dialog_SourceInitialized;
    }

    private void Dialog_SourceInitialized(object? sender, EventArgs e)
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

    private void LoadGameTypes()
    {
        var gameTypes = GamePathDetector.GetAvailableGameTypes();
        
        foreach (var (gameId, displayName) in gameTypes)
        {
            GameVersionComboBox.Items.Add(new GameTypeItem { GameId = gameId, DisplayName = displayName });
        }
        
        // Select FS25 by default
        GameVersionComboBox.SelectedIndex = 0;
    }

    private void BrowsePath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Mods Folder",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            ModsPathTextBox.Text = dialog.FolderName;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        // Validate
        if (GameVersionComboBox.SelectedItem is not GameTypeItem selectedGame)
        {
            MessageBox.Show("Please select a game version.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ModsPathTextBox.Text))
        {
            MessageBox.Show("Please specify the mods folder path.", "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var modsPath = ModsPathTextBox.Text.Trim();
        
        // Create folder if it doesn't exist
        if (!Directory.Exists(modsPath))
        {
            var result = MessageBox.Show(
                "The mods folder does not exist. Do you want to create it?",
                "Create Folder",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Directory.CreateDirectory(modsPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create folder: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                return;
            }
        }

        // Create the game instance
        var customName = CustomNameTextBox.Text.Trim();
        GameInstance = new GameInstance
        {
            Id = Guid.NewGuid().ToString(),
            Name = string.IsNullOrEmpty(customName) ? selectedGame.DisplayName : customName,
            GameId = selectedGame.GameId,
            ModsPath = modsPath,
            GamePath = Path.GetDirectoryName(modsPath) ?? string.Empty,
            Source = "Manual",
            IsManual = true,
            IsValid = true,
            LastModified = Directory.Exists(modsPath) ? Directory.GetLastWriteTime(modsPath) : DateTime.Now
        };

        DialogResult = true;
        Close();
    }

    private class GameTypeItem
    {
        public string GameId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        
        public override string ToString() => DisplayName;
    }
}
