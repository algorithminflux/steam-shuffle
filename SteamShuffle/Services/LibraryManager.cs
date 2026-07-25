using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SteamShuffle.Models;

namespace SteamShuffle.Services
{
    public record LibrarySyncProgress(string Message, int Completed, int Total);

    /// <summary>
    /// Top-level orchestration: pull owned games + wishlist from Steam, merge them,
    /// enrich with store metadata (throttled), and persist everything locally.
    /// </summary>
    public class LibraryManager
    {
        private readonly CollectionRepository _repo;
        private readonly HttpClient _http;

        public LibraryManager(CollectionRepository repo, HttpClient http)
        {
            _repo = repo;
            _http = http;
        }

        public List<SteamGame> GetCachedLibrary() => _repo.GetAllGames();

        /// <summary>
        /// Refreshes owned games + wishlist from Steam and tops up store metadata
        /// for anything missing or stale. Reports progress for a UI progress bar.
        /// </summary>
        public async Task<List<SteamGame>> SyncAsync(
            AppSettings settings,
            IProgress<LibrarySyncProgress>? progress = null,
            CancellationToken ct = default)
        {
            progress?.Report(new("Fetching owned games...", 0, 0));
            var webApi = new SteamWebApiService(_http, settings.SteamApiKey);
            var owned = await webApi.GetOwnedGamesAsync(settings.SteamId64);

            progress?.Report(new("Fetching wishlist...", 0, 0));
            var wishlistService = new SteamWishlistService(_http);
            List<SteamGame> wishlist;
            try
            {
                wishlist = await wishlistService.GetWishlistAsync(settings.SteamId64);
            }
            catch (InvalidOperationException)
            {
                // Wishlist is optional — profile might be private for it. Owned games still work.
                wishlist = new List<SteamGame>();
            }

            _repo.UpsertGames(owned);
            _repo.UpsertGames(wishlist);

            var staleIds = _repo.GetAppIdsNeedingStoreRefresh(TimeSpan.FromDays(3));
            var storeService = new SteamStoreService(_http, settings.CountryCode);

            for (int i = 0; i < staleIds.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new($"Fetching store details...", i + 1, staleIds.Count));

                var details = await storeService.GetAppDetailsAsync(staleIds[i], ct);
                if (details is not null)
                    _repo.SaveStoreDetails(details);
            }

            return _repo.GetAllGames();
        }
    }
}
