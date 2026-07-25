namespace SteamShuffle.CoreModels;

/// <summary>
/// Persistence boundary for the local games/collections cache. Lives in CoreModels
/// (rather than being referenced directly from Infrastructure) so Services can
/// depend on the abstraction without taking a dependency on the concrete storage
/// mechanism.
/// </summary>
public interface ICollectionRepository
{
    /// <summary>
    /// Inserts or merges games into the local cache. If a game already exists,
    /// ownership/wishlist flags are OR'd together rather than overwritten, so a
    /// game already tagged Owned doesn't lose that status during a wishlist-only
    /// refresh (and vice versa).
    /// </summary>
    void UpsertGames(IEnumerable<SteamGame> games);

    void SaveStoreDetails(StoreDetails details);

    List<SteamGame> GetAllGames();

    /// <summary>Games whose store metadata is missing or older than <paramref name="maxAge"/>.</summary>
    List<int> GetAppIdsNeedingStoreRefresh(TimeSpan maxAge);

    List<GameCollection> GetCollections();

    GameCollection CreateCollection(string name);

    void RenameCollection(int collectionId, string newName);

    void DeleteCollection(int collectionId);

    void AddGameToCollection(int collectionId, int appId);

    void RemoveGameFromCollection(int collectionId, int appId);
}
