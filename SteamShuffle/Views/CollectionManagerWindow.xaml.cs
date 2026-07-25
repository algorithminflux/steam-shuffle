using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SteamShuffle.CoreModels;

namespace SteamShuffle.Views;

public class GameSelectionItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public SteamGame Game { get; }
    public Visibility WishlistBadgeVisibility => Game.IsWishlisted ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ManualBadgeVisibility => Game.IsManual ? Visibility.Visible : Visibility.Collapsed;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GameSelectionItem(SteamGame game, bool isSelected)
    {
        Game = game;
        _isSelected = isSelected;
    }
}

public partial class CollectionManagerWindow : Window
{
    private readonly ICollectionRepository _repo;
    private readonly GameCollection _collection;
    private readonly List<GameSelectionItem> _allItems;

    public CollectionManagerWindow(ICollectionRepository repo, GameCollection collection, IEnumerable<SteamGame> allGames)
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

        ApplyFilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void SelectedOnlyCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void MembershipCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: GameSelectionItem item })
        {
            return;
        }

        if (item.IsSelected)
        {
            _repo.AddGameToCollection(_collection.Id, item.Game.AppId);
        }
        else
        {
            _repo.RemoveGameFromCollection(_collection.Id, item.Game.AppId);
        }
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text.Trim();
        IEnumerable<GameSelectionItem> items = _allItems;

        if (SelectedOnlyCheckBox.IsChecked == true)
        {
            items = items.Where(i => i.IsSelected);
        }

        if (!string.IsNullOrEmpty(query))
        {
            items = items.Where(i => i.Game.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase));
        }

        GamesList.ItemsSource = items.ToList();
    }
}