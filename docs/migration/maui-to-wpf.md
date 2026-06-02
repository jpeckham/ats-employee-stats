# MAUI to WPF Migration Notes

## Summary

The presentation layer was migrated from .NET MAUI to WPF because the app is now a Windows-only desktop data exploration tool. The goal is to preserve the existing Clean Architecture and replace only the presentation technology.

## Architectural Changes

- Removed the `AtsEmployeeStats.Maui` project from the solution and source tree.
- Added `AtsEmployeeStats.Wpf` as the Windows desktop composition root.
- Kept Domain, Application, Infrastructure, data access, and analytics/gold-layer projects unchanged.
- WPF composition uses Microsoft.Extensions.DependencyInjection to wire Application use cases and Infrastructure implementations.
- Views are WPF XAML and do not reference Domain or Infrastructure namespaces.
- ViewModels depend on Application contracts and DTOs from `AtsEmployeeStats.Contracts`.

## UI Changes

- Replaced page-style MAUI navigation with a persistent two-pane desktop layout.
- Left pane uses a WPF `TreeView` for the explorer hierarchy.
- Right pane uses detail panels with `TabControl` and `DataGrid`.
- Entity lists use sortable `DataGrid` column headers instead of explicit sort buttons.
- Dense grid views replace mobile-oriented card layouts.
- A lightweight WPF `SparklineControl` renders compact row trends.

## MVVM Structure

- `MainWindowViewModel` loads dashboard statistics and coordinates selection.
- `CompanyExplorerViewModel` exposes the explorer tree.
- `CompanyDetailViewModel`, `GarageDetailViewModel`, `DriverDetailViewModel`, `TruckDetailViewModel`, `TrailerDetailViewModel`, `JobDetailViewModel`, and `CityDetailViewModel` expose detail tabs and rows.
- ViewModels use CommunityToolkit.Mvvm `ObservableObject` and `RelayCommand`.

## Composition Root

`App.xaml.cs` is the WPF composition root. It registers:

- `SqliteMedallionSaveSnapshotSource`
- `IStatisticsIngestUseCase`
- `IStatisticsDashboardUseCases`
- `IStatisticsReloadUseCase`
- recommendation and diagnosis use cases
- `MainWindowViewModel`
- `MainWindow`

## Behavior Preserved

- Local use cases remain the source of statistics.
- The app reads from local save-derived medallion data.
- Company, garage, driver, truck, trailer, job, and city detail surfaces are still represented.
- Parent-child exploration is retained through the tree and related-data tabs.
