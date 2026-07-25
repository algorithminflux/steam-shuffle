using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SteamShuffle.Models;

namespace SteamShuffle.Services;

/// <summary>
/// Local SQLite store: a cache of merged game data (owned + wishlist + store
/// metadata) and the user's own collections. This is the only "database" the
/// app depends on — nothing here relies on Steam's undocumented local files.
/// </summary>
public class CollectionRepository
{
    private readonly string _connectionString;

    public CollectionRepository(string? dbPathOverride = null)
    {
        string dbPath;
        if (dbPathOverride is not null)
        {
            dbPath = dbPathOverride;
        }
        else
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SteamShuffle");
            Directory.CreateDirectory(dir);
            dbPath = Path.Combine(dir, "library.db");
        }

        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void Initialize()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS games (
                              app_id INTEGER PRIMARY KEY,
                              name TEXT NOT NULL,
                              playtime_minutes INTEGER NOT NULL DEFAULT 0,
                              last_played_unix INTEGER NULL,
                              is_owned INTEGER NOT NULL DEFAULT 0,
                              is_wishlisted INTEGER NOT NULL DEFAULT 0,
                              header_image_url TEXT NULL,
                              is_free INTEGER NOT NULL DEFAULT 0,
                              price_cad REAL NULL,
                              genres_json TEXT NULL,
                              tags_json TEXT NULL,
                              store_fetched_at_unix INTEGER NULL
                          );

                          CREATE TABLE IF NOT EXISTS collections (
                              id INTEGER PRIMARY KEY AUTOINCREMENT,
                              name TEXT NOT NULL UNIQUE
                          );

                          CREATE TABLE IF NOT EXISTS collection_games (
                              collection_id INTEGER NOT NULL REFERENCES collections(id) ON DELETE CASCADE,
                              app_id INTEGER NOT NULL,
                              PRIMARY KEY (collection_id, app_id)
                          );
                          """;
        cmd.ExecuteNonQuery();
    }

    // ---------- Games cache ----------

    /// <summary>
    /// Inserts or merges games into the local cache. If a game already exists,
    /// ownership/wishlist flags are OR'd together rather than overwritten, so a
    /// game already tagged Owned doesn't lose that status during a wishlist-only
    /// refresh (and vice versa).
    /// </summary>
    public void UpsertGames(IEnumerable<SteamGame> games)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();

        foreach (var game in games)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                              INSERT INTO games (app_id, name, playtime_minutes, last_played_unix, is_owned, is_wishlisted)
                              VALUES ($appId, $name, $playtime, $lastPlayed, $isOwned, $isWishlisted)
                              ON CONFLICT(app_id) DO UPDATE SET
                                  name = excluded.name,
                                  playtime_minutes = MAX(games.playtime_minutes, excluded.playtime_minutes),
                                  last_played_unix = COALESCE(excluded.last_played_unix, games.last_played_unix),
                                  is_owned = MAX(games.is_owned, excluded.is_owned),
                                  is_wishlisted = MAX(games.is_wishlisted, excluded.is_wishlisted);
                              """;
            cmd.Parameters.AddWithValue("$appId", game.AppId);
            cmd.Parameters.AddWithValue("$name", game.Name);
            cmd.Parameters.AddWithValue("$playtime", game.PlaytimeForeverMinutes);
            cmd.Parameters.AddWithValue("$lastPlayed", (object?)game.LastPlayed?.ToUnixTimeSeconds() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$isOwned", game.IsOwned ? 1 : 0);
            cmd.Parameters.AddWithValue("$isWishlisted", game.IsWishlisted ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public void SaveStoreDetails(StoreDetails details)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          UPDATE games SET
                              header_image_url = $header,
                              is_free = $isFree,
                              price_cad = $price,
                              genres_json = $genres,
                              tags_json = $tags,
                              store_fetched_at_unix = $fetchedAt
                          WHERE app_id = $appId;
                          """;
        cmd.Parameters.AddWithValue("$header", (object?)details.HeaderImageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$isFree", details.IsFree ? 1 : 0);
        cmd.Parameters.AddWithValue("$price", (object?)details.PriceCad ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$genres", JsonSerializer.Serialize(details.Genres));
        cmd.Parameters.AddWithValue("$tags", JsonSerializer.Serialize(details.Tags));
        cmd.Parameters.AddWithValue("$fetchedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$appId", details.AppId);
        cmd.ExecuteNonQuery();
    }

    public List<SteamGame> GetAllGames()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM games ORDER BY name;";
        using var reader = cmd.ExecuteReader();

        var games = new List<SteamGame>();
        while (reader.Read())
            games.Add(ReadGame(reader));

        return games;
    }

    /// <summary>Games whose store metadata is missing or older than <paramref name="maxAge"/>.</summary>
    public List<int> GetAppIdsNeedingStoreRefresh(TimeSpan maxAge)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(maxAge).ToUnixTimeSeconds();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT app_id FROM games WHERE store_fetched_at_unix IS NULL OR store_fetched_at_unix < $cutoff;";
        cmd.Parameters.AddWithValue("$cutoff", cutoff);
        using var reader = cmd.ExecuteReader();

        var ids = new List<int>();
        while (reader.Read())
            ids.Add(reader.GetInt32(0));
        return ids;
    }

    private static SteamGame ReadGame(SqliteDataReader reader)
    {
        var game = new SteamGame
        {
            AppId = reader.GetInt32(reader.GetOrdinal("app_id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            PlaytimeForeverMinutes = reader.GetInt32(reader.GetOrdinal("playtime_minutes")),
            IsOwned = reader.GetInt32(reader.GetOrdinal("is_owned")) == 1,
            IsWishlisted = reader.GetInt32(reader.GetOrdinal("is_wishlisted")) == 1,
        };

        var lastPlayedOrdinal = reader.GetOrdinal("last_played_unix");
        if (!reader.IsDBNull(lastPlayedOrdinal))
            game.LastPlayed = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(lastPlayedOrdinal));

        var headerOrdinal = reader.GetOrdinal("header_image_url");
        if (!reader.IsDBNull(headerOrdinal))
            game.HeaderImageUrl = reader.GetString(headerOrdinal);

        game.IsFree = reader.GetInt32(reader.GetOrdinal("is_free")) == 1;

        var priceOrdinal = reader.GetOrdinal("price_cad");
        if (!reader.IsDBNull(priceOrdinal))
            game.PriceCad = (decimal)reader.GetDouble(priceOrdinal);

        var genresOrdinal = reader.GetOrdinal("genres_json");
        if (!reader.IsDBNull(genresOrdinal))
            game.Genres = JsonSerializer.Deserialize<List<string>>(reader.GetString(genresOrdinal)) ?? new();

        var tagsOrdinal = reader.GetOrdinal("tags_json");
        if (!reader.IsDBNull(tagsOrdinal))
            game.Tags = JsonSerializer.Deserialize<List<string>>(reader.GetString(tagsOrdinal)) ?? new();

        return game;
    }

    // ---------- Collections ----------

    public List<GameCollection> GetCollections()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM collections ORDER BY name;";
        using var reader = cmd.ExecuteReader();

        var collections = new List<GameCollection>();
        while (reader.Read())
        {
            collections.Add(new GameCollection
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
            });
        }

        foreach (var collection in collections)
            collection.AppIds = GetAppIdsInCollection(collection.Id);

        return collections;
    }

    public GameCollection CreateCollection(string name)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO collections (name) VALUES ($name); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$name", name);
        var id = (long)cmd.ExecuteScalar()!;
        return new GameCollection { Id = (int)id, Name = name };
    }

    public void RenameCollection(int collectionId, string newName)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE collections SET name = $name WHERE id = $id;";
        cmd.Parameters.AddWithValue("$name", newName);
        cmd.Parameters.AddWithValue("$id", collectionId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteCollection(int collectionId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM collection_games WHERE collection_id = $id; DELETE FROM collections WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", collectionId);
        cmd.ExecuteNonQuery();
    }

    public void AddGameToCollection(int collectionId, int appId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO collection_games (collection_id, app_id) VALUES ($cid, $appId);";
        cmd.Parameters.AddWithValue("$cid", collectionId);
        cmd.Parameters.AddWithValue("$appId", appId);
        cmd.ExecuteNonQuery();
    }

    public void RemoveGameFromCollection(int collectionId, int appId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM collection_games WHERE collection_id = $cid AND app_id = $appId;";
        cmd.Parameters.AddWithValue("$cid", collectionId);
        cmd.Parameters.AddWithValue("$appId", appId);
        cmd.ExecuteNonQuery();
    }

    private List<int> GetAppIdsInCollection(int collectionId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT app_id FROM collection_games WHERE collection_id = $cid;";
        cmd.Parameters.AddWithValue("$cid", collectionId);
        using var reader = cmd.ExecuteReader();

        var ids = new List<int>();
        while (reader.Read())
            ids.Add(reader.GetInt32(0));
        return ids;
    }
}