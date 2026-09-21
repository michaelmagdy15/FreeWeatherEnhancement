using System;
using System.IO;
using System.Windows;
using SkyWeave.App.ViewModels;
using SkyWeave.App.Views;
using Wpf.Ui.Appearance;

namespace SkyWeave.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                File.WriteAllText("crash_global.log", "GLOBAL CRASH:\n" + args.ExceptionObject?.ToString());
            }
            catch { }
        };

        base.OnStartup(e);

        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        var viewModel = new MainViewModel();
        var mainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        mainWindow.Closed += (s, args) => viewModel.Dispose();
        mainWindow.Show();
    }
}
