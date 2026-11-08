using System;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Xaml;

namespace AMRIE.WinUI.Common;

/// <summary>
/// Lightweight settings persistence for unpackaged WinUI 3 applications.
/// Stores configuration in %LOCALAPPDATA%\AMRIE\settings.json.
/// </summary>
public static class SettingsManager
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMRIE");
    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

    public static ElementTheme LoadTheme()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Theme", out var prop))
                {
                    string? themeStr = prop.GetString();
                    if (!string.IsNullOrEmpty(themeStr) && Enum.TryParse<ElementTheme>(themeStr, out var theme))
                    {
                        return theme;
                    }
                }
            }
        }
        catch { }
        return ElementTheme.Default;
    }

    public static void SaveTheme(ElementTheme theme)
    {
        try
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }
            string json = JsonSerializer.Serialize(new { Theme = theme.ToString() });
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }
}
