using System;
using System.IO;
using AMR_Engine;
using Microsoft.UI.Xaml;

namespace AMRIE.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();

        UnhandledException += App_UnhandledException;

        // Configure system root path for Interpretation Engine tables and resources
        string baseDir = AppContext.BaseDirectory;
        if (!baseDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            baseDir += Path.DirectorySeparatorChar;
        }
        Constants.SystemRootPath = baseDir;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[AMRIE UnhandledException] {e.Message}");
        System.Diagnostics.Debug.WriteLine(e.Exception);
        e.Handled = true;
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
