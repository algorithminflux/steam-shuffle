using System.IO;
using System.Text.Json;
using SteamShuffle.CoreModels;

namespace SteamShuffle.Infrastructure;

/// <summary>
/// Persists <see cref="AppSettings"/> as JSON in %AppData%\SteamShuffle. Kept
/// separate from the AppSettings data model so the model itself stays a plain
/// CoreModels type with no file-system dependency.
/// </summary>
public static class AppSettingsStore
{
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
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
            // Corrupt or unreadable settings file -> fall through to defaults.
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
