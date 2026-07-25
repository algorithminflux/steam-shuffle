using System.Net.Http;
using System.Text.Json;
using SteamShuffle.Models;

namespace SteamShuffle.Services;

/// <summary>
/// Talks to the documented Steam Web API (api.steampowered.com) using the user's
/// API key. Covers owned games, playtime, and last-played timestamps.
/// </summary>
public class SteamWebApiService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public SteamWebApiService(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<List<SteamGame>> GetOwnedGamesAsync(string steamId64)
    {
        var url = "https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/" +
                  $"?key={_apiKey}&steamid={steamId64}&include_appinfo=1" +
                  "&include_played_free_games=1&include_extended_appinfo=1";

        using var response = await _http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Steam Web API request failed ({(int)response.StatusCode}). " +
                "Double-check your API key and that this SteamID64's profile is public.");
        }

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var games = new List<SteamGame>();

        if (!doc.RootElement.TryGetProperty("response", out var responseEl) ||
            !responseEl.TryGetProperty("games", out var gamesEl))
        {
            // Empty/private response -> no games, not necessarily an error.
            return games;
        }

        foreach (var g in gamesEl.EnumerateArray())
        {
            var appId = g.GetProperty("appid").GetInt32();
            var name = g.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? $"App {appId}" : $"App {appId}";
            var playtime = g.TryGetProperty("playtime_forever", out var ptEl) ? ptEl.GetInt32() : 0;

            DateTimeOffset? lastPlayed = null;
            if (g.TryGetProperty("rtime_last_played", out var lastPlayedEl))
            {
                var unix = lastPlayedEl.GetInt64();
                if (unix > 0)
                {
                    lastPlayed = DateTimeOffset.FromUnixTimeSeconds(unix);
                }
            }

            games.Add(new SteamGame
            {
                AppId = appId,
                Name = name,
                PlaytimeForeverMinutes = playtime,
                LastPlayed = lastPlayed,
                IsOwned = true,
            });
        }

        return games;
    }
}