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

        // Configure system root path for Interpretation Engine tables and resources
        string baseDir = AppContext.BaseDirectory;
        if (!baseDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            baseDir += Path.DirectorySeparatorChar;
        }
        Constants.SystemRootPath = baseDir;
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
