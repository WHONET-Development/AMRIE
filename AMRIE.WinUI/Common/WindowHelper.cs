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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static void SetNativeTitleBarTheme(Window window, bool isDark)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            int useDark = isDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }
        catch { }
    }

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
