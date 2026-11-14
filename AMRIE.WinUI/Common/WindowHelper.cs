using System;
#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
#endif
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AMRIE.WinUI.Common;

public static class WindowHelper
{
    public static Window? MainWindow { get; set; }

#if WINDOWS
    public static IntPtr GetMainWindowHandle()
    {
        if (MainWindow == null)
            throw new InvalidOperationException("MainWindow is not initialized.");

        return WindowNative.GetWindowHandle(MainWindow);
    }
#endif

    /// <summary>
    /// Associates a WinRT picker or dialog with the main window handle.
    /// On Windows, binds the HWND using WinRT interop; on cross-platform targets (Uno), this is a no-op.
    /// </summary>
    /// <param name="target">The picker or dialog target object.</param>
    /// <exception cref="InvalidOperationException">Thrown if MainWindow has not yet been initialized on Windows.</exception>
    public static void InitializeWithMainWindow(object target)
    {
#if WINDOWS
        InitializeWithWindow.Initialize(target, GetMainWindowHandle());
#endif
    }

    /// <summary>
    /// Safely attempts to associate a WinRT picker or dialog with the main window handle without throwing.
    /// </summary>
    /// <param name="target">The picker or dialog target object.</param>
    /// <returns>True if successfully initialized; false if MainWindow is null.</returns>
    public static bool TryInitializeWithMainWindow(object target)
    {
#if WINDOWS
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
#else
        return true;
#endif
    }

#if WINDOWS
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
#endif

    public static void SetNativeTitleBarTheme(Window window, bool isDark)
    {
#if WINDOWS
        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            int useDark = isDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }
        catch { }
#endif
    }

    public static void SetWindowSize(Window window, int widthDip, int heightDip)
    {
#if WINDOWS
        var hwnd = WindowNative.GetWindowHandle(window);
        var dpi = GetDpiForWindow(hwnd);
        var scale = dpi / 96.0;

        var widthPx = (int)Math.Round(widthDip * scale);
        var heightPx = (int)Math.Round(heightDip * scale);

        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(widthPx, heightPx));
#endif
    }

    public static void RestoreWindowState(Window window, AppSettings settings)
    {
#if WINDOWS
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        int targetW;
        int targetH;
        if (settings.WindowWidth is int w && w >= 600 &&
            settings.WindowHeight is int h && h >= 450)
        {
            targetW = w;
            targetH = h;
        }
        else
        {
            var scale = GetDpiForWindow(hwnd) / 96.0;
            targetW = (int)Math.Round(1150 * scale);
            targetH = (int)Math.Round(780 * scale);
        }

        DisplayArea display = settings.WindowX is int x && settings.WindowY is int y
            ? DisplayArea.GetFromPoint(new PointInt32(x + targetW / 2, y + targetH / 2), DisplayAreaFallback.Primary)
            : DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);

        var work = display.WorkArea;
        int clampedW = Math.Min(targetW, work.Width);
        int clampedH = Math.Min(targetH, work.Height);
        int centeredX = work.X + Math.Max(0, (work.Width - clampedW) / 2);
        int centeredY = work.Y + Math.Max(0, (work.Height - clampedH) / 2);
        appWindow.MoveAndResize(new RectInt32(centeredX, centeredY, clampedW, clampedH));

        if ((settings.IsMaximized ?? true) && appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
#endif
    }

    public static void SaveWindowState(Window window)
    {
#if WINDOWS
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var settings = SettingsManager.LoadSettings();

        bool isMaximized = appWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Maximized;
        settings.IsMaximized = isMaximized;

        if (!isMaximized)
        {
            var pos = appWindow.Position;
            var size = appWindow.Size;
            if (size.Width >= 600 && size.Height >= 450)
            {
                settings.WindowX = pos.X;
                settings.WindowY = pos.Y;
                settings.WindowWidth = size.Width;
                settings.WindowHeight = size.Height;
            }
        }

        SettingsManager.SaveSettings(settings);
#endif
    }

    /// <summary>
    /// Applies the active app theme and XamlRoot to a ContentDialog before it is shown.
    /// </summary>
    public static void ApplyDialogTheme(ContentDialog dialog, FrameworkElement? source = null)
    {
        var xamlRoot = source?.XamlRoot ?? (MainWindow?.Content as UIElement)?.XamlRoot;
        if (xamlRoot != null)
            dialog.XamlRoot = xamlRoot;

        dialog.RequestedTheme = MainWindow is MainWindow mainWindow
            ? mainWindow.CurrentTheme
            : source?.ActualTheme ?? ElementTheme.Default;
    }

    /// <summary>
    /// Wires an inline Close button and Escape key handling for dialogs that omit ContentDialog footer buttons.
    /// </summary>
    public static void ConfigureInlineCloseDialog(ContentDialog dialog, Button closeButton)
    {
        closeButton.Click += (_, _) => dialog.Hide();
        dialog.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                e.Handled = true;
                dialog.Hide();
            }
        };
    }
}
