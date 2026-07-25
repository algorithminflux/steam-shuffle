# AGENTS.md

## Architecture

Layered projects, dependency rule flows one way (outer -> inner never the reverse):

- `SteamShuffle.CoreModels` — pure domain models/DTOs (`SteamGame`, `GameCollection`, `StoreDetails`, `StoreSearchResult`, `AppSettings`, `LibrarySyncProgress`) plus the `ICollectionRepository` abstraction. No dependencies.
- `SteamShuffle.ApiClients` — thin wrappers around external Steam HTTP endpoints (`SteamWebApiService`, `SteamWishlistService`, `SteamStoreService`). References CoreModels only.
- `SteamShuffle.Infrastructure` — local persistence: `CollectionRepository` (SQLite, implements `ICollectionRepository`) and `AppSettingsStore` (JSON settings file). References CoreModels only.
- `SteamShuffle.Services` — business orchestration (`LibraryManager`). References CoreModels + ApiClients directly; depends on Infrastructure only through the `ICollectionRepository` interface (never the concrete `CollectionRepository`), so Services stays swappable/testable without a real SQLite file.
- `SteamShuffle` — WPF presentation project and composition root (Views, Controls, MainWindow, App). References all four of the above and wires up concrete instances (e.g. `new CollectionRepository()`).
- `SteamShuffle.Tests` — references everything; test folders mirror the project split (`CoreModels/`, `ApiClients/`, `Infrastructure/`).

When adding a new class, place it by what it does, not where it's used from: talks to an external HTTP API -> ApiClients; reads/writes local disk state -> Infrastructure; pure data crossing project boundaries -> CoreModels; orchestrates the above -> Services; WPF-only glue -> the main project. If Services needs something from Infrastructure, add the abstraction to CoreModels rather than referencing Infrastructure directly.

## Code style

- All `if` statements must use curly braces, even for single-line bodies. No braceless `if (x) return;` style.

## Class member order

- Order: private fields/properties, then public properties, then constructor(s), then public methods, then private methods.

## Workflow

- After any code modification, run `dotnet format` then `dotnet test` before considering the change done.
