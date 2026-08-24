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

        Width = _vm.Settings.WindowWidth;
        Height = _vm.Settings.WindowHeight;

        if (_vm.Settings.AutoConnect)
        {
            _vm.Connection.ConnectCommand.Execute(null);
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_vm == null) return;

        _vm.Settings.WindowWidth = Width;
        _vm.Settings.WindowHeight = Height;
        _vm.SaveSettings();
    }
}