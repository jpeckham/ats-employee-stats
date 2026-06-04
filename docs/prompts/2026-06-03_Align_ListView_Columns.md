# Align List View Column Headers and Values

## Goal

Every list/tab in the app currently shares one of two column sets (`TableColumns.Default` or `TableColumns.Cities`). The generic headers ("Details", "Jobs / Meta", "Trend") are reused across entity types even when the underlying data has nothing to do with jobs, meta, or a trend. This makes column labels meaningless in several tabs and causes one case where a column is actively misleading (Driver › Garages shows "Current/Past" under the header "Profit").

Create per-tab column definitions (or at minimum per-entity-type ones) so that every column header accurately describes the value it contains.

---

## Current column layouts

### `TableColumns.Companies` (Companies list)
| Header | Value |
|--------|-------|
| Company | `company.DisplayName` |
| Profit | `$N` total company profit |
| Details | `"N garages / N drivers / N trucks"` |
| Jobs / Meta | `"N jobs"` |
| Trend | sparkline (company profit trend) |

> **Issue:** `Companies` is identical to `Default` except the first header. No substantive improvement; "Details" and "Jobs / Meta" carry different data per entity type.

---

### `TableColumns.Default` — Garages tab (Company › Garages, Garage drill-in lists)
| Header | Value |
|--------|-------|
| Name | `garage.DisplayName` |
| Profit | `$N` |
| Details | `"N drivers / N trucks"` |
| Jobs / Meta | `"$N/day"` (average profit per day) |
| Trend | sparkline |

> **Issue:** "Jobs / Meta" actually shows a money-per-day value. "Details" is acceptable but "Counts" or "Staff" would be more precise.

---

### `TableColumns.Default` — Drivers tab (Company › Drivers, Garage › Drivers)
| Header | Value |
|--------|-------|
| Name | `driver.DisplayName` |
| Profit | `$N` |
| Details | `"Garage / Truck"` (display names) |
| Jobs / Meta | `"N jobs"` |
| Trend | sparkline |

> **Issue:** "Details" shows assignment info, not "details" per se. Reasonable but could be "Assignment".

---

### `TableColumns.Default` — Trucks tab (Company › Trucks, Garage › Trucks, Driver › Trucks, Trailer › Trucks)
| Header | Value |
|--------|-------|
| Name | `truck.DisplayName` (model + plate) |
| Profit | `$N` |
| Details | `"Garage / Driver"` |
| Jobs / Meta | `truck.LicensePlate ?? truck.Id` |
| Trend | sparkline |

> **Issue:** "Jobs / Meta" shows a license plate — completely unrelated to the column name.

---

### `TableColumns.Default` — Trailers tab (Company › Trailers, Garage › Trailers, Truck › Trailers)
| Header | Value |
|--------|-------|
| Name | `trailer.LicensePlate ?? trailer.Id` |
| Profit | `$N` |
| Details | `"TrailerType / Garage"` |
| Jobs / Meta | `"N jobs"` |
| Trend | sparkline |

> **Issue:** "Details" works loosely; "Type / Location" would be clearer.

---

### `TableColumns.Default` — Jobs tab (Company › Jobs, Driver › Jobs, Truck › Jobs, Trailer › Jobs, City › Jobs)
| Header | Value |
|--------|-------|
| Name | `job.Cargo ?? job.Id` |
| Profit | `$N` |
| Details | `"SourceCity to TargetCity"` |
| Jobs / Meta | `job.TimestampDay?.ToString() ?? "-"` |
| Trend | *(empty — jobs have no per-row sparkline)* |

> **Issues:**
> - "Details" shows a route, not "details".
> - "Jobs / Meta" shows a game day number.
> - The Trend column is always empty; it wastes space.

---

### `TableColumns.Default` — Driver › Garages tab (garage assignment history)
| Header | Value |
|--------|-------|
| Name | `GarageName` (display name of the garage) |
| Profit | `"Current"` or `"Past"` ← **actively wrong** |
| Details | `assignment.EffectiveFromSaveName` |
| Jobs / Meta | `assignment.EffectiveToSaveName ?? "-"` |
| Trend | *(empty)* |

> **Issues:**
> - "Profit" shows a status string. This is the worst mismatch: the Profit column is a money column but renders "Current" or "Past".
> - "Details" shows an opaque save-file name, not "details".
> - "Jobs / Meta" shows a save-file name.
> - Trend column is empty.

---

### `TableColumns.Default` — City › Routes tab
| Header | Value |
|--------|-------|
| Name | `"Origin to Destination"` |
| Profit | `$N` route profit |
| Details | `"N jobs"` |
| Jobs / Meta | `"X.XX/mi"` (profit per mile) |
| Trend | *(empty)* |

> **Issues:**
> - "Details" shows a job count; "Jobs" or "Job Count" would be accurate.
> - "Jobs / Meta" shows $/mile — a money rate with no relationship to "jobs".
> - Trend column is empty.

---

### `TableColumns.Cities` — Company › Cities tab
| Header | Value |
|--------|-------|
| City | `city.DisplayName` |
| Garage | `"Owned"` or `"-"` |
| Eligible | `"Yes"` or `"No"` |
| Visits | visit count |
| Outbound | `$N` outbound profit |
| Inbound | `$N` inbound profit |
| Total | `$N` outbound + inbound |
| Expansion | expansion score |

> **Issue:** "Total" is outbound + inbound (all traffic). The `Profit`/`ProfitSort` fields on the row are set to `BidirectionalProfit` (only routes with return coverage), which doesn't appear as a column. Consider whether "Total" vs bidirectional distinction should be surfaced, or if "Total" is the right label for outbound+inbound.

---

## Issues summary

| Tab | Column | Current Header | Actual Content | Severity |
|-----|---------|---------------|----------------|----------|
| All Jobs tabs | Jobs / Meta | "Jobs / Meta" | Game day number | Medium |
| All Jobs tabs | Details | "Details" | Route string | Medium |
| All Jobs tabs | Trend | "Trend" | *(always empty)* | Low |
| Garages | Jobs / Meta | "Jobs / Meta" | `$N/day` | Medium |
| Trucks | Jobs / Meta | "Jobs / Meta" | License plate | High |
| Driver › Garages | Profit | "Profit" | "Current" / "Past" | **Critical** |
| Driver › Garages | Details | "Details" | EffectiveFromSaveName | High |
| Driver › Garages | Jobs / Meta | "Jobs / Meta" | EffectiveToSaveName | High |
| Driver › Garages | Trend | "Trend" | *(always empty)* | Low |
| City › Routes | Details | "Details" | Job count | Medium |
| City › Routes | Jobs / Meta | "Jobs / Meta" | $/mile rate | High |
| City › Routes | Trend | "Trend" | *(always empty)* | Low |

---

## Proposed per-tab column sets

Replace generic column definitions with entity-specific ones. The binding paths on `GridRowViewModel` already carry all the required data; only the headers and the column set selected per-tab need to change. In some cases a new `GridRowViewModel` property or a new named column set is needed.

### Garages columns
| Header | Binding |
|--------|---------|
| Name | `Name` |
| Profit | `Profit` (sort: `ProfitSort`) |
| Staff | `Detail` ("N drivers / N trucks") |
| Avg/Day | `Secondary` |
| Trend | `Trend` (sparkline) |

### Drivers columns
| Header | Binding |
|--------|---------|
| Name | `Name` |
| Profit | `Profit` (sort: `ProfitSort`) |
| Assignment | `Detail` ("Garage / Truck") |
| Jobs | `Secondary` |
| Trend | `Trend` (sparkline) |

### Trucks columns
| Header | Binding |
|--------|---------|
| Name | `Name` |
| Profit | `Profit` (sort: `ProfitSort`) |
| Assignment | `Detail` ("Garage / Driver") |
| Plate | `Secondary` |
| Trend | `Trend` (sparkline) |

### Trailers columns
| Header | Binding |
|--------|---------|
| Name | `Name` |
| Profit | `Profit` (sort: `ProfitSort`) |
| Type / Location | `Detail` |
| Jobs | `Secondary` |
| Trend | `Trend` (sparkline) |

### Jobs columns (no trend column)
| Header | Binding |
|--------|---------|
| Cargo | `Name` |
| Profit | `Profit` (sort: `ProfitSort`) |
| Route | `Detail` |
| Day | `Secondary` |

### Garage Assignments columns (Driver › Garages tab)
| Header | Binding |
|--------|---------|
| Garage | `Name` |
| Status | `Profit` ("Current" / "Past") — or rename field |
| From | `Detail` |
| Until | `Secondary` |

> **Note:** Alternatively, keep the `Profit` column as a money column and introduce a separate `Status` field on `GridRowViewModel`, or build a dedicated `AssignmentRowViewModel`.

### Routes columns (City › Routes tab)
| Header | Binding |
|--------|---------|
| Route | `Name` |
| Profit | `Profit` (sort: `ProfitSort`) |
| Jobs | `Detail` |
| $/Mile | `Secondary` |

---

## Implementation approach

1. Add new `static readonly IReadOnlyList<TableColumnViewModel>` entries to `TableColumns` (in `Rows.cs`) for each entity type.
2. Thread the correct column set through each `DetailTabViewModel` constructor call in `DetailViewModels.cs`.
3. The `Driver › Garages` tab needs the most care: the "Profit" column currently renders a string. Either:
   - Accept it under a renamed header ("Status"), or
   - Add a `Status` property to `GridRowViewModel` and create a dedicated `AssignmentColumns` set.
4. Drop the `Trend` column from Jobs and Routes column sets (or make the `DataGridTemplateColumn` collapse when the collection is empty).
