using System;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Xaml;

namespace AMRIE.WinUI.Common;

public sealed class AppSettings
{
    public string? Theme { get; set; }
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public int? WindowWidth { get; set; }
    public int? WindowHeight { get; set; }
    public bool? IsMaximized { get; set; }
}

/// <summary>
/// Lightweight settings persistence for unpackaged WinUI 3 applications.
/// Stores configuration in %LOCALAPPDATA%\AMRIE\settings.json.
/// </summary>
public static class SettingsManager
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMRIE");
    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public static void SaveSettings(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }

    public static ElementTheme LoadTheme()
    {
        var settings = LoadSettings();
        if (!string.IsNullOrEmpty(settings.Theme) && Enum.TryParse<ElementTheme>(settings.Theme, out var theme))
        {
            return theme;
        }
        return ElementTheme.Default;
    }

    public static void SaveTheme(ElementTheme theme)
    {
        var settings = LoadSettings();
        settings.Theme = theme.ToString();
        SaveSettings(settings);
    }
}
