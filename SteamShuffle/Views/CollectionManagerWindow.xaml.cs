using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SteamShuffle.Models;
using SteamShuffle.Services;

namespace SteamShuffle.Views;

public class GameSelectionItem : INotifyPropertyChanged
{
    public SteamGame Game { get; }
    public Visibility WishlistBadgeVisibility => Game.IsWishlisted ? Visibility.Visible : Visibility.Collapsed;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public GameSelectionItem(SteamGame game, bool isSelected)
    {
        Game = game;
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class CollectionManagerWindow : Window
{
    private readonly CollectionRepository _repo;
    private readonly GameCollection _collection;
    private readonly List<GameSelectionItem> _allItems;

    public CollectionManagerWindow(CollectionRepository repo, GameCollection collection, IEnumerable<SteamGame> allGames)
    {
        InitializeComponent();
        _repo = repo;
        _collection = collection;

        TitleText.Text = $"Manage \"{collection.Name}\"";

        var memberIds = new HashSet<int>(collection.AppIds);
        _allItems = allGames
            .OrderBy(g => g.Name)
            .Select(g => new GameSelectionItem(g, memberIds.Contains(g.AppId)))
            .ToList();

        GamesList.ItemsSource = _allItems;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        GamesList.ItemsSource = string.IsNullOrEmpty(query)
            ? _allItems
            : _allItems.Where(i => i.Game.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void MembershipCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: GameSelectionItem item })
            return;

        if (item.IsSelected)
            _repo.AddGameToCollection(_collection.Id, item.Game.AppId);
        else
            _repo.RemoveGameFromCollection(_collection.Id, item.Game.AppId);
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}