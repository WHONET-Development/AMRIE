using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace AMRIE.WinUI.Common;

public static class WindowHelper
{
    public static Window? MainWindow { get; set; }

    public static IntPtr GetMainWindowHandle()
    {
        if (MainWindow == null)
            throw new InvalidOperationException("MainWindow is not initialized.");

        return WindowNative.GetWindowHandle(MainWindow);
    }

    /// <summary>
    /// Associates a WinRT picker or dialog with the main window handle.
    /// </summary>
    /// <param name="target">The picker or dialog target object.</param>
    /// <exception cref="InvalidOperationException">Thrown if MainWindow has not yet been initialized.</exception>
    public static void InitializeWithMainWindow(object target)
    {
        InitializeWithWindow.Initialize(target, GetMainWindowHandle());
    }

    /// <summary>
    /// Safely attempts to associate a WinRT picker or dialog with the main window handle without throwing.
    /// </summary>
    /// <param name="target">The picker or dialog target object.</param>
    /// <returns>True if successfully initialized; false if MainWindow is null.</returns>
    public static bool TryInitializeWithMainWindow(object target)
    {
        try
        {
            if (MainWindow == null) return false;
            InitializeWithWindow.Initialize(target, WindowNative.GetWindowHandle(MainWindow));
            return true;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    public static void SetWindowSize(Window window, int widthDip, int heightDip)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var dpi = GetDpiForWindow(hwnd);
        var scale = dpi / 96.0;

        var widthPx = (int)Math.Round(widthDip * scale);
        var heightPx = (int)Math.Round(heightDip * scale);

        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(widthPx, heightPx));
    }
}
