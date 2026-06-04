# ATS Employee Stats

<!-- badges:start -->
## Status Badges

[![Continuous Delivery](https://github.com/jpeckham/ats-employee-stats/actions/workflows/continuous-delivery-installers.yml/badge.svg)](https://github.com/jpeckham/ats-employee-stats/actions/workflows/continuous-delivery-installers.yml)
[![Coverage](.github/badges/coverage.svg)](https://github.com/jpeckham/ats-employee-stats/releases/latest/download/coverage-report.zip)
<!-- badges:end -->

A C# WPF companion app for American Truck Simulator save games. It scans historical
`game.sii` files, partitions statistics by trucking company/player character, and
ranks garages, drivers, trucks, missions, and trailer types by profit.

Encrypted/compressed ATS saves are decoded through the infrastructure layer before
projection. Completed delivery history that does not include a trailer definition is
kept under the `unknown` trailer type bucket so its profit is still visible.

## Run

```powershell
dotnet run --project src/AtsEmployeeStats.Wpf
```

On startup, the app checks the default ATS folders under Documents:

- `American Truck Simulator`
- `American Truck Simulator\profiles`
- `American Truck Simulator\steam_profiles`

## Architecture

- `AtsEmployeeStats.Domain`: save document and statistics models.
- `AtsEmployeeStats.Application`: statistics projection and source boundary.
- `AtsEmployeeStats.Infrastructure`: ATS SII parser, historical file scanner, live file watcher.
- `AtsEmployeeStats.Contracts`: presentation DTO contracts.
- `AtsEmployeeStats.Wpf`: desktop dashboard, dependency injection, and presentation view models.

The scanner currently reads plain-text `game.sii` files. If ATS save files are compressed
or encrypted, decrypt or export them to plain SII before scanning.

## Releases

Releases are generated automatically by GitHub Actions on each push to `main`.

- CI computes the next version tag from `version.json` (`v<major>.<minor>.<patch>`).
- It runs the test suite and publishes a self-contained Windows app zip file for `win-x64`.
- It creates a GitHub Release and uploads the generated zip asset for download.
- To install, download the `win-x64` zip from the latest GitHub Release, extract it, and run `AtsEmployeeStats.Wpf.exe`.
