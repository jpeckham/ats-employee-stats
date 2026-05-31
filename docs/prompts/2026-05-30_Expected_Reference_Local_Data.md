# Expected Reference / Locale Data from SCS Files

## Background

ATS save files use internal non-human-readable identifiers for all entities. The friendly,
localized display names (e.g. "International Lonestar T 115", "Los Angeles", "Flatbed") live
separately inside the game's SCS resource archives — primarily `locale.scs` and potentially
vehicle/cargo definition `.scs` files in the game install root.

The SCS archive format is a proprietary hash-based container. SCS Software publishes an official
extraction tool (`scs_extractor.exe`) which can extract most archives, but intentionally refuses
to extract `locale.scs` via the official tool. A community-maintained extractor (CVault Ultimate
Tools, Python-based) handles `locale.scs`. Our current implementation uses the official extractor
downloaded from `https://download.eurotrucksimulator2.com/scs_extractor_1_55.zip`.

Key forum references:
- https://forum.scssoft.com/viewtopic.php?t=331671
- https://forum.scssoft.com/viewtopic.php?p=1939277

## What We Currently Extract

`ScsReferenceDataIngestor.ExtractLocaleAsync()` (ScsReferenceData.cs):
1. Locates `locale.scs` in the game install root.
2. SHA256-hashes the archive to detect version changes.
3. Extracts to a versioned cache directory under `%LOCALAPPDATA%\AtsEmployeeStats\reference-cache\locale\<hash>\`.
4. Writes a `.extracted` marker so re-extraction is skipped on subsequent runs.

`IngestReferenceDataAsync` then walks the extracted output and parses:
- `locale/<lang>/driver_names.sii` — maps driver name keys to display name strings
- `locale/<lang>/local.sii` — maps cargo and city/company keys to display name strings

Both are parsed by `SiiSaveParser` into `SiiUnit` records of type `localization_db` and stored in
`bronze_reference_sii_units`.

## Bronze Layer: Reference Tables

```
bronze_reference_archives
  archive_id        TEXT PK   -- SHA256 hash of the .scs file
  full_path         TEXT      -- absolute path to the .scs archive on disk
  content_hash      TEXT      -- same as archive_id; tracks version
  extracted_time_utc TEXT
  status            TEXT      -- 'ok' | 'error'
  error_message     TEXT

bronze_reference_sii_units
  archive_id        TEXT      -- FK -> bronze_reference_archives
  relative_path     TEXT      -- e.g. "locale/en_us/driver_names.sii"
  unit_ordinal      INTEGER
  unit_type         TEXT      -- always 'localization_db' for locale files
  unit_id           TEXT
  scalar_values_json TEXT     -- JSON object
  array_values_json  TEXT     -- JSON object; "key" and "val" parallel arrays
```

`LoadLocaleAsync` reads `bronze_reference_sii_units` where `unit_type = 'localization_db'` and
deserializes the `key`/`val` parallel arrays into a `Dictionary<string, string>`.

## What Is Currently Applied (Silver Layer)

| Silver Table | Applied By | Key Pattern |
|---|---|---|
| `silver_drivers.display_name` | `ApplyReferenceDriverNamesAsync` | driver id matched against `key` entries in driver_names.sii |
| `silver_jobs.cargo_name` / `gold_job_details.cargo_name` | `ApplyReferenceCargoNamesAsync` | cargo id prefixed with `cn_` (e.g. `cn_milk`) looked up in local.sii |

## What Is NOT Yet Applied (Gaps)

The following entities currently get display names from string-mangling only — not from locale data:

| Entity | Current Workaround | Expected Locale Source |
|---|---|---|
| **Trucks** | `FormatTruckModelName()` — hardcoded brand map + parse from definition path | Vehicle definition `.scs` files or `local.sii` model name keys |
| **Trailers** | Raw type string from save (e.g. `flatbed`) | `local.sii` or trailer definition `.scs` files |
| **Garages** | City slug title-cased (`austin` → "Austin") | `local.sii` — city name keys (e.g. `city.austin`) |
| **Cities** | Same city slug title-casing | `local.sii` — city name keys |

## Intended Bronze Layer Additions

To close these gaps we intend to extract additional SCS archives beyond `locale.scs`:

### `def.scs` / vehicle definition archives
- Contains `.sii` definition files for each truck model and trailer type under `def/vehicle/truck/` and `def/vehicle/trailer_owned/`.
- These files carry the human-readable model name string (or a reference to a locale key).
- Parsed units would be stored in `bronze_reference_sii_units` under a new `relative_path` prefix (e.g. `def/vehicle/truck/<brand>/<model>/data.sii`).

### `local.sii` city and company keys
- Already partially extracted but only applied to cargo names.
- City name keys follow the pattern `city.<slug>` (e.g. `city.los_angeles` → "Los Angeles").
- Company/garage name keys follow `company.<slug>` or `company.permanent.<slug>`.
- These lookups should also update `silver_garages.display_name` and `silver_cities.display_name`.

### Trailer type names
- Trailer type identifiers come from `trailer_definition` / `trailer_def` fields in save units.
- The friendly names live in `local.sii` under keys like `trailer_type.<slug>` or in trailer definition files.
- Intended target: `silver_trailers.trailer_type` (currently stored as raw slug).

## Current Failure Root Cause (Investigated 2026-05-30)

### Error in `bronze_reference_archives`

```
archive_id: locale-scs-extraction-failed
status:     failed
error_msg:  scs_extractor.exe failed with exit code 1:
            *** ERROR *** : Root directory not found, can not extract this archive!
```

The official `scs_extractor.exe` intentionally refuses `locale.scs` because it uses
**HashFS v2 with CityHash** — a newer format the official tool does not support.
The `bronze_reference_sii_units` table is therefore empty.

### `locale.scs` File Format (HashFS v2 / "CITY")

Binary header (49 bytes total):

| Field | Type | Value (actual locale.scs) |
|---|---|---|
| magic | char[4] | `SCS#` |
| version | uint16 | 2 |
| salt | uint16 | 0 |
| hash_method | char[4] | `CITY` |
| num_entries | uint32 | 522 |
| entry_table_length | uint32 | 5 693 (compressed) |
| num_metadata_entries | uint32 | 2 610 |
| metadata_table_length | uint32 | 5 423 (compressed) |
| entry_table_start | uint64 | 0x00B4C420 (near end of file) |
| metadata_table_start | uint64 | 0x00B4DA60 |
| security_descriptor_offset | uint32 | 0x80 |
| platform | byte | 0 |

Key structural facts:
- File data lives at the **start** of the archive; the entry and metadata tables are
  at the **end** and are both **zlib-compressed**.
- The entry table is a flat array of `EntryTableEntry` structs
  (`hash:uint64`, `metadata_index:uint32`, `metadata_count:uint8` or similar packed fields);
  entries are cast directly via `MemoryMarshal.Cast` after decompression.
- The metadata table is walked per entry; each metadata block starts with a
  3-byte index + 1-byte `MetadataChunkType`, followed by the main metadata
  (`offset:uint64`, `compressed_size:uint32`, `size:uint32`, `is_compressed:bool`).
- File paths are **not stored** in the archive; files are looked up by
  `CityHash64(path)` (salt = 0 for this archive → hash is plain CityHash64 of the path string).

### Fix: Replace `scs_extractor.exe` with Native C# HashFS v2 Reader

The library **`TruckLib.HashFs`** (NuGet, sk-zk/TruckLib.HashFs, v0.2.6, targets .NET 10)
implements a complete HashFS v1+v2 reader in C# including the CityHash and
decompression logic.  It has dependencies: `GisDeflate`, `System.IO.Hashing`,
and `TruckLib.Models`.

**Proposed approach:**

1. Add `TruckLib.HashFs` as a package reference in `AtsEmployeeStats.Infrastructure.csproj`.
2. Replace (or complement) `ProcessScsArchiveExtractor` with a new
   `ScsHashFsExtractor : IScsArchiveExtractor` that:
   - Opens the `.scs` with `HashFsReader.Open(path)`
   - Recursively walks the directory listing starting from `/`
   - Writes the files we care about (`locale/<lang>/driver_names.sii`,
     `locale/<lang>/local.sii`) directly into `outputDirectory`
   - Writes the `.extracted` marker on success
3. `ScsExtractorBootstrapper` / the exe download path would become a no-op
   (or remain as fallback for def.scs extraction).
4. Because the new extractor is pure C# it works on all platforms with no
   external process.

**Alternative:** Implement CityHash64 and the entry/metadata parsing inline
(no extra NuGet dependency) — feasible given the format is now fully understood,
but more code to maintain.

## Application Order in IngestAsync

The reference data application should run after silver tables are populated and before gold
aggregates are built:

1. `IngestReferenceDataAsync` — extract + parse SCS archives → `bronze_reference_sii_units` ✓ **DONE 2026-05-31**
2. `ApplyReferenceDriverNamesAsync` — driver names — **no locale file found** (locale.scs has no driver_names.sii for en_us; en_gb has one but IDs like driver.208 don't match)
3. `ApplyReferenceCargoNamesAsync` — cargo names ✓ **WORKING** — loads from `%en_us%` locale units; cargo slugs matched via `cn_<slug>` key in `localization.sui` / `local.override.sii`
4. `ApplyReferenceCityNamesAsync` — city names ✓ **DONE 2026-05-31** — updates `silver_cities.display_name` and `silver_garages.display_name` via `garage.` prefix strip
5. `ApplyReferenceTrailerTypeNamesAsync` — **to be added** — update `silver_trailers.trailer_type`
6. `ApplyReferenceTruckModelNamesAsync` — **to be added** — update `silver_trucks.model_name` (or replace `FormatTruckModelName()` entirely)

## Implementation Notes (2026-05-31)

### Files Parsed from locale.scs

Per language (en_us used for display names):
- `locale/<lang>/local.sii` — small inline keys (currency symbols, game_name); uses `{` on separate line (normalized before parsing)
- `locale/<lang>/local.override.sii` — main ATS-specific keys (cities, accessories, companies); uses append `key[]:` syntax
- `locale/<lang>/localization.sui` — large fragment (~560KB) with cargo names (`cn_*`) and general strings; wrapped in synthetic `localization_db` unit before parsing

Key parser changes made:
- `SiiSaveParser.FieldRegex`: changed `\d+` → `\d*` to handle append-syntax `key[]:` (empty brackets)  
- `SiiSaveParser.NormalizeSeparateBrace()`: normalizes `type : id\n{` → `type : id {` for locale files where brace is on separate line
- Both changes are targeted to locale files and do not affect save file parsing

### City Key Format

City IDs in `silver_cities` are raw slugs (`los_angeles`, `phoenix`). The locale uses the SAME slug as the key (not `city.los_angeles` as originally assumed in the doc). Direct lookup: `locale["los_angeles"] → "Los Angeles"`.

### Pre-existing Test Failure

`StatisticsService_persists_city_route_trailer_and_trend_read_models` — `trailer.JobCount == 2` expected but `0` returned. This is unrelated to reference data; it's a regression from the license-plate re-keying commits that changed how trailers are attributed to jobs. The test data needs a `license_plate` field added to the trailer unit.
