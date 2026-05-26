# ATS Employee Stats

A C# console companion app for American Truck Simulator save games. It scans historical
`game.sii` files, watches for newly written saves, partitions statistics by trucking
company/player character, and ranks garages, drivers, trucks, missions, and trailer
types by profit.

Encrypted/compressed ATS saves are decoded through the infrastructure layer before
projection. Completed delivery history that does not include a trailer definition is
kept under the `unknown` trailer type bucket so its profit is still visible.

## Run

```powershell
dotnet run --project src/AtsEmployeeStats.Console -- --save-root "$HOME\Documents\American Truck Simulator"
```

By default, the app scans saves modified in the last 5 days. Override that with
`--history-days <days>`.

If `--save-root` is omitted, the app checks the default ATS folder under Documents:

- `American Truck Simulator`
- `American Truck Simulator\profiles`
- `American Truck Simulator\steam_profiles`

Use `--once` to print one dashboard snapshot and exit:
Use `--view garages|drivers|trailers|trucks|missions` to choose the printed ranking.

```powershell
dotnet run --project src/AtsEmployeeStats.Console -- --save-root "C:\path\to\ATS" --once --view trailers
```

## Controls

- Left/right: switch rankings
- Up/down: switch trucking company
- `r`: rescan saves
- `q` or Escape: quit

When the app is launched without an interactive console input stream, it renders one
snapshot and exits instead of waiting for key navigation. Use `--once` for scripted runs.

## Architecture

- `AtsEmployeeStats.Domain`: save document and statistics models.
- `AtsEmployeeStats.Application`: statistics projection and source boundary.
- `AtsEmployeeStats.Infrastructure`: ATS SII parser, historical file scanner, live file watcher.
- `AtsEmployeeStats.Console`: interactive terminal dashboard.

The scanner currently reads plain-text `game.sii` files. If ATS save files are compressed
or encrypted, decrypt or export them to plain SII before scanning.
