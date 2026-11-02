using System;
using System.IO;
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

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    private const uint WM_SETICON = 0x0080;
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private const int SM_CXICON = 11;
    private const int SM_CYICON = 12;
    private const int SM_CXSMICON = 49;
    private const int SM_CYSMICON = 50;

    /// <summary>
    /// Sets the Application User Model ID for the process so the Windows Taskbar
    /// treats it as its own distinct application and renders the correct icon even when run via 'dotnet run'.
    /// </summary>
    public static void SetAppUserModelId(string appId = "WHONET.AMRIE.WinUI")
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(appId);
        }
        catch { }
    }

    /// <summary>
    /// Sets both the AppWindow title bar icon and the Win32 HWND WM_SETICON (big and small),
    /// ensuring the icon is visible in the Windows Taskbar, Alt-Tab switcher, and Title Bar.
    /// </summary>
    public static void SetWindowIcon(Window window, string iconPath)
    {
        try
        {
            if (!File.Exists(iconPath)) return;

            // 1. AppWindow icon (Titlebar)
            window.AppWindow.SetIcon(iconPath);

            // 2. Win32 WM_SETICON (Taskbar ICON_BIG and titlebar ICON_SMALL)
            var hwnd = WindowNative.GetWindowHandle(window);
            if (hwnd != IntPtr.Zero)
            {
                int cxBig = GetSystemMetrics(SM_CXICON);
                int cyBig = GetSystemMetrics(SM_CYICON);
                int cxSmall = GetSystemMetrics(SM_CXSMICON);
                int cySmall = GetSystemMetrics(SM_CYSMICON);

                IntPtr hBigIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, cxBig > 0 ? cxBig : 32, cyBig > 0 ? cyBig : 32, LR_LOADFROMFILE);
                IntPtr hSmallIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, cxSmall > 0 ? cxSmall : 16, cySmall > 0 ? cySmall : 16, LR_LOADFROMFILE);

                if (hBigIcon != IntPtr.Zero)
                {
                    SendMessage(hwnd, WM_SETICON, (IntPtr)1 /* ICON_BIG */, hBigIcon);
                }
                if (hSmallIcon != IntPtr.Zero)
                {
                    SendMessage(hwnd, WM_SETICON, (IntPtr)0 /* ICON_SMALL */, hSmallIcon);
                }
            }
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
