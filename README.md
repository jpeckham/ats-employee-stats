# ATS Employee Stats

A C# web companion app for American Truck Simulator save games. It scans historical
`game.sii` files, partitions statistics by trucking company/player character, and
ranks garages, drivers, trucks, missions, and trailer types by profit.

Encrypted/compressed ATS saves are decoded through the infrastructure layer before
projection. Completed delivery history that does not include a trailer definition is
kept under the `unknown` trailer type bucket so its profit is still visible.

## Run

```powershell
dotnet run --project src/AtsEmployeeStats.Api --urls http://localhost:5000 -- `
  --Statistics:SaveRoot "$HOME\Documents\American Truck Simulator"
```

Open `http://localhost:5000` after the API starts. The API serves the Blazor WebAssembly
frontend, JSON endpoints, and SignalR hub from the same host.

If `--save-root` is omitted, the app checks the default ATS folder under Documents:

- `American Truck Simulator`
- `American Truck Simulator\profiles`
- `American Truck Simulator\steam_profiles`

By default, the app scans saves modified in the last 14 days. Override that with
configuration:

```powershell
dotnet run --project src/AtsEmployeeStats.Api -- `
  --Statistics:SaveRoot "C:\path\to\ATS" `
  --Statistics:HistoryDays 7
```

## API

- `GET /api/config`
- `GET /api/statistics?rangeDays=14`
- `POST /api/statistics/reload?rangeDays=14`
- SignalR hub: `/hubs/statistics`

The SignalR hub broadcasts `StatusChanged`, `LoadingProgress`, and `StatisticsUpdated`
messages for the live frontend.

## Architecture

- `AtsEmployeeStats.Domain`: save document and statistics models.
- `AtsEmployeeStats.Application`: statistics projection and source boundary.
- `AtsEmployeeStats.Infrastructure`: ATS SII parser, historical file scanner, live file watcher.
- `AtsEmployeeStats.Contracts`: API/Web DTO contracts.
- `AtsEmployeeStats.Api`: minimal API, SignalR hub, dependency injection, and hosted frontend.
- `AtsEmployeeStats.Web`: Blazor WebAssembly dashboard.

The scanner currently reads plain-text `game.sii` files. If ATS save files are compressed
or encrypted, decrypt or export them to plain SII before scanning.
