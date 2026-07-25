namespace SteamShuffle.CoreModels;

/// <summary>
/// A user-managed collection (e.g. "Cozy", "Co-op", "Backlog").
/// Membership is stored as a simple list of AppIds in the local SQLite store.
/// </summary>
public class GameCollection
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<int> AppIds { get; set; } = new();

    public override string ToString() => Name;
}