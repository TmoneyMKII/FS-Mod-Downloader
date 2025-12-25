using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using FSModDownloader.Models;
using FSModDownloader.Services;
using Serilog;

namespace FSModDownloader.Views;

/// <summary>
/// Dialog for displaying manifest installation progress.
/// </summary>
public partial class ManifestInstallDialog : Window
{
    private readonly ILogger _logger = Log.ForContext<ManifestInstallDialog>();
    private readonly ModListManifest _manifest;
    private readonly GameInstance _gameInstance;
    private readonly ManifestInstaller _installer;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isInstalling;
    private bool _installCompleted;

    public ObservableCollection<ModInstallItem> ModItems { get; } = new();

    public ManifestInstallDialog(ModListManifest manifest, GameInstance gameInstance)
    {
        InitializeComponent();
        
        _manifest = manifest;
        _gameInstance = gameInstance;
        _installer = new ManifestInstaller();

        // Setup UI
        ManifestNameText.Text = manifest.Name;
        ManifestInfoText.Text = $"{manifest.Mods.Count} mods • {manifest.Game}";

        // Populate mod list
        foreach (var mod in manifest.Mods)
        {
            ModItems.Add(new ModInstallItem
            {
                Id = mod.Id,
                Name = mod.DisplayName,
                Status = ModInstallStatus.Pending
            });
        }

        ModsList.ItemsSource = ModItems;

        // Subscribe to installer events
        _installer.ModInstallStarted += OnModInstallStarted;
        _installer.ModInstallCompleted += OnModInstallCompleted;
        _installer.ModDownloadProgress += OnModDownloadProgress;
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_installCompleted)
        {
            DialogResult = true;
            Close();
            return;
        }

        if (_isInstalling)
            return;

        _isInstalling = true;
        InstallButton.IsEnabled = false;
        InstallButton.Content = "Installing...";

        _cancellationTokenSource = new CancellationTokenSource();

        var progress = new Progress<ManifestInstallProgress>(UpdateProgress);

        try
        {
            var result = await _installer.InstallManifestAsync(
                _manifest,
                _gameInstance,
                progress,
                _cancellationTokenSource.Token);

            _installCompleted = true;

            if (result.Success)
            {
                OverallProgressText.Text = $"✓ Successfully installed {result.InstalledCount + result.ReplacedCount} mods";
                if (result.SkippedCount > 0)
                {
                    OverallProgressText.Text += $" ({result.SkippedCount} already up-to-date)";
                }
                OverallProgressBar.Value = 100;
                OverallPercentText.Text = "100%";
                
                InstallButton.Content = "Close";
                InstallButton.IsEnabled = true;
                CancelButton.Visibility = Visibility.Collapsed;
            }
            else if (result.WasCancelled)
            {
                OverallProgressText.Text = "Installation cancelled";
                InstallButton.Content = "Close";
                InstallButton.IsEnabled = true;
                CancelButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                OverallProgressText.Text = $"Completed with {result.FailedCount} error(s)";
                InstallButton.Content = "Close";
                InstallButton.IsEnabled = true;
                CancelButton.Visibility = Visibility.Collapsed;

                // Show error summary
                if (result.Failures.Count > 0)
                {
                    var errorMsg = string.Join("\n", result.Failures.Select(f => $"• {f.ModEntry.DisplayName}: {f.Error}"));
                    MessageBox.Show($"The following mods failed to install:\n\n{errorMsg}", 
                        "Installation Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during manifest installation");
            OverallProgressText.Text = $"Error: {ex.Message}";
            InstallButton.Content = "Close";
            InstallButton.IsEnabled = true;
            CancelButton.Visibility = Visibility.Collapsed;
            _installCompleted = true;
        }
        finally
        {
            _isInstalling = false;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isInstalling)
        {
            _cancellationTokenSource?.Cancel();
            CancelButton.IsEnabled = false;
            CancelButton.Content = "Cancelling...";
        }
        else
        {
            DialogResult = false;
            Close();
        }
    }

    private void UpdateProgress(ManifestInstallProgress progress)
    {
        Dispatcher.Invoke(() =>
        {
            OverallProgressBar.Value = progress.OverallProgress;
            OverallPercentText.Text = $"{progress.OverallProgress:F0}%";
            OverallProgressText.Text = progress.StatusMessage;
            
            CurrentOperationText.Text = progress.Phase switch
            {
                ManifestInstallPhase.Analyzing => "Analyzing existing mods...",
                ManifestInstallPhase.Downloading => $"Downloading ({progress.CurrentModIndex}/{progress.TotalMods})...",
                ManifestInstallPhase.Verifying => "Verifying file hash...",
                ManifestInstallPhase.Installing => "Installing to mods folder...",
                ManifestInstallPhase.BackingUp => "Backing up existing file...",
                _ => progress.StatusMessage
            };

            CurrentProgressBar.Value = progress.CurrentOperationProgress;
        });
    }

    private void OnModInstallStarted(object? sender, ModInstallEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var item = ModItems.FirstOrDefault(m => m.Id == e.ModEntry.Id);
            if (item != null)
            {
                item.Status = ModInstallStatus.InProgress;
                item.ProgressText = "Downloading...";
            }
        });
    }

    private void OnModInstallCompleted(object? sender, ModInstallEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var item = ModItems.FirstOrDefault(m => m.Id == e.ModEntry.Id);
            if (item != null)
            {
                if (e.Success)
                {
                    item.Status = ModInstallStatus.Completed;
                }
                else
                {
                    item.Status = ModInstallStatus.Failed;
                    item.Error = e.Error ?? "Unknown error";
                }
            }
        });
    }

    private void OnModDownloadProgress(object? sender, ModDownloadProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var item = ModItems.FirstOrDefault(m => m.Id == e.ModEntry.Id);
            if (item != null)
            {
                var percent = e.TotalBytes > 0 ? (e.BytesReceived * 100.0 / e.TotalBytes) : 0;
                item.ProgressText = $"{percent:F0}%";
            }
        });
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _installer.ModInstallStarted -= OnModInstallStarted;
        _installer.ModInstallCompleted -= OnModInstallCompleted;
        _installer.ModDownloadProgress -= OnModDownloadProgress;
        _installer.Dispose();
        _cancellationTokenSource?.Dispose();
        base.OnClosing(e);
    }
}

/// <summary>
/// Represents a mod item in the installation list UI.
/// </summary>
public class ModInstallItem : INotifyPropertyChanged
{
    private ModInstallStatus _status;
    private string _progressText = "";
    private string _error = "";

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    public ModInstallStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); }
    }

    public string ProgressText
    {
        get => _progressText;
        set { _progressText = value; OnPropertyChanged(nameof(ProgressText)); }
    }

    public string Error
    {
        get => _error;
        set { _error = value; OnPropertyChanged(nameof(Error)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Status of a mod in the installation process.
/// </summary>
public enum ModInstallStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}
