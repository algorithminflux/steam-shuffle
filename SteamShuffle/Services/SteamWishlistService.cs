using System.Net.Http;
using System.Text.Json;
using SteamShuffle.Models;

namespace SteamShuffle.Services;

/// <summary>
/// Fetches the user's Steam wishlist.
///
/// Steam does not expose wishlists through the documented Web API with a plain
/// API key. This uses the legacy public "wishlistdata" endpoint, which works for
/// any SteamID64 whose profile has "game details" set to Public in their Steam
/// privacy settings. No API key required for this call.
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
        var results = new List<SteamGame>();
        int page = 0;

        while (true)
        {
            var url = $"https://store.steampowered.com/wishlist/profiles/{steamId64}/wishlistdata/?p={page}";
            using var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (page == 0)
                {
                    throw new InvalidOperationException(
                        "Could not read wishlist. Make sure the SteamID64 is correct and your " +
                        "profile's 'Game details' privacy setting is Public.");
                }
                break;
            }

            var body = await response.Content.ReadAsStringAsync();

            // Steam returns "[]" (an empty JSON array) once you've paged past the end.
            if (string.IsNullOrWhiteSpace(body) || body.Trim() == "[]")
                break;

            // A private/invalid profile returns an HTML page with a 200 status
            // instead of JSON (e.g. a login wall) — treat that as a normal failure.
            if (body.TrimStart().StartsWith('<'))
            {
                if (page == 0)
                {
                    throw new InvalidOperationException(
                        "Could not read wishlist. Make sure the SteamID64 is correct and your " +
                        "profile's 'Game details' privacy setting is Public.");
                }
                break;
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || doc.RootElement.EnumerateObject().MoveNext() == false)
                break;

            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                if (!int.TryParse(entry.Name, out var appId))
                    continue;

                var name = entry.Value.TryGetProperty("name", out var nameEl)
                    ? nameEl.GetString() ?? $"App {appId}"
                    : $"App {appId}";

                results.Add(new SteamGame
                {
                    AppId = appId,
                    Name = name,
                    IsOwned = false,
                    IsWishlisted = true,
                });
            }

            page++;
        }

        return results.OrderBy(g => g.Name).ToList();
    }
}