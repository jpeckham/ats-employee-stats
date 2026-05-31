# Driver Names from Locale: Mapping Research

## Background

ATS hired-driver records in save files are identified by `driver.N` (e.g. `driver.208`).
The `silver_drivers` table stores these IDs as-is; `display_name` currently shows the raw
ID because no locale lookup is wired up.

We investigated where the human-readable names come from.

## What the Locale File Contains

`locale/en_gb/driver_names.sii` (8,692 bytes) — only exists in `en_gb`, not `en_us`.

Unit type: `driver_names : .driver.names`  
(NOT `localization_db` — current `LoadLocaleAsync` queries `unit_type = 'localization_db'` so
this unit is invisible to the existing lookup path.)

```
SiiNunit
{

driver_names : .driver.names
{
    # When adding new driver with index X,
    # make sure there is a corresponding material in /material/ui/driver/X.mat.
    # Prefix female names with +.

    name[00]: "Bronislaw E."
    name[01]: "Ian P."
    name[02]: "Cameron S."
    ...
    name[10]: "+Sophie I."    ← female: + prefix
    ...
    name[354]: "Jacob N."
}
}
```

- 355 entries, indexed `name[0]` through `name[354]`
- Explicit numeric indices (not append syntax)
- Female driver names are prefixed with `+`; the `+` is NOT part of the display name

## Correlation with `silver_drivers`

| Metric | Value |
|---|---|
| Entries in `driver_names.sii` | 355 (indices 0–354) |
| Distinct driver IDs in `silver_drivers` | 174 |
| Min driver numeric ID | 1 |
| Max driver numeric ID | 354 |

The max driver index (354) matches the max `name[]` index exactly.

## Conjecture (user, 2026-05-31)

> The numeric part of `driver.N` IS the 0-based index into the `name[]` array.
>
> `driver.23` → `name[23]` → `"+Lucy L."` → display name `"Lucy L."`  
> `driver.208` → `name[208]` → whatever is at that index

This needs **visual in-game verification**: hire driver `driver.23`, check if their
in-game name matches `name[23]: "+Lucy L."` from the file.

## Why the Current Implementation Doesn't Work

`ApplyReferenceDriverNamesAsync` calls:
```csharp
var locale = await LoadLocaleAsync(connection, "%driver_names%", cancellationToken);
```

`LoadLocaleAsync` queries:
```sql
select array_values_json from bronze_reference_sii_units
where unit_type = 'localization_db' and relative_path like '%driver_names%'
```

Two problems:
1. `driver_names.sii` has unit type `driver_names`, not `localization_db` — the WHERE clause
   never matches.
2. Even if it did, the lookup is by key string (`locale["driver.23"]`) but the file stores
   names by array index, not by a driver-id key. There is no `key[]: "driver.23"` entry.

## Proposed Fix (pending in-game verification)

### 1. Parse `driver_names.sii` into bronze

Add `driver_names.sii` to the ingested file list in `IngestReferenceDataAsync`.
The file already uses explicit `name[N]` syntax which the parser handles.
Its unit type will be stored as `driver_names` in `bronze_reference_sii_units`.

### 2. New `ApplyReferenceDriverNamesAsync` logic

Instead of a key-string lookup, extract the numeric index from `driver.N` and look up
`name[N]` directly in the stored JSON array:

```csharp
// Pseudocode
var unit = bronze_reference_sii_units WHERE unit_type = 'driver_names'
              AND relative_path LIKE '%en_gb%'

var nameArray = JsonDeserialize(unit.array_values_json)["name"];
// nameArray[208] → "+Lucy L." or "Bob S."

foreach driver in silver_drivers:
    var n = int.Parse(driver.driver_id.Replace("driver.", ""));
    if (n < nameArray.Count):
        var raw = nameArray[n];           // e.g. "+Lucy L."
        var display = raw.TrimStart('+'); // strip female prefix
        UPDATE silver_drivers SET display_name = display WHERE driver_id = driver.driver_id
```

### 3. Female name prefix

Strip the leading `+` before storing as `display_name`. The `+` is a locale convention
for tagging female names (to select the correct pronoun in localised strings) and should
not appear in the UI.

## Files Referenced

| File | Location in archive |
|---|---|
| `driver_names.sii` | `locale/en_gb/driver_names.sii` |
| Implementation | `SqliteMedallionSaveSnapshotSource.cs` → `ApplyReferenceDriverNamesAsync` |
| Bronze table | `bronze_reference_sii_units` (unit_type = `driver_names`) |
| Silver table | `silver_drivers.display_name` |
