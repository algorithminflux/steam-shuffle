using System.Net.Http;
using System.Text.Json;
using SteamShuffle.CoreModels;

namespace SteamShuffle.ApiClients;

/// <summary>
/// Fetches the user's Steam wishlist.
///
/// Steam does not expose wishlists through a plain-API-key Web API call. This
/// uses the "IWishlistService/GetWishlist" endpoint, which Valve's own store
/// front-end calls and which works for any public SteamID64 with no API key.
/// (Steam retired the older "wishlistdata" page endpoint this app used to
/// call — it now just redirects to the store homepage.) This endpoint only
/// returns app IDs, not names, so entries get a placeholder name that's
/// replaced once the store-details sync fills in the real one.
/// </summary>
public class SteamWishlistService
{
    private readonly HttpClient _http;

    public SteamWishlistService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<SteamGame>> GetWishlistAsync(string steamId64)
    {
        var url = $"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?steamid={steamId64}";
        using var response = await _http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                "Could not read wishlist. Make sure the SteamID64 is correct and your " +
                "profile's 'Game details' privacy setting is Public.");
        }

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var results = new List<SteamGame>();

        if (!doc.RootElement.TryGetProperty("response", out var responseEl) ||
            !responseEl.TryGetProperty("items", out var itemsEl))
        {
            return results;
        }

        foreach (var item in itemsEl.EnumerateArray())
        {
            if (!item.TryGetProperty("appid", out var appIdEl))
            {
                continue;
            }

            var appId = appIdEl.GetInt32();
            results.Add(new SteamGame
            {
                AppId = appId,
                Name = $"App {appId}",
                IsOwned = false,
                IsWishlisted = true,
            });
        }

        return results.OrderBy(g => g.AppId).ToList();
    }
}