# Trailer Natural Key — Design Spec
**Date:** 2026-05-30  
**Status:** Phase 1 ready to implement

---

## Problem

ATS save files identify trailer units via a `unit_id` (e.g. `_nameless.26b.dd5f.13a0`) which is a
**memory pointer address** re-assigned by the game engine on every load/save cycle. The same physical
trailer (e.g. license plate `200B-420 Texas`) has appeared under 42 different `unit_id` values across
historical snapshots in `bronze_sii_units`.

The current silver layer uses `unit_id` as the primary key for `silver_trailers` and as the FK stored
in `silver_jobs` / `gold_job_details`. This causes:

1. **Unstable URLs** — `/companies/{companyId}/trailers/{unit_id}` breaks silently after every
   re-ingestion when the game reassigns the pointer.
2. **No cross-snapshot identity** — there is no way to know that two unit_ids from different saves
   refer to the same physical trailer.
3. **Broken job history aggregation** — if the game reassigns a trailer's unit_id between sessions,
   historical job profits cannot be correctly attributed.

The **stable identifier** for a player-owned trailer is its **license plate**, which is stored in
`bronze_sii_units.scalar_values_json ->> '$.license_plate'` and is already extracted for trucks
via `CleanLicensePlate()` in `StatisticsProjection`.

---

## Approach: Surrogate Keys + Candidate Key on License Plate

Use integer surrogate primary keys (SQLite ROWID / auto-increment) throughout the silver layer:

- `silver_companies` gets a surrogate integer PK. The existing text `company_id` slug (e.g.
  `tgcitw-parnell`) becomes a `UNIQUE` candidate key.
- `silver_trailers` gets a surrogate integer PK. Its candidate key is
  `UNIQUE (company_pk, license_plate)` where `company_pk` is the FK to the company surrogate.

This gives trailers a **single-column stable identity** (`silver_trailers.id`) that survives
unit_id churn, while retaining the natural candidate key for deduplication at ingestion time.

---

## Current Architecture (before changes)

### silver_trailers schema
```sql
CREATE TABLE silver_trailers (
    company_id    TEXT NOT NULL,
    trailer_id    TEXT NOT NULL,   -- volatile unit_id, e.g. _nameless.26b.dd5f.13a0
    trailer_type  TEXT NOT NULL,
    profit        INTEGER NOT NULL,
    job_count     INTEGER NOT NULL,
    body_type     TEXT,
    is_articulated INTEGER,
    garage_id     TEXT,
    PRIMARY KEY (company_id, trailer_id)
);
```

### silver_companies schema
```sql
CREATE TABLE silver_companies (
    company_id        TEXT NOT NULL PRIMARY KEY,
    display_name      TEXT NOT NULL,
    last_updated_utc  TEXT NOT NULL
);
```

### Domain model: TrailerStatistic
```csharp
public sealed record TrailerStatistic(
    string Id,           // = unit_id (volatile)
    string TrailerType,
    long Profit,
    int JobCount,
    bool IsArticulated = false,
    string? BodyType = null,
    string? GarageId = null);
```

### Contract: TrailerDto
```csharp
public sealed record TrailerDto(
    string Id,           // = unit_id (volatile)
    string TrailerType,
    long Profit,
    int JobCount,
    bool IsArticulated = false,
    string? BodyType = null,
    long ProfitPerDay = 0,
    SparklineDto? Trend = null,
    string? GarageId = null);
```

### URL routing
```
/companies/{CompanyId}/trailers/{TrailerId}   -- TrailerId = unit_id today
```

### silver_jobs / gold_job_details
Both tables store `trailer_id TEXT` which is the volatile unit_id captured at the time the mission
was in the save file. Within a single snapshot the join is consistent; across snapshots it is not.

---

## Phased Implementation Plan

### Phase 1 — Company surrogate key ✅ **(start here)**
**Scope:** DB foundation only. No URL changes, no screen changes.

Changes:
- `silver_companies`: add `id INTEGER PRIMARY KEY` (auto-increment), add `UNIQUE (company_id)` on
  the text slug.
- Use `EnsureColumnAsync` pattern for additive schema migration; full rebuild handles the rest since
  silver/gold tables are deleted and rebuilt on every ingestion.
- No changes to other silver tables yet — they continue to use the text `company_id` FK.

Goal: The surrogate exists and is populated so Phase 2 can immediately reference it as a FK.

---

### Phase 2 — Trailer natural key + surrogate
**Scope:** Extract license plate, stabilise trailer identity, fix trailer URLs.

Changes:
- `StatisticsProjection.BuildTrailerStats`: read `license_plate` from trailer SiiUnit using
  `CleanLicensePlate(FirstKnownValue(trailer, "license_plate"))`. Trailers without a license plate
  are still included but with a `null` / empty plate (skip natural-key dedup for those).
- `TrailerStatistic`: add `string? LicensePlate` field.
- `TrailerDto`: add `string? LicensePlate` field.
- `silver_trailers`: add `company_pk INTEGER` (FK → silver_companies.id), add `license_plate TEXT`,
  add `UNIQUE (company_pk, license_plate)`, add `id INTEGER PRIMARY KEY`.
- Insert: populate `company_pk` from the company surrogate; use `license_plate` for dedup.
- `TrailerDetail` route: change `{TrailerId}` → `{LicensePlate}` (URL-encode the plate for routing).
- `DashboardViewModel.FindTrailer`: match on `LicensePlate` instead of `Id`.
- `StatisticsDashboardMapper`: use license plate as the entity_id for trailer sparklines.

---

### Phase 3 — Stabilise job↔trailer links
**Scope:** Make job history survive save-to-save unit_id churn.

Changes:
- `silver_jobs` / `gold_job_details`: add `trailer_pk INTEGER` (FK → silver_trailers.id).
- At ingestion, resolve the current unit_id → surrogate via the just-inserted silver_trailers rows.
- Trailer job-history aggregation uses the surrogate, not the text unit_id.
- Trailer detail page job list works correctly across save files.

---

### Phase 4 — Cascade company surrogate FK outward *(optional, deferred)*
**Scope:** Consistency cleanup. No user-visible changes.

Changes:
- Replace text `company_id` FK in `silver_drivers`, `silver_trucks`, `silver_garages`,
  `silver_jobs`, `silver_trailer_types`, `silver_cities`, `silver_routes`,
  `gold_*` tables with the integer company surrogate.

---

## Key Implementation Notes

- **Silver/gold tables are fully deleted and rebuilt on every ingestion** (see
  `SqliteMedallionSaveSnapshotSource.cs` line ~1206). Schema migrations for new columns use
  `EnsureColumnAsync` (additive only); structural changes (new PKs, new FKs) are handled by the
  full rebuild.
- **`CleanLicensePlate()`** already exists in `StatisticsProjection.cs` and handles the
  `plate|state` format used by ATS (e.g. `200B-420|texas` → `200B-420 Texas`).
- **Trucks already follow this pattern**: `silver_trucks` stores `license_plate TEXT` (added via
  `EnsureColumnAsync`) and `TruckStatistic` / `TruckDto` carry it. Trailers should mirror that.
- **bronze_sii_units** is a historical append-only log keyed by `(save_id, unit_ordinal)`. It is
  **not** changed by any phase — it is the raw source of truth.

---

## Files Touched Per Phase

### Phase 1
| File | Change |
|------|--------|
| `SqliteMedallionSaveSnapshotSource.cs` | Add `id INTEGER PRIMARY KEY` + `UNIQUE (company_id)` to silver_companies DDL; populate on insert |

### Phase 2
| File | Change |
|------|--------|
| `StatisticsProjection.cs` | Extract `license_plate` from trailer unit |
| `StatisticsModels.cs` | Add `LicensePlate` to `TrailerStatistic` |
| `StatisticsDtos.cs` | Add `LicensePlate` to `TrailerDto` |
| `StatisticsDashboardMapper.cs` | Pass `LicensePlate` through; use as sparkline entity_id |
| `SqliteMedallionSaveSnapshotSource.cs` | Add `id`, `company_pk`, `license_plate` to silver_trailers; update INSERT/SELECT |
| `TrailerDetail.razor` | Change route + lookup parameter |
| `DashboardViewModel.cs` | `FindTrailer` matches on `LicensePlate` |
| `StatisticsClient.cs` | Any URL builder for trailer detail links |
| Tests | Update affected test fixtures |

### Phase 3
| File | Change |
|------|--------|
| `SqliteMedallionSaveSnapshotSource.cs` | Add `trailer_pk` to silver_jobs / gold_job_details; resolve at ingestion |
| `StatisticsProjection.cs` | Thread surrogate through mission aggregation |
| Tests | Update affected test fixtures |
