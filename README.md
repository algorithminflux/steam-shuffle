# Steam Shuffle

A WPF desktop app that picks a random game from your own curated collections
(pulled from your owned Steam library **and** your wishlist), with a
slot-machine style reveal animation.

## Requirements

- .NET 8 SDK (Windows) — https://dotnet.microsoft.com/download
- A Steam account with a public "Game details" privacy setting (needed so the
  app can read your owned games and wishlist)

## One-time setup

1. **Get a Steam Web API key**: https://steamcommunity.com/dev/apikey
   (any domain name works for personal use, e.g. `localhost`)
2. **Find your SteamID64**: paste your profile URL into https://steamid.io
   — you want the 17-digit number.
3. **Set your profile to public**: Steam → Profile → Edit Profile → Privacy
   Settings → set "Game details" (and ideally the whole profile) to Public.
   The wishlist endpoint in particular has no API-key-based access — it only
   works if this is public.

Run the app once, click **Settings**, and paste in your API key, SteamID64,
and a store region code (`ca` for CAD pricing).

## Running it

```
cd SteamShuffle
dotnet restore
dotnet run --project SteamShuffle
```

Or open `SteamShuffle.sln` in Visual Studio / Rider and hit Run.

## Running the tests

```
cd SteamShuffle
dotnet test
```

The `SteamShuffle.Tests` project (xUnit) covers:

- **`SteamGameTests`** — the display-string logic (playtime formatting, "Never"
  vs "N/A" vs a real date, free-vs-priced, owned/wishlist source badges).
- **`CollectionRepositoryTests`** — the SQLite layer: upsert merge semantics
  (owning a wishlisted game doesn't drop the wishlist flag or vice versa,
  playtime never regresses, a null last-played doesn't clobber a real one),
  store-detail round-tripping, staleness detection, and collection CRUD
  including duplicate-add and cascade-delete. Each test runs against its own
  temp SQLite file, so tests don't share state or touch your real library.
- **`SteamWebApiServiceTests`**, **`SteamWishlistServiceTests`**,
  **`SteamStoreServiceTests`** — JSON parsing against a fake `HttpMessageHandler`
  (no real network calls), including pagination, the "0 means never played"
  quirk, free-game price handling, and the privacy-related error messages.
- **`AppSettingsTests`** — the `IsConfigured` validation logic.

## How it works

- **Owned games + playtime + last played** come from the documented Steam Web
  API (`IPlayerService/GetOwnedGames`).
- **Wishlist** comes from Steam's legacy public `wishlistdata` endpoint —
  there's no officially documented, API-key-based way to read a wishlist, so
  this is the standard workaround every third-party Steam tool uses. It only
  needs your SteamID64, not the API key.
- **Price (CAD), genre, and store tags** come from the storefront `appdetails`
  endpoint, throttled to roughly 1 request/second and cached locally so you're
  not re-fetching on every launch. Note: what's shown as "tags" is Steam's
  official genre/category data (e.g. "Single-player", "Co-op", "Action") —
  the community-voted tags you see on store pages aren't exposed by any
  official API and would require HTML scraping, which this app deliberately
  avoids.
- Everything (owned/wishlist status, store metadata, your collections) is
  cached in a local SQLite database at
  `%AppData%\SteamShuffle\library.db`. Your API key and SteamID64 live in
  `%AppData%\SteamShuffle\settings.json`. Nothing is sent anywhere except
  directly to Steam's own servers.

## Using it

1. **Sync Library** — pulls your owned games + wishlist, and fills in store
   metadata for anything new. Safe to re-run any time; it only re-fetches
   store details older than 3 days.
2. **+ New** — create a collection (e.g. "Cozy Nights", "Couch Co-op").
3. Select a collection, click **Manage** — check off any owned or wishlisted
   game to add it. Wishlist games show a small "Wishlist" badge.
4. Select a collection and hit **SPIN**.

## Known limitations

- If your Steam profile (or just "Game details") is private, owned-games and
  wishlist fetches will fail with a clear error message telling you to check
  privacy settings.
- The `appdetails` endpoint is unofficial and can occasionally rate-limit or
  omit price data for region-restricted titles; those will show "N/A" for
  price rather than crash the sync.
