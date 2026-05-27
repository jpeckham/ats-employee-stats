# Route-Backed Navigation Design

## Goal

Change the Blazor dashboard from a multi-table drilldown on one screen to a route-backed list/detail experience. The app should open on trucking companies only, then drill into company, garage, driver, truck, and job subsets through explicit `View` actions and browser-friendly URLs.

## Recommended Approach

Keep the existing statistics API contract for this pass. The frontend already receives companies with nested garages, drivers, trucks, missions, and trailer types, which is enough to build the requested navigation without reshaping the API or storage layers.

Replace the current in-memory `DashboardNavigationState` flow with route parameters and small view/filter helpers. The URL should own the selected company, garage, driver, and active tab so refresh and browser Back work naturally.

## Route Map

- `/` lists trucking companies only.
- `/companies/{companyId}` shows company detail with `Garages`, `Drivers`, and `Trucks` tabs.
- `/companies/{companyId}/garages/{garageId}` shows garage detail with `Drivers` and `Trucks` tabs.
- `/companies/{companyId}/garages/{garageId}/drivers/{driverId}` shows driver detail reached from a garage.
- `/companies/{companyId}/drivers/{driverId}` shows driver detail reached from company-level drivers.

Driver detail uses tabs rather than deeper routes for now:

- `Jobs`: missions where `mission.DriverId == driver.Id`.
- `Trucks`: every company truck tied to the driver by current assignment or mission history.

## Filtering Rules

Company detail tabs:

- Garages: `company.Garages`
- Drivers: `company.Drivers`
- Trucks: `company.Trucks`

Garage detail tabs:

- Drivers: `company.Drivers.Where(driver => driver.GarageId == garage.Id)`
- Trucks: `company.Trucks.Where(truck => truck.GarageId == garage.Id)`

Driver detail tabs:

- Jobs: `company.Missions.Where(mission => mission.DriverId == driver.Id)`
- Trucks: trucks where `truck.DriverId == driver.Id`, `driver.TruckId == truck.Id`, or any matching driver mission references the truck id.

## UX

Rows should use explicit `View` buttons as the primary navigation action. Breadcrumbs should be links, for example `Companies > Desert Line > phoenix > Alice Ramirez`.

Keep the time range selector, live status text, and progress strip at the top of every dashboard route. If a selected id is not found in the loaded data, show a clear empty state with a link back to the nearest valid parent.

## Testing

Use test-first implementation for extracted navigation/filter behavior. The most important unit coverage is the driver truck history filter because it combines current assignment and historical mission evidence.

Run targeted tests after each behavior change, then `dotnet test` before completion.
