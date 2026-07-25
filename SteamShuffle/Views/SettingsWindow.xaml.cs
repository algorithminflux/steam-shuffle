using System.Windows;
using SteamShuffle.Services;

namespace SteamShuffle.Views
{
    public partial class SettingsWindow : Window
    {
        public AppSettings Settings { get; private set; }
        public bool Saved { get; private set; }

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            Settings = settings;

            ApiKeyBox.Text = settings.SteamApiKey;
            SteamIdBox.Text = settings.SteamId64;
            CountryCodeBox.Text = settings.CountryCode;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var apiKey = ApiKeyBox.Text.Trim();
            var steamId = SteamIdBox.Text.Trim();
            var countryCode = CountryCodeBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(steamId))
            {
                ValidationText.Text = "Both an API key and a SteamID64 are required.";
                ValidationText.Visibility = Visibility.Visible;
                return;
            }

            if (steamId.Length != 17 || !ulong.TryParse(steamId, out _))
            {
                ValidationText.Text = "SteamID64 should be a 17-digit number (e.g. 76561198000000000).";
                ValidationText.Visibility = Visibility.Visible;
                return;
            }

            Settings.SteamApiKey = apiKey;
            Settings.SteamId64 = steamId;
            Settings.CountryCode = string.IsNullOrWhiteSpace(countryCode) ? "ca" : countryCode.ToLowerInvariant();
            Settings.Save();

            Saved = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
