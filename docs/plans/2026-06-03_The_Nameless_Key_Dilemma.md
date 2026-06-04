# The `_nameless` Key Dilemma

## What are `_nameless` IDs?

ATS and ETS2 save files use a flat unit format where every object has a type and an ID. Entities with no semantic name — `profit_log_entry`, `delivery_log_entry`, and similar log records — get auto-generated IDs like `_nameless.23c.c0cd.dd40`. The hex segments encode a memory address or allocation slot from the game process at save time. They are **not stable**: the same logical job will carry a different `_nameless` ID in every save file.

## Where the problem surfaces

`silver_jobs` uses `job_id` as part of its primary key, and that column is populated directly from the unit ID:

```sql
primary key (company_id, job_id)
```

```csharp
("$job_id", mission.Id),   // mission.Id == SiiUnit.Id == "_nameless.23c.c0cd.dd40"
```

So `silver_jobs` (and by extension `gold_*` tables that join on it) treats these ephemeral addresses as stable row identities.

## Why it hasn't caused obvious breakage

The bronze → silver pipeline clears and rewrites silver/gold on every ingest, so stale IDs don't accumulate. And the deduplication logic in `StatisticsProjection.BuildSnapshotMissions` never relies on the unit ID for cross-snapshot identity — it uses a semantic composite key instead:

```csharp
// For delivery_log_entry / profit_log_entry:
string.Join('|', source.Type, sourceCity, targetCity, cargo, truckId, trailerType, profit)

// For job units:
source.Id   // ← still the raw _nameless ID, but job units are only present in one snapshot
```

So the merge/dedup step is correct. The problem is only that the persisted `job_id` column carries a meaningless value.

## Concrete consequences

1. **No stable external reference.** You cannot bookmark or link to a specific job by ID because the ID will change on the next save.

2. **Dedup key instability for player jobs (post-enrichment).** `profit_log_entry` missions use the semantic key, which now includes `source_city`, `target_city`, and `cargo` after the delivery-log enrichment fix. Before enrichment those fields were empty, so the key was `profit_log_entry||||unknown|218298`. After enrichment it becomes `profit_log_entry|leipzig|kouvola|med_vaccine|unknown|218298`. If a job drops out of the delivery log's rolling window between two save snapshots, one snapshot produces the enriched key and the other the bare key — they won't dedup against each other and the job appears twice.

3. **Potential double-count from delivery_log_entry.** The economy's `delivery_log_entry` units are also ingested as missions (with `driverId = null`). They represent the same physical jobs as the player's `profit_log_entry` missions. Currently they survive dedup as separate rows because their keys differ by type prefix (`delivery_log_entry|...` vs `profit_log_entry|...`) and the profit field uses a different value (`params[22]` vs gross revenue). Neither unit is suppressed.

## Options

### A — Accept the status quo
The `_nameless` ID in `silver_jobs.job_id` is cosmetic; no feature currently relies on it being stable. The semantic dedup key handles cross-snapshot merging correctly for the common case. Low effort, low risk.

### B — Derive a stable synthetic job ID
Replace the raw unit ID with a hash of the semantic dedup key before writing to silver:

```csharp
var stableId = Convert.ToHexString(
    SHA256.HashData(Encoding.UTF8.GetBytes(deduplicationKey)))[..16];
```

The same job would then get the same `job_id` across saves as long as its dedup fields (type, cities, cargo, truck, trailer type, profit) are stable. Breaks if any of those fields change between saves (e.g. enrichment state differs).

### C — Suppress delivery_log_entry missions and rely solely on profit_log_entry
Exclude `delivery_log_entry` units from the global mission pipeline and instead only use them as a route-data lookup source (which is what the current enrichment fix does for recent jobs). This eliminates the double-count and reduces the number of null-driver ghost rows in silver.

**Trade-off:** loses the delivery_log_entry as an independent profit source for cases where the corresponding profit_log_entry is absent (e.g. very old jobs that have rolled off the profit_log).

### D — Correlate delivery_log_entry ↔ profit_log_entry and emit one merged mission
Use the same `(timestamp_day, revenue)` correlation already used for enrichment to treat matching pairs as a single mission. Emit only the profit_log_entry side (for accurate financials) and annotate it with the route fields from the delivery_log_entry side.

This is the most correct model but requires careful handling of the cases where no delivery_log_entry match exists.

## Current state (2026-06-03)

The route-data enrichment fix (reading `delivery_log_entry` params to fill in `source_city`, `destination_city`, and `cargo` for player `profit_log_entry` missions) is in place. The double-count and unstable-ID problems are **known but unaddressed**. Option C or D would be the natural next step.
