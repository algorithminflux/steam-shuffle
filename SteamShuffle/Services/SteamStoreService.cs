using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SteamShuffle.Models;

namespace SteamShuffle.Services
{
    /// <summary>
    /// Fetches store-page metadata (price, genres, categories, header art) from the
    /// undocumented but widely-used "appdetails" storefront endpoint.
    ///
    /// Note: full community "tags" (the ones players vote on, e.g. "Roguelike",
    /// "Metroidvania") are NOT available through any official API — they'd require
    /// scraping the store page's HTML. This uses Steam's official "genres" and
    /// "categories" (e.g. "Single-player", "Co-op", "Action") as a solid stand-in.
    ///
    /// This endpoint is rate-limited by Steam (roughly one request per second is
    /// safe for sustained use), so calls are throttled and results should be cached.
    /// </summary>
    public class SteamStoreService
    {
        private readonly HttpClient _http;
        private readonly string _countryCode;
        private DateTime _lastCallUtc = DateTime.MinValue;
        private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(900);

        public SteamStoreService(HttpClient http, string countryCode)
        {
            _http = http;
            _countryCode = string.IsNullOrWhiteSpace(countryCode) ? "ca" : countryCode;
        }

        public async Task<StoreDetails?> GetAppDetailsAsync(int appId, CancellationToken ct = default)
        {
            await ThrottleAsync(ct);

            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc={_countryCode}&l=english";
            using var response = await _http.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty(appId.ToString(), out var entry) ||
                !entry.TryGetProperty("success", out var successEl) ||
                !successEl.GetBoolean() ||
                !entry.TryGetProperty("data", out var data))
            {
                return null;
            }

            var details = new StoreDetails { AppId = appId };

            if (data.TryGetProperty("header_image", out var headerEl))
                details.HeaderImageUrl = headerEl.GetString();

            if (data.TryGetProperty("is_free", out var freeEl))
                details.IsFree = freeEl.GetBoolean();

            if (!details.IsFree && data.TryGetProperty("price_overview", out var priceEl) &&
                priceEl.TryGetProperty("final", out var finalEl))
            {
                // Steam returns price in the smallest currency unit (cents for CAD).
                details.PriceCad = finalEl.GetInt64() / 100m;
            }

            if (data.TryGetProperty("genres", out var genresEl))
            {
                foreach (var g in genresEl.EnumerateArray())
                {
                    if (g.TryGetProperty("description", out var descEl) && descEl.GetString() is { } desc)
                        details.Genres.Add(desc);
                }
            }

            if (data.TryGetProperty("categories", out var categoriesEl))
            {
                foreach (var c in categoriesEl.EnumerateArray())
                {
                    if (c.TryGetProperty("description", out var descEl) && descEl.GetString() is { } desc)
                        details.Tags.Add(desc);
                }
            }

            return details;
        }

        private async Task ThrottleAsync(CancellationToken ct)
        {
            var elapsed = DateTime.UtcNow - _lastCallUtc;
            if (elapsed < MinInterval)
                await Task.Delay(MinInterval - elapsed, ct);
            _lastCallUtc = DateTime.UtcNow;
        }
    }

    public class StoreDetails
    {
        public int AppId { get; set; }
        public string? HeaderImageUrl { get; set; }
        public bool IsFree { get; set; }
        public decimal? PriceCad { get; set; }
        public List<string> Genres { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }
}
