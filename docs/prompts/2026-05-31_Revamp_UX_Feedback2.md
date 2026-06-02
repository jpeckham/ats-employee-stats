GOAL

Convert this application from .NET MAUI to WPF while preserving the existing Clean Architecture.

The application is no longer intended to be cross-platform.

The target platform is Windows desktop only.

This application is a desktop data exploration and analysis tool similar to:

- SQL Server Management Studio
- Azure Storage Explorer
- Visual Studio Solution Explorer
- Process Explorer

The application is NOT a mobile application and should NOT use mobile-first UI patterns.

======================================================================
ARCHITECTURE REQUIREMENTS
======================================================================

Preserve all existing layers:

- Domain
- Application
- Infrastructure
- Data access
- Analytics / Gold layer

Only replace the Presentation layer.

The WPF project should become the composition root.

Use MVVM.

Use CommunityToolkit.Mvvm.

Presentation layer should depend only on Application contracts.

No Domain or Infrastructure references from Views.

======================================================================
TARGET FRAMEWORK
======================================================================

Use:

- .NET 10
- WPF
- CommunityToolkit.Mvvm

Do not use MAUI compatibility packages.

Remove MAUI-specific code, controls, services, navigation, and XAML.

======================================================================
UI DESIGN REQUIREMENTS
======================================================================

This application is a desktop explorer.

Use a two-pane layout:

--------------------------------------------------------
| Explorer Tree | Detail View                          |
--------------------------------------------------------

The explorer pane should remain visible at all times.

The explorer should support hierarchy:

Companies
  Company
    Garages
      Garage
    Drivers
      Driver
    Trucks
      Truck
    Trailers
      Trailer
    Jobs
    Cities

Selecting a node updates the detail panel.

Avoid page navigation whenever possible.

Favor master-detail interaction.

======================================================================
DATA DISPLAY REQUIREMENTS
======================================================================

Replace card-heavy layouts with information-dense desktop layouts.

Use:

- DataGrid
- TreeView
- Grid
- TabControl
- DockPanel

Avoid:

- Mobile card layouts
- Excessive whitespace
- Vertical scrolling dashboards

Users may have:

- 180+ trucks
- 180+ drivers
- 40+ garages
- 7000+ jobs

Optimize for comparison and analysis.

======================================================================
SORTING
======================================================================

Use DataGrid column header sorting.

Example:

Name ▲
Profit ▼
Jobs

Do not use dedicated sort buttons.

======================================================================
TABS
======================================================================

Retain entity relationship tabs.

Examples:

Company:
- Overview
- Garages
- Drivers
- Trucks
- Trailers
- Jobs
- Cities

Garage:
- Drivers
- Trucks
- Trailers

Driver:
- Jobs
- Trucks
- Garages

Use WPF TabControl.

======================================================================
SPARKLINES
======================================================================

Reintroduce sparklines.

Many existing web screens contain trend visualizations.

Each row should be able to display a compact trend graph.

Examples:

Garage Profit Trend
Driver Profit Trend
Truck Profit Trend

Use a lightweight WPF sparkline implementation.

======================================================================
VIEW MODELS
======================================================================

Create ViewModels for:

MainWindow
CompanyExplorer
CompanyDetail
GarageDetail
DriverDetail
TruckDetail
TrailerDetail
JobDetail
CityDetail

Use ObservableObject and RelayCommand.

======================================================================
DELIVERABLES
======================================================================

1. Remove MAUI presentation project.
2. Create WPF presentation project.
3. Create MVVM structure.
4. Build explorer tree navigation.
5. Convert existing screens to WPF.
6. Replace lists with DataGrid.
7. Add sorting.
8. Add sparkline support.
9. Preserve all existing functionality.
10. Produce migration notes describing every architectural change.