using System.Collections.ObjectModel;
using System.Net.Http;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using SteamShuffle.ApiClients;
using SteamShuffle.CoreModels;
using SteamShuffle.Infrastructure;
using SteamShuffle.Services;

namespace SteamShuffle.Views;

public partial class MainWindow : Window
{
    private readonly HttpClient _http = new();
    private readonly ICollectionRepository _repo = new CollectionRepository();
    private readonly LibraryManager _library;
    private AppSettings _settings;

    private List<SteamGame> _allGames;
    private readonly ObservableCollection<GameCollection> _collections = [];
    private readonly Random _rng = new();

    private GameCollection? SelectedCollection => CollectionsList.SelectedItem as GameCollection;

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettingsStore.Load();
        _library = new LibraryManager(_repo, _http);

        CollectionsList.ItemsSource = _collections;
        _allGames = _repo.GetAllGames();
        ReloadCollections();

        // WPF used the Loaded event for post-construction async work; Avalonia's
        // equivalent is Opened (Loaded also exists but fires on every visual-tree
        // attach, which can be more often than wanted for one-time startup logic).
        Opened += async (_, _) => await OnWindowOpenedAsync();
    }

    private async Task OnWindowOpenedAsync()
    {
        if (!_settings.IsConfigured)
        {
            StatusText.Text = "Welcome! Connect your Steam account to get started.";
            await OpenSettingsAsync();
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

    private void CollectionsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ResultCard.IsVisible = false;
    }

    private async void NewCollection_Click(object? sender, RoutedEventArgs e)
    {
        var name = await SimplePromptWindow.AskAsync(this, "New Collection", "Collection name:");
        if (name is null)
        {
            return;
        }

        var created = _repo.CreateCollection(name);
        _collections.Add(created);
        CollectionsList.SelectedItem = created;
    }

    private async void ManageCollection_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedCollection is null)
        {
            await MessageWindow.ShowAsync(this, "Steam Shuffle", "Select a collection first.");
            return;
        }

        if (_allGames.Count == 0)
        {
            await MessageWindow.ShowAsync(this, "Steam Shuffle", "Sync your library first so there are games to add.");
            return;
        }

        var manager = new CollectionManagerWindow(_repo, SelectedCollection, _allGames);
        await manager.ShowDialog(this);

        // Refresh the AppIds/count shown in the sidebar for this collection.
        ReloadCollections();
    }

    private async void AddGame_Click(object? sender, RoutedEventArgs e)
    {
        var storeService = new SteamStoreService(_http, _settings.CountryCode);
        var dialog = new AddGameWindow(storeService);

        var added = await dialog.ShowDialog<bool>(this);
        if (!added || dialog.SelectedResult is not { } selected)
        {
            return;
        }

        StatusText.Text = "Adding game...";
        try
        {
            var details = await storeService.GetAppDetailsAsync(selected.AppId);
            var name = details?.Name ?? selected.Name;

            _repo.UpsertGames([new SteamGame { AppId = selected.AppId, Name = name, IsOwned = true, IsManual = true }]);
            if (details is not null)
            {
                _repo.SaveStoreDetails(details);
            }

            _allGames = _repo.GetAllGames();
            StatusText.Text = $"Added \"{name}\". Use \"Manage\" to add it to a collection.";
        }
        catch (Exception ex)
        {
            await MessageWindow.ShowAsync(this, "Add game failed", ex.Message);
            StatusText.Text = $"{_allGames.Count} games cached locally.";
        }
    }

    private async void Settings_Click(object? sender, RoutedEventArgs e) => await OpenSettingsAsync();

    private async Task OpenSettingsAsync()
    {
        var dialog = new SettingsWindow(_settings);
        var saved = await dialog.ShowDialog<bool>(this);
        if (saved)
        {
            _settings = dialog.Settings;
        }
    }

    private async void Sync_Click(object? sender, RoutedEventArgs e)
    {
        if (!_settings.IsConfigured)
        {
            await OpenSettingsAsync();
            if (!_settings.IsConfigured)
            {
                return;
            }
        }

        await SyncLibraryAsync();
    }

    private async Task SyncLibraryAsync()
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
            await MessageWindow.ShowAsync(this, "Sync failed", ex.Message);
        }
        finally
        {
            SpinButton.IsEnabled = true;
        }
    }

    private async void Spin_Click(object? sender, RoutedEventArgs e)
    {
        var collection = SelectedCollection;
        if (collection is null)
        {
            await MessageWindow.ShowAsync(this, "Steam Shuffle", "Select a collection to spin from.");
            return;
        }

        var pool = _allGames.Where(g => collection.AppIds.Contains(g.AppId)).ToList();
        if (pool.Count == 0)
        {
            await MessageWindow.ShowAsync(this, "Steam Shuffle", "This collection is empty. Add some games first via \"Manage\".");
            return;
        }

        ResultCard.IsVisible = false;
        SpinButton.IsEnabled = false;

        var winner = pool[_rng.Next(pool.Count)];

        // Cleanest improvement from the WPF port: no SpinCompleted event
        // subscribe/unsubscribe dance -- just await the spin directly.
        var result = await Reel.SpinAsync(pool, winner);
        ShowResult(result);

        SpinButton.IsEnabled = true;
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

        _ = LoadResultImageAsync(game.CapsuleImageUrl);

        ResultCard.IsVisible = true;
    }

    private async Task LoadResultImageAsync(string url)
    {
        try
        {
            using var stream = await _http.GetStreamAsync(url);
            ResultImage.Source = new Bitmap(stream);
        }
        catch
        {
            // Leave blank if art fails to load.
        }
    }
}
