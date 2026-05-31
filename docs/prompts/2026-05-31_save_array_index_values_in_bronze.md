# Save Array Index Values in Bronze

## The Problem

SII files store array fields with explicit numeric indices:

```
name[00]: "Bronislaw E."
name[23]: "+Lucy L."
name[208]: "..."
profit_log[0]: 1000
profit_log[1]: 2000
key[]: "cn_milk"          ← append syntax; index assigned sequentially
val[]: "Milk"
```

### What the parser currently does

`SiiSaveParser` accumulates these into `SortedDictionary<int, string>` while parsing,
then calls `ToIndexedList` to produce a `List<string>`:

```csharp
private static List<string> ToIndexedList(SortedDictionary<int, string> values)
{
    var indexed = Enumerable.Repeat(string.Empty, values.Keys.Max() + 1).ToList();
    foreach (var (index, value) in values)
        indexed[index] = value;
    return indexed;
}
```

The list is positional: `list[N]` corresponds to the original `field[N]`. Gaps between
used indices are filled with empty strings. This list is then JSON-serialized into
`array_values_json` in both `bronze_sii_units` and `bronze_reference_sii_units`:

```json
{ "name": ["Bronislaw E.", "Ian P.", "", ..., "+Lucy L.", "", ..., "..."] }
```

The original index numbers **do not appear anywhere in the stored JSON**.

### Why this matters

**For driver names** (the immediate trigger): `driver.N` in a save file refers to a hired
driver. The conjecture is that N is the 0-based index into `name[]` in `driver_names.sii`.
To look up "what is the name of `driver.208`", we need `nameArray[208]`. This only works
if `name[208]` in the original SII ends up at position 208 in the serialized JSON array —
which relies entirely on `ToIndexedList` filling all gaps 0–208 with empty strings and
no JSON layer reordering the array.

**JSON array ordering is guaranteed by spec**, so this is technically safe for a single
round-trip through `System.Text.Json`. However:

1. Relying on gap-filling with empty strings is fragile: any future consumer that skips
   empty entries (e.g. `WHERE ... != ''`) or that serializes through a library that
   compacts sparse arrays would silently produce wrong results.
2. The original index is semantically meaningful data (it IS the driver identity) but
   is not stored as data — it's encoded only as list position.
3. For save-game units (`bronze_sii_units`), arrays like `drivers[0]`, `drivers[1]` in
   a `garage` unit carry the same fragility: the consumer must iterate by position to
   recover the original index.
4. Debugging bronze data is harder: looking at the raw DB you cannot tell whether
   `["", "", ..., "Lucy L."]` has a gap at index 0–22 intentionally or due to a parsing
   bug.

## Proposed Change: Store Indices Explicitly

Change `array_values_json` from a positional array to an **object keyed by string-encoded
integer index**:

### Current format
```json
{
  "name": ["Bronislaw E.", "Ian P.", "", "", ..., "+Lucy L."]
}
```

### Proposed format
```json
{
  "name": { "0": "Bronislaw E.", "1": "Ian P.", "23": "+Lucy L.", "208": "..." }
}
```

No gap-filling. Every stored entry has an explicit, human-readable index. Lookup by
index is `obj["208"]` rather than `array[208]`.

### Parser change

Replace `ToIndexedList` in `SiiSaveParser` with a method that returns
`Dictionary<int, string>` (or `Dictionary<string, string>` with string-encoded keys):

```csharp
// Instead of ToIndexedList returning List<string>:
private static Dictionary<string, string> ToIndexedDict(SortedDictionary<int, string> values)
    => values.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
```

The `SiiUnit.Arrays` type would change from
`Dictionary<string, IReadOnlyList<string>>` to
`Dictionary<string, IReadOnlyDictionary<string, string>>`.

### Consumer changes

**`LoadLocaleAsync`** — currently iterates `keys[i]` / `vals[i]` in parallel by position.
With indexed dicts, iterate by matching index keys:

```csharp
// Proposed
var arrays = JsonDeserialize<Dictionary<string, Dictionary<string, string>>>(json);
if (!arrays.TryGetValue("key", out var keys) || !arrays.TryGetValue("val", out var vals)) continue;
foreach (var (idx, keyName) in keys)
{
    if (vals.TryGetValue(idx, out var valName) && ...)
        result.TryAdd(keyName, valName);
}
```

**`ApplyReferenceDriverNamesAsync`** — look up driver N by string key `"208"`:

```csharp
var arrays = JsonDeserialize<Dictionary<string, Dictionary<string, string>>>(json);
var nameDict = arrays["name"];  // { "0": "Bronislaw E.", "23": "+Lucy L.", ... }
var n = driverId.Replace("driver.", "");  // "208"
if (nameDict.TryGetValue(n, out var raw))
    display = raw.TrimStart('+');
```

**Save-game array consumers** — same pattern: look up by string index rather than list
position.

## Impact

### What needs to change
| Component | Change |
|---|---|
| `SiiSaveParser.ToIndexedList` | Replace with `ToIndexedDict` returning `Dictionary<string, string>` |
| `SiiUnit.Arrays` type | `Dictionary<string, IReadOnlyDictionary<string, string>>` |
| `InsertBronzeUnitsAsync` / `InsertReferenceUnitsAsync` | Serialize new dict type (trivial, same call) |
| `LoadLocaleAsync` | Iterate by matching index key rather than parallel list position |
| All other bronze array readers in silver layer | Replace `array[i]` positional access with dict lookup |
| Schema | No column change; `array_values_json` content format changes |

### Full rebuild required
All existing rows in `bronze_sii_units` and `bronze_reference_sii_units` contain the old
positional-array format. Both tables must be dropped/truncated and all save files and
reference archives re-ingested after the parser change.

- `bronze_sii_units`: re-ingest from existing `.sii` save files on disk (automatic on
  next startup with `force: true` or by deleting the `bronze_save_files` records).
- `bronze_reference_sii_units`: delete `bronze_reference_archives` records and clear
  the locale cache (`reference-cache/locale/`) so extraction + parsing reruns.

### Tests
Unit tests that assert on `array_values_json` content or that call `SiiUnit.Arrays`
positionally will need updating. The `SiiSaveParserTests` and
`ScsReferenceDataIngestionTests` will likely need the deserialization assertions updated
to the new dict format.

## Not Affected

- `scalar_values_json` — stores `Dictionary<string, string>` which already has explicit
  keys; no change needed.
- The SII text format on disk — only the in-memory representation and DB serialization
  change.
- Column names and schema structure — `array_values_json` column stays; content changes.
