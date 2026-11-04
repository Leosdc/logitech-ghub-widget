using System;
using System.IO;
using System.Windows;
using GHubWidget.Helpers;
using GHubWidget.Models;

namespace GHubWidget;

public partial class App : System.Windows.Application
{
    public App()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        AppLogger.Log("Application starting...");

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                var err = $"UnhandledException:\n{args.ExceptionObject}";
                AppLogger.Log(err);
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogitechGHubWidget", "crash.log");
                File.WriteAllText(logPath, err);
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                var err = $"DispatcherUnhandledException:\n{args.Exception}";
                AppLogger.Log(err);
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogitechGHubWidget", "crash.log");
                File.WriteAllText(logPath, err);
            }
            catch { }
            args.Handled = true;
        };

        base.OnStartup(e);
        AppLogger.Log("Application startup completed.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogger.Log($"Application OnExit with code {e.ApplicationExitCode}.");
        base.OnExit(e);
    }
}
