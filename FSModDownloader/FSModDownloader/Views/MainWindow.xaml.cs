using System.Windows;
using FSModDownloader.ViewModels;

namespace FSModDownloader.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
