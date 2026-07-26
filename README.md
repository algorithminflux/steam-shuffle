# Steam Shuffle

A cross-platform (Windows/Linux/macOS) desktop app, built with Avalonia UI,
that picks a random game from your own curated collections (pulled from your
owned Steam library **and** your wishlist), with a slot-machine style reveal
animation.

## Get it running

1. Grab the zip for your OS from **[Releases](../../releases/latest)**:
   `win-x64`, `linux-x64`, or `osx-arm64`.
2. Unzip it.
3. Run it:
   - **Windows** — double-click `SteamShuffle.exe`.
   - **Linux** — `./SteamShuffle` (if that's blocked, `chmod +x SteamShuffle` first).
   - **macOS** — right-click → **Open** and confirm (it's unsigned, so Gatekeeper
     will otherwise block it), or run `xattr -d com.apple.quarantine SteamShuffle && ./SteamShuffle`.
4. On first launch, click **Settings** and enter:
   - A Steam Web API key — get one free at <https://steamcommunity.com/dev/apikey>
   - Your SteamID64 — paste your profile URL into <https://steamid.io> to find it
   - A store region code (`ca` for CAD pricing)

   Your Steam profile's **Game details** privacy setting needs to be Public —
   that's what lets the app read your owned games and wishlist.
5. Click **Sync Library**, then **SPIN**.

## Development

```
git clone https://github.com/algorithminflux/steam-shuffle.git
cd steam-shuffle
dotnet restore
dotnet run --project SteamShuffle
```

Or open `SteamShuffle.sln` in Visual Studio / Rider and hit Run. Requires the
.NET 8 SDK (<https://dotnet.microsoft.com/download>) — works on Windows,
Linux, or macOS.

Run the test suite with:

```
dotnet test
```

---

## Screenshots

| [![Reel ready to spin](https://github.com/algorithminflux/steam-shuffle/raw/main/docs/2026-07-26_before-spin.png)](docs/2026-07-26_before-spin.png) Ready to spin | [![Spin result with game details](https://github.com/algorithminflux/steam-shuffle/raw/main/docs/2026-07-26_after-spin.png)](docs/2026-07-26_after-spin.png) Spin result |
| --- | --- |
| [![Manage Collection window](https://github.com/algorithminflux/steam-shuffle/raw/main/docs/2026-07-26_manage-collection.png)](docs/2026-07-26_manage-collection.png) Manage a collection | [![Manage Collection window filtered to selected games only](https://github.com/algorithminflux/steam-shuffle/raw/main/docs/2026-07-26_manage-collection_selected-only.png)](docs/2026-07-26_manage-collection_selected-only.png) "Selected only" filter |
| [![Add Game Manually search dialog](https://github.com/algorithminflux/steam-shuffle/raw/main/docs/2026-07-26_add-game-manually.png)](docs/2026-07-26_add-game-manually.png) Add a game manually (e.g. Family Share) | [![Settings window](https://github.com/algorithminflux/steam-shuffle/raw/main/docs/2026-07-26_settings.png)](docs/2026-07-26_settings.png) Settings |

## Using it

1. **Sync Library** — pulls your owned games + wishlist, and fills in store
   metadata for anything new. Safe to re-run any time; it only re-fetches
   store details older than 3 days.
2. **+ New** — create a collection (e.g. "Cozy Nights", "Couch Co-op").
3. Select a collection, click **Manage** — check off any owned or wishlisted
   game to add it. Wishlist games show a small "Wishlist" badge. Tick
   **Selected only** to filter the list down to just what's already in the
   collection.
4. **+ Add Game** — for games the Steam Web API can't see (most commonly
   Family Share titles, which never show up in `GetOwnedGames`), search by
   name, pick the match, and it's added to your library tagged
   "Family/Manual" so it can join collections and spins like any other game.
5. Select a collection and hit **SPIN**.

## How it works

- **Owned games + playtime + last played** come from the documented Steam Web
  API (`IPlayerService/GetOwnedGames`).
- **Wishlist** comes from Steam's `IWishlistService/GetWishlist` endpoint —
  there's no officially documented, API-key-based way to read a wishlist, so
  this is the standard workaround every third-party Steam tool uses. It only
  needs your SteamID64, not the API key. This endpoint only returns app IDs,
  so wishlist entries show a placeholder name until the next store-details
  sync fills in the real one.
- **Price (CAD), genre, and store tags** come from the storefront `appdetails`
  endpoint, throttled to roughly 1 request/second and cached locally. What's
  shown as "tags" is Steam's official genre/category data (e.g.
  "Single-player", "Co-op", "Action") — the community-voted tags on store
  pages aren't exposed by any official API and would require HTML scraping,
  which this app deliberately avoids.
- Everything (owned/wishlist status, store metadata, your collections) is
  cached in a local SQLite database under your OS's app-data folder
  (`%AppData%\SteamShuffle\library.db` on Windows, `~/.config/SteamShuffle/`
  on Linux, `~/Library/Application Support/SteamShuffle/` on macOS). Your API
  key and SteamID64 live alongside it in `settings.json`. Nothing is sent
  anywhere except directly to Steam's own servers.
- If a wishlist placeholder name (e.g. "App 619820") is still showing after a
  sync, the next **Sync Library** keeps retrying that game's store details
  regardless of the normal 3-day cache window, until a real name comes back.
- Reel tiles show the library capsule art (tall, matches the tile shape) when
  available; if a game has no capsule image, the store's wide header banner
  is shown letterboxed instead of cropped, and if neither loads, the tile
  falls back to just the game's name.

## Architecture

The solution is split into layered projects (dependency rule flows one way,
outer → inner never the reverse):

- **`SteamShuffle.CoreModels`** — pure domain models/DTOs (`SteamGame`,
  `GameCollection`, `StoreDetails`, `StoreSearchResult`, `AppSettings`,
  `LibrarySyncProgress`) plus the `ICollectionRepository` abstraction. No
  dependencies.
- **`SteamShuffle.ApiClients`** — thin wrappers around the external Steam HTTP
  endpoints (`SteamWebApiService`, `SteamWishlistService`, `SteamStoreService`).
- **`SteamShuffle.Infrastructure`** — local persistence: `CollectionRepository`
  (SQLite) and `AppSettingsStore` (the `settings.json` file).
- **`SteamShuffle.Services`** — business orchestration (`LibraryManager`),
  which depends on Infrastructure only through `ICollectionRepository`, never
  the concrete SQLite class.
- **`SteamShuffle`** — the Avalonia presentation project (Views, Controls,
  MainWindow, App) and composition root.
- **`SteamShuffle.Tests`** — xUnit tests, folders mirror the project split.

### Test coverage

- **`CoreModels/SteamGameTests`** — display-string logic (playtime formatting,
  "Never" vs "N/A" vs a real date, free-vs-priced, owned/wishlist badges).
- **`CoreModels/AppSettingsTests`** — `IsConfigured` validation.
- **`Infrastructure/CollectionRepositoryTests`** — SQLite upsert merge
  semantics, store-detail round-tripping, staleness detection, collection CRUD
  (each test runs against its own temp SQLite file, no shared state).
- **`ApiClients/*ServiceTests`** — JSON parsing against a fake
  `HttpMessageHandler` (no real network calls), including the "0 means never
  played" quirk, free-game price handling, and privacy-related error messages.

## Known limitations

- If your Steam profile (or just "Game details") is private, owned-games and
  wishlist fetches will fail with a clear error message telling you to check
  privacy settings.
- The `appdetails` endpoint is unofficial and can occasionally rate-limit or
  omit price data for region-restricted titles; those will show "N/A" for
  price rather than crash the sync.
- Family Share games never appear via the Steam Web API (no official endpoint
  exposes a family's shared library) — use **+ Add Game** to add them by hand.
