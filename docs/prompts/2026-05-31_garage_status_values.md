# Garage Status Values in ATS Save Files

## The Nuance

ATS save files include a `garage` unit for every city slot in the game, whether or not
the player owns it. The `status` scalar on that unit encodes the ownership level:

| `status` value | Meaning |
|---|---|
| `0` | Locked / not yet accessible (region DLC not active, or initial state) |
| `1` | Available to purchase — the slot exists but the player does not own it. This is also the state **after selling a garage**. |
| `2` | Owned — small garage |
| `3` | Owned — large garage (upgraded) |
| `null` / absent | Owned — seen in older save files that predate explicit status tracking |

### What "selling" looks like in the bronze layer

When the player sells a garage, ATS does **not** remove the garage unit from the save
file. It resets `status` to `1` and nulls out all driver and vehicle slots, while leaving
the slot-count scalars (`drivers`, `vehicles`) at their previous capacity:

```
// Before selling (status 3, large owned garage)
garage : garage.sacramento {
  vehicles: 5
  drivers: 5
  status: 3
  vehicles[0]: _nameless.27e.02a7.ed60
  vehicles[1]: _nameless.27e.02a8.0de0
  ...
  drivers[0]: driver.23
  ...
}

// After selling (status 1, available to buy again)
garage : garage.sacramento {
  vehicles: 5
  drivers: 5
  status: 1
  vehicles[0]: null
  vehicles[1]: null
  ...
  drivers[0]: null
  ...
}
```

The `vehicles` and `drivers` scalars are the **slot capacity**, not the current count of
assigned vehicles or drivers. All entries in the arrays become `null` after selling.

## Why This Matters for `IsOwnedGarage`

The original `IsOwnedGarage` check was:

```csharp
return status is null || !StringComparer.OrdinalIgnoreCase.Equals(status, "0");
```

This incorrectly treats `status: 1` (available to purchase / just sold) as owned, because
`1 != 0`. The correct check is:

```csharp
if (status is null) return true;          // backward compat for old saves
return int.TryParse(status, out var v) && v >= 2;
```

Only status values `>= 2` indicate that the player currently owns the garage.

## The Historical Garage Problem

Because `allOwnedGarages` sweeps **all** snapshots to build the historical garage list,
older snapshots where Sacramento had `status: 2` or `status: 3` will still contribute a
`GarageStatistic` record for Sacramento even after it is sold. This record has
`isCurrent = false` so its profit, employee count, and truck count all show as zero —
correct for the Garages tab (which intentionally shows historical garages).

However, without the companion fix in `BuildCityStats`, that historical `GarageStatistic`
entry would cause the city's `HasOwnedGarage` flag to remain `true` after the sale.
The fix filters `garageStats` to only currently-owned garages before passing to
`BuildCityStats`:

```csharp
var currentGarageStats = garageStats.Where(g => currentGarageIds.Contains(g.Id)).ToList();
var cityStats = BuildCityStats(missionStats, currentGarageStats, routeStats, garageEligibleCityIds);
```

Both fixes are required together. `IsOwnedGarage` alone would still let the historical
snapshot data bleed through into `HasOwnedGarage`.
