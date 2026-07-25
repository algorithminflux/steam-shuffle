using SteamShuffle.CoreModels;
using SteamShuffle.Infrastructure;
using Xunit;

namespace SteamShuffle.Tests.Infrastructure;

public class CollectionRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly CollectionRepository _repo;

    public CollectionRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"steamshuffle-test-{Guid.NewGuid():N}.db");
        _repo = new CollectionRepository(_dbPath);
    }

    public void Dispose()
    {
        // SQLite keeps a lock on the file briefly after the last connection closes;
        // best-effort cleanup so temp files don't pile up, but don't fail the test over it.
        try { File.Delete(_dbPath); } catch { /* ignore */ }
    }

    [Fact]
    public void UpsertGames_NewGame_IsStoredWithGivenFlags()
    {
        _repo.UpsertGames(new[]
        {
            new SteamGame { AppId = 100, Name = "Hades", IsOwned = true, PlaytimeForeverMinutes = 120 }
        });

        var game = Assert.Single(_repo.GetAllGames());
        Assert.Equal("Hades", game.Name);
        Assert.True(game.IsOwned);
        Assert.False(game.IsWishlisted);
        Assert.Equal(120, game.PlaytimeForeverMinutes);
    }

    [Fact]
    public void UpsertGames_SameAppIdOwnedThenWishlisted_MergesFlagsInsteadOfOverwriting()
    {
        _repo.UpsertGames(new[] { new SteamGame { AppId = 200, Name = "Celeste", IsOwned = true } });
        _repo.UpsertGames(new[] { new SteamGame { AppId = 200, Name = "Celeste", IsWishlisted = true } });

        var game = Assert.Single(_repo.GetAllGames());
        Assert.True(game.IsOwned, "Owned flag should not be lost when re-upserted as wishlisted.");
        Assert.True(game.IsWishlisted);
    }

    [Fact]
    public void UpsertGames_LaterUpsertWithLowerPlaytime_KeepsHigherPlaytime()
    {
        _repo.UpsertGames(new[] { new SteamGame { AppId = 300, Name = "Deep Rock Galactic", PlaytimeForeverMinutes = 500 } });
        _repo.UpsertGames(new[] { new SteamGame { AppId = 300, Name = "Deep Rock Galactic", PlaytimeForeverMinutes = 10 } });

        var game = Assert.Single(_repo.GetAllGames());
        Assert.Equal(500, game.PlaytimeForeverMinutes);
    }

    [Fact]
    public void UpsertGames_NullLastPlayedDoesNotClobberExistingValue()
    {
        var timestamp = DateTimeOffset.UtcNow.AddDays(-2);
        _repo.UpsertGames(new[] { new SteamGame { AppId = 400, Name = "Portal 2", LastPlayed = timestamp } });
        _repo.UpsertGames(new[] { new SteamGame { AppId = 400, Name = "Portal 2", LastPlayed = null } });

        var game = Assert.Single(_repo.GetAllGames());
        Assert.NotNull(game.LastPlayed);
        Assert.Equal(timestamp.ToUnixTimeSeconds(), game.LastPlayed!.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public void SaveStoreDetails_RoundTripsPriceGenresAndTags()
    {
        _repo.UpsertGames(new[] { new SteamGame { AppId = 500, Name = "Stardew Valley" } });

        _repo.SaveStoreDetails(new StoreDetails
        {
            AppId = 500,
            HeaderImageUrl = "https://example.com/header.jpg",
            IsFree = false,
            PriceCad = 16.99m,
            Genres = new() { "Simulation", "RPG" },
            Tags = new() { "Farming Sim", "Multiplayer" },
        });

        var game = Assert.Single(_repo.GetAllGames());
        Assert.Equal(16.99m, game.PriceCad);
        Assert.Equal(new[] { "Simulation", "RPG" }, game.Genres);
        Assert.Equal(new[] { "Farming Sim", "Multiplayer" }, game.Tags);
    }

    [Fact]
    public void GetAppIdsNeedingStoreRefresh_ExcludesRecentlyFetchedGames()
    {
        _repo.UpsertGames(new[]
        {
            new SteamGame { AppId = 600, Name = "Never fetched" },
            new SteamGame { AppId = 601, Name = "Freshly fetched" },
        });
        _repo.SaveStoreDetails(new StoreDetails { AppId = 601 });

        var stale = _repo.GetAppIdsNeedingStoreRefresh(TimeSpan.FromDays(3));

        Assert.Contains(600, stale);
        Assert.DoesNotContain(601, stale);
    }

    [Fact]
    public void CreateCollection_AddAndRemoveGame_UpdatesMembership()
    {
        _repo.UpsertGames(new[] { new SteamGame { AppId = 700, Name = "Balatro", IsOwned = true } });
        var collection = _repo.CreateCollection("Cozy Nights");

        _repo.AddGameToCollection(collection.Id, 700);
        var withMember = _repo.GetCollections().Single(c => c.Id == collection.Id);
        Assert.Contains(700, withMember.AppIds);

        _repo.RemoveGameFromCollection(collection.Id, 700);
        var withoutMember = _repo.GetCollections().Single(c => c.Id == collection.Id);
        Assert.DoesNotContain(700, withoutMember.AppIds);
    }

    [Fact]
    public void DeleteCollection_RemovesItAndItsMemberships()
    {
        var collection = _repo.CreateCollection("Temp Collection");
        _repo.UpsertGames(new[] { new SteamGame { AppId = 800, Name = "Hollow Knight" } });
        _repo.AddGameToCollection(collection.Id, 800);

        _repo.DeleteCollection(collection.Id);

        Assert.DoesNotContain(_repo.GetCollections(), c => c.Id == collection.Id);
    }

    [Fact]
    public void AddGameToCollection_CalledTwice_DoesNotDuplicateMembership()
    {
        var collection = _repo.CreateCollection("Dupe Test");
        _repo.UpsertGames(new[] { new SteamGame { AppId = 900, Name = "Vampire Survivors" } });

        _repo.AddGameToCollection(collection.Id, 900);
        _repo.AddGameToCollection(collection.Id, 900);

        var refreshed = _repo.GetCollections().Single(c => c.Id == collection.Id);
        Assert.Single(refreshed.AppIds);
    }
}