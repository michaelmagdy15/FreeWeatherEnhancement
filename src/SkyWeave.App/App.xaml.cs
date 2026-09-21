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
        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                File.WriteAllText("crash_dispatcher.log", "DISPATCHER CRASH:\n" + args.Exception?.ToString());
            }
            catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                File.WriteAllText("crash_global.log", "GLOBAL CRASH:\n" + args.ExceptionObject?.ToString());
            }
            catch { }
        };

        base.OnStartup(e);

        try
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkyWeave", "Logs", "debug_lifecycle.log");
            void LogLifecycle(string msg)
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n"); } catch { }
            }

            LogLifecycle("Applying theme");
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);

            LogLifecycle("Creating MainViewModel");
            var viewModel = new MainViewModel();

            LogLifecycle("Creating MainWindow");
            var mainWindow = new MainWindow
            {
                DataContext = viewModel
            };

            MainWindow = mainWindow;

            mainWindow.Closing += (s, args) =>
            {
                LogLifecycle($"MainWindow.Closing called! Cancel={args.Cancel}. StackTrace:\n{Environment.StackTrace}");
            };

            mainWindow.Closed += (s, args) =>
            {
                LogLifecycle("MainWindow.Closed called! Disposing viewModel.");
                viewModel.Dispose();
            };

            Exit += (s, args) =>
            {
                LogLifecycle($"Application.Exit called! ExitCode={args.ApplicationExitCode}");
            };

            LogLifecycle("Calling mainWindow.Show()");
            mainWindow.Show();
            LogLifecycle($"mainWindow.Show() returned. IsVisible={mainWindow.IsVisible}, WindowState={mainWindow.WindowState}, Width={mainWindow.ActualWidth}, Height={mainWindow.ActualHeight}");
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkyWeave", "Logs", "crash_startup.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.WriteAllText(logPath, "STARTUP CRASH:\n" + ex.ToString());
            }
            catch { }
            throw;
        }
    }
}
