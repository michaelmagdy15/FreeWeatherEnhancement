using Avalonia;
using System;

namespace SkyWeave.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => 
        {
            System.IO.File.WriteAllText("crash_global.log", "GLOBAL CRASH:\n" + e.ExceptionObject?.ToString());
        };
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText("crash.log", ex.ToString());
            Console.WriteLine("FATAL ERROR: " + ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
