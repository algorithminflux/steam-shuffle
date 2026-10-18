using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SteamShuffle.CoreModels;
using SteamShuffle.Infrastructure;

namespace SteamShuffle.Views;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; private set; }

    public SettingsWindow()
    {
        InitializeComponent();
        Settings = new AppSettings();
    }

    public SettingsWindow(AppSettings settings) : this()
    {
        Settings = settings;
        ApiKeyBox.Text = settings.SteamApiKey;
        SteamIdBox.Text = settings.SteamId64;
        CountryCodeBox.Text = settings.CountryCode;
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        var apiKey = (ApiKeyBox.Text ?? string.Empty).Trim();
        var steamId = (SteamIdBox.Text ?? string.Empty).Trim();
        var countryCode = (CountryCodeBox.Text ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(steamId))
        {
            ValidationText.Text = "Both an API key and a SteamID64 are required.";
            ValidationText.IsVisible = true;
            return;
        }

        if (steamId.Length != 17 || !ulong.TryParse(steamId, out _))
        {
            ValidationText.Text = "SteamID64 should be a 17-digit number (e.g. 76561198000000000).";
            ValidationText.IsVisible = true;
            return;
        }

        Settings.SteamApiKey = apiKey;
        Settings.SteamId64 = steamId;
        Settings.CountryCode = string.IsNullOrWhiteSpace(countryCode) ? "ca" : countryCode.ToLowerInvariant();
        AppSettingsStore.Save(Settings);

        Close(true);
    }

    private void Link_Click(object? sender, RoutedEventArgs e)
    {
        // Only ever open the https URLs hard-coded in the XAML Tag.
        if (sender is not Button { Tag: string { Length: > 0 } url } || !url.StartsWith("https://", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            ValidationText.Text = $"Couldn't open the browser. Open this address yourself: {url}";
            ValidationText.IsVisible = true;
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
