# Web API Blazor SignalR Design

## Goal

Replace the Terminal.Gui console presentation with a minimal ASP.NET Core API and a Blazor WebAssembly frontend, while keeping the existing clean architecture boundaries.

## Architecture

The existing `Domain`, `Application`, and `Infrastructure` projects remain the core of the system. The console project is removed. Presentation moves into:

- `AtsEmployeeStats.Api`: composition root, minimal API endpoints, SignalR hub, configuration, and static hosting for the WebAssembly frontend.
- `AtsEmployeeStats.Web`: Blazor WebAssembly UI, HTTP client calls, SignalR client subscription, and browser-side drilldown state.

Dependencies point inward:

- `Web` depends on shared DTO contracts and browser libraries.
- `Api` depends on `Application`, `Infrastructure`, and shared DTO contracts.
- `Application` depends on `Domain`.
- `Infrastructure` depends on `Application` and `Domain`.
- `Domain` depends on nothing application-specific.

## Backend

The API exposes a small dashboard surface:

- `GET /api/statistics`: returns the current statistics snapshot.
- `POST /api/statistics/reload`: reloads saves and broadcasts the updated snapshot.
- `GET /api/config`: returns resolved save-root and history settings.

The API hosts SignalR at `/hubs/statistics` with these client messages:

- `LoadingProgress`: emitted during scanning/parsing.
- `StatisticsUpdated`: emitted after a successful reload.
- `StatusChanged`: emitted for no-data, read errors, and reload lifecycle messages.

The first pass can load statistics on demand through the existing `StatisticsService`. A small application-facing adapter can convert domain statistics to DTOs so the Blazor client does not bind directly to domain entities.

## Frontend

The Blazor WebAssembly app starts on a dashboard screen, not a marketing page. It shows:

- company/player list
- selected company garage list
- selected garage driver list
- selected driver job summaries and job details
- 14-day / 7-day segmented range toggle
- breadcrumb/back navigation
- live status strip fed by SignalR

The UI keeps navigation state browser-side. The API remains stateless except for live reload broadcasting.

## Replacement Scope

`AtsEmployeeStats.Console` is removed from the solution. Console-specific tests are deleted or replaced. Existing projection, parser, and SQLite tests stay.

## Testing

Use test-first implementation:

- endpoint tests prove `/api/statistics`, `/api/config`, and `/api/statistics/reload` are mapped
- DTO mapping tests prove company, garage, driver, and driver-job fields survive projection
- Blazor navigation state tests prove breadcrumbs and back navigation
- full solution build/test proves the console project is gone and the new projects compile
