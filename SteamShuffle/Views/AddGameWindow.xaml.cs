using System.Windows;
using System.Windows.Input;
using SteamShuffle.ApiClients;
using SteamShuffle.CoreModels;

namespace SteamShuffle.Views;

public partial class AddGameWindow
{
    private readonly SteamStoreService _storeService;

    public StoreSearchResult? SelectedResult { get; private set; }

    public AddGameWindow(SteamStoreService storeService)
    {
        InitializeComponent();
        _storeService = storeService;
        Loaded += (_, _) => SearchBox.Focus();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = RunSearchAsync();
        }
    }

    // ReSharper disable once AsyncVoidEventHandlerMethod
    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        await RunSearchAsync();
    }

    private async Task RunSearchAsync()
    {
        var term = SearchBox.Text.Trim();
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
            MessageBox.Show(this, ex.Message, "Search failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }

    private void ResultsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        AddButton.IsEnabled = ResultsList.SelectedItem is StoreSearchResult;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is not StoreSearchResult selected)
        {
            return;
        }

        SelectedResult = selected;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
