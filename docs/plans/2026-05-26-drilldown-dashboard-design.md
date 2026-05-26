# Drilldown Dashboard Design

## Goal

Replace the flat dashboard with a drilldown flow: trucking company/player character, garage profitability, garage drivers, and driver job profitability. The dashboard should use a 14-day default history window, support a 7-day range option, format money with dollar signs and commas, and fix missing truck assignments where the save file stores driver/truck relationships indirectly.

## Recommended Approach

Build the drilldown on top of the existing SQLite-backed statistics pipeline. Keep the current medallion warehouse and add the missing derived aggregate for driver route pairs. Rebuild silver and gold tables from cached bronze rows when derived schema changes, so users do not need to delete the SQLite database.

This is preferable to a UI-only implementation because route-pair profitability and time-range reporting belong in the reporting layer, not inside ad hoc Terminal.Gui table code. A full reporting-query service can be added later if the dashboard grows, but this pass can keep scope lower by extending the current statistics model and SQLite projection.

## Navigation

The startup screen lists trucking companies. The app treats trucking company and player character as synonymous.

Selecting a company opens a garage screen:

- `Garage`
- `Profit`
- `$/Day`
- `Drivers`
- `Trucks`

Selecting a garage opens a driver screen:

- `Driver`
- `Profit`
- `$/Day`
- `Truck`
- `Jobs`

Selecting a driver opens a job screen:

- job type profitability
- route-pair profitability
- individual job details

Route-pair profitability combines both directions of the same route into one row. For example, `Phoenix -> Denver` and `Denver -> Phoenix` aggregate under `Phoenix <-> Denver`.

## Time Ranges

Set the command-line default history window to 14 days. The dashboard range selector should support `Last 14 days` and `Last 7 days`, defaulting to 14.

The current data model does not store a mission timestamp. Until mission-level dates are available, range selection is based on the save-file history window loaded into the warehouse, and `$/Day` is computed as aggregate profit divided by the selected range days.

## Money Formatting

Format money values as whole-dollar strings with a dollar sign and comma grouping, such as `$1,234,567`. Use one helper for dashboard table values so garage, driver, job type, route-pair, and job detail screens are consistent.

## Truck Assignment Fix

Truck assignment should be inferred in this priority order:

1. Driver scalar fields such as `assigned_truck`, `truck`, or `vehicle`.
2. Truck scalar fields that point back to a driver, such as `assigned_driver`, `driver`, or `employee`.
3. Garage array index pairing. If a garage lists drivers and vehicles in parallel arrays, pair `drivers[i]` with `vehicles[i]` when neither side has an explicit assignment.

Silver and gold tables should rebuild from bronze rows after this projection changes so corrected truck IDs appear without requiring manual database deletion.

## Testing

Use test-first implementation:

- default history changes from 5 to 14 days
- projection infers truck IDs from garage driver/vehicle index pairing
- dashboard starts on company selection
- garage screen formats dollars and computes dollars per day
- driver screen filters by selected garage and shows truck IDs
- driver job screen aggregates job types and route pairs
- SQLite warehouse writes and reads route-pair rows if a gold table is added

