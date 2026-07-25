using System.IO;
using System.Text.Json;

namespace SteamShuffle.Services;

public class AppSettings
{
    public string SteamApiKey { get; set; } = string.Empty;
    public string SteamId64 { get; set; } = string.Empty;

    /// <summary>Steam "cc" country code used for store pricing. "ca" = Canada / CAD.</summary>
    public string CountryCode { get; set; } = "ca";

    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamShuffle",
            "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // Corrupt or unreadable settings file -> fall through to defaults.
        }

        return new AppSettings();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SteamApiKey) && !string.IsNullOrWhiteSpace(SteamId64);
}