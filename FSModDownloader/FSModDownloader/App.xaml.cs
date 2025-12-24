using System.Windows;
using Serilog;
using FSModDownloader.Views;

namespace FSModDownloader;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure Serilog for logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FSModDownloader",
                    "logs",
                    "fsmoddl-.txt"),
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("Application starting");

        try
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application startup failed");
            MessageBox.Show("Failed to start application: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application closing");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
