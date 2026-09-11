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

    public static void InitializeWithMainWindow(object target)
    {
        InitializeWithWindow.Initialize(target, GetMainWindowHandle());
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
