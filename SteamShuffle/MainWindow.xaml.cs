using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Windows;
using SteamShuffle.Models;
using SteamShuffle.Services;
using SteamShuffle.Views;

namespace SteamShuffle
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _http = new();
        private readonly CollectionRepository _repo = new();
        private readonly LibraryManager _library;
        private AppSettings _settings;

        private List<SteamGame> _allGames = new();
        private readonly ObservableCollection<GameCollection> _collections = new();
        private readonly Random _rng = new();

        public MainWindow()
        {
            InitializeComponent();

            _settings = AppSettings.Load();
            _library = new LibraryManager(_repo, _http);

            CollectionsList.ItemsSource = _collections;
            _allGames = _repo.GetCachedLibrary();
            ReloadCollections();

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_settings.IsConfigured)
            {
                StatusText.Text = "Welcome! Connect your Steam account to get started.";
                OpenSettings();
            }

            if (_settings.IsConfigured && _allGames.Count == 0)
            {
                await SyncLibraryAsync();
            }
            else
            {
                StatusText.Text = $"{_allGames.Count} games cached locally. Hit \"Sync Library\" any time to refresh.";
            }
        }

        private void ReloadCollections()
        {
            _collections.Clear();
            foreach (var c in _repo.GetCollections())
                _collections.Add(c);
        }

        private GameCollection? SelectedCollection => CollectionsList.SelectedItem as GameCollection;

        private void CollectionsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ResultCard.Visibility = Visibility.Collapsed;
        }

        private void NewCollection_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SimplePromptWindow("New Collection", "Collection name:") { Owner = this };
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                var created = _repo.CreateCollection(dialog.ResponseText.Trim());
                _collections.Add(created);
                CollectionsList.SelectedItem = created;
            }
        }

        private void ManageCollection_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCollection is null)
            {
                MessageBox.Show(this, "Select a collection first.", "Steam Shuffle", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_allGames.Count == 0)
            {
                MessageBox.Show(this, "Sync your library first so there are games to add.", "Steam Shuffle", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var manager = new CollectionManagerWindow(_repo, SelectedCollection, _allGames) { Owner = this };
            manager.ShowDialog();

            // Refresh the AppIds/count shown in the sidebar for this collection.
            var refreshed = _repo.GetCollections();
            _collections.Clear();
            foreach (var c in refreshed)
                _collections.Add(c);
        }

        private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

        private void OpenSettings()
        {
            var dialog = new SettingsWindow(_settings) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _settings = dialog.Settings;
            }
        }

        private async void Sync_Click(object sender, RoutedEventArgs e)
        {
            if (!_settings.IsConfigured)
            {
                OpenSettings();
                if (!_settings.IsConfigured) return;
            }

            await SyncLibraryAsync();
        }

        private async System.Threading.Tasks.Task SyncLibraryAsync()
        {
            SpinButton.IsEnabled = false;
            try
            {
                var progress = new Progress<LibrarySyncProgress>(p =>
                {
                    StatusText.Text = p.Total > 0 ? $"{p.Message} ({p.Completed}/{p.Total})" : p.Message;
                });

                _allGames = await _library.SyncAsync(_settings, progress);
                StatusText.Text = $"Synced. {_allGames.Count} games available across owned + wishlist.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Sync failed.";
                MessageBox.Show(this, ex.Message, "Sync failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SpinButton.IsEnabled = true;
            }
        }

        private void Spin_Click(object sender, RoutedEventArgs e)
        {
            var collection = SelectedCollection;
            if (collection is null)
            {
                MessageBox.Show(this, "Select a collection to spin from.", "Steam Shuffle", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var pool = _allGames.Where(g => collection.AppIds.Contains(g.AppId)).ToList();
            if (pool.Count == 0)
            {
                MessageBox.Show(this, "This collection is empty. Add some games first via \"Manage\".", "Steam Shuffle", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ResultCard.Visibility = Visibility.Collapsed;
            SpinButton.IsEnabled = false;

            var winner = pool[_rng.Next(pool.Count)];
            Reel.SpinCompleted += OnSpinCompletedOnce;
            Reel.Spin(pool, winner);

            void OnSpinCompletedOnce(object? s, SteamGame result)
            {
                Reel.SpinCompleted -= OnSpinCompletedOnce;
                ShowResult(result);
                SpinButton.IsEnabled = true;
            }
        }

        private void ShowResult(SteamGame game)
        {
            ResultTitle.Text = game.Name;
            ResultSourceBadge.Text = game.SourceBadge;
            ResultPrice.Text = game.PriceDisplay;
            ResultGenre.Text = game.Genres.Count > 0 ? string.Join(", ", game.Genres) : "Unknown";
            ResultPlaytime.Text = game.PlaytimeDisplay;
            ResultLastPlayed.Text = game.LastPlayedDisplay;
            ResultTags.ItemsSource = game.Tags;

            try
            {
                ResultImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(game.CapsuleImageUrl));
            }
            catch
            {
                // Leave blank if art fails to load.
            }

            ResultCard.Visibility = Visibility.Visible;
        }
    }
}
