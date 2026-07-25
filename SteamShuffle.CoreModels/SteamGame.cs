namespace SteamShuffle.CoreModels;

/// <summary>
/// A single game owned by the user, merged from the Steam Web API (ownership,
/// playtime, last-played) and the Steam Store API (price, genres, tags).
/// </summary>
public class SteamGame
{
    // --- From GetOwnedGames (Web API) ---
    public int AppId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PlaytimeForeverMinutes { get; set; }
    public DateTimeOffset? LastPlayed { get; set; }

    // --- Provenance: a game can be owned, wishlisted, or (rarely) both ---
    public bool IsOwned { get; set; }
    public bool IsWishlisted { get; set; }

    // Added by hand (e.g. Family Share games, which the Steam Web API never
    // reports as owned by this account) rather than pulled from a sync.
    public bool IsManual { get; set; }

    public string SourceBadge =>
        IsManual ? "Family/Manual" :
        IsOwned && IsWishlisted ? "Owned + Wishlist" :
        IsWishlisted ? "Wishlist" :
        "Owned";

    // --- From Store API (appdetails), cached locally ---
    public string? HeaderImageUrl { get; set; }
    public decimal? PriceCad { get; set; }
    public bool IsFree { get; set; }
    public List<string> Genres { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public DateTimeOffset? StoreDataFetchedAt { get; set; }

    public string CapsuleImageUrl =>
        $"https://cdn.akamai.steamstatic.com/steam/apps/{AppId}/library_600x900.jpg";

    public string PlaytimeDisplay =>
        !IsOwned ? "Not owned yet" :
        PlaytimeForeverMinutes <= 0 ? "Never played" :
        $"{PlaytimeForeverMinutes / 60.0:0.#} hrs";

    public string LastPlayedDisplay =>
        !IsOwned ? "N/A" :
        LastPlayed is null or { Year: <= 1970 } ? "Never" :
        LastPlayed.Value.LocalDateTime.ToString("MMM d, yyyy");

    public string PriceDisplay =>
        IsFree ? "Free to Play" : PriceCad is null ? "N/A" : $"${PriceCad:0.00} CAD";
}