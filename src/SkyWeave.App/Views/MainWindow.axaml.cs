using Avalonia.Controls;
using SkyWeave.App.ViewModels;

namespace SkyWeave.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
    }

    private void OnOpened(object? sender, System.EventArgs e)
    {
        _vm = DataContext as MainViewModel;
        if (_vm == null) return;

        Width = _vm.WindowWidth;
        Height = _vm.WindowHeight;

        if (_vm.AutoConnect)
        {
            _vm.ConnectCommand.Execute(null);
            if (_vm.IsConnected)
                _vm.StartWeatherCommand.Execute(null);
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_vm == null) return;

        _vm.WindowWidth = Width;
        _vm.WindowHeight = Height;
        _vm.SaveSettingsNow();
    }
}