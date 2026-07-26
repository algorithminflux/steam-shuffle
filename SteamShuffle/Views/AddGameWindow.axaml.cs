using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SteamShuffle.ApiClients;
using SteamShuffle.CoreModels;

namespace SteamShuffle.Views;

public partial class AddGameWindow : Window
{
    private readonly SteamStoreService _storeService;

    public StoreSearchResult? SelectedResult { get; private set; }

    public AddGameWindow(SteamStoreService storeService)
    {
        InitializeComponent();
        _storeService = storeService;
        Opened += (_, _) => SearchBox.Focus();
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = RunSearchAsync();
        }
    }

    private async void Search_Click(object? sender, RoutedEventArgs e)
    {
        await RunSearchAsync();
    }

    private async Task RunSearchAsync()
    {
        var term = (SearchBox.Text ?? string.Empty).Trim();
        if (term.Length == 0)
        {
            return;
        }

        SearchButton.IsEnabled = false;
        AddButton.IsEnabled = false;
        StatusText.Text = "Searching...";
        ResultsList.ItemsSource = null;

        try
        {
            var results = await _storeService.SearchAsync(term);
            ResultsList.ItemsSource = results;
            StatusText.Text = results.Count == 0 ? "No matches found." : string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Search failed.";
            await MessageWindow.ShowAsync(this, "Search failed", ex.Message);
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }

    private void ResultsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        AddButton.IsEnabled = ResultsList.SelectedItem is StoreSearchResult;
    }

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is not StoreSearchResult selected)
        {
            return;
        }

        SelectedResult = selected;
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
