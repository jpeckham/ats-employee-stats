# Driver Profit Distance Design

Add a driver-table profitability-per-distance column without changing storage.

Drivers tab should show a sortable `Profit/Dist` column. Each cell displays the company currency and the game's distance unit, for example `$12.34/mi` for ATS and `€8.90/km` for ETS2. If a driver has no positive job distance, the cell shows `-`.

Use existing `CompanyDto.RecentDriverJobs` distances. Match jobs by `DriverId`, sum positive `Distance` values, and divide the driver's current range `Profit` by that sum. Infer distance unit from the same company prefix pattern already used for currency: `ets2-*` uses `km`; all other company IDs use `mi`.

Keep the change in WPF view-model formatting. Do not rebuild the database or add contract fields unless later screens need the metric from application APIs.
