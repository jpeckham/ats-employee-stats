# Fix: Expansion Score Inflated by Prior Garage Cities

## Problem

Cities where you previously owned a garage show artificially high expansion scores after the garage is sold/removed.

The expansion score formula is:

```
outbound.Count + inbound.Count + (outbound.Sum(profit) / 10000)
```

It zeros out if the city **currently** has a garage (`hasOwnedGarage`). That check is present-state only. While Roswell (for example) had a garage, Roswell-based drivers generated high outbound job counts — many jobs originated from that city because trucks were stationed there. Those historical missions remain in the all-time mission list. Once the garage is removed, `hasOwnedGarage` becomes `false` and Roswell suddenly accumulates a large expansion score from traffic that was garage-driven, not organic demand.

## Root Cause

Each `MissionStatistic` carries a `GarageId` (e.g., `garage.roswell`). The city slug after the dot (`roswell`) identifies which city's garage dispatched the job. When a job's `GarageId` points to the same city as its `SourceCity`, the outbound activity was generated because a garage existed there — not because the city is an independently strong expansion candidate.

The expansion score currently does not use `GarageId` at all.

## Fix

Filter the outbound missions used in the expansion score to exclude jobs where the driver's home garage is in the same city as the job's origin:

```
expansionOutbound = outbound where (GarageId is null OR ExtractGarageCitySlug(GarageId) != sourceCity)
expansionScore = expansionOutbound.Count + inbound.Count + (expansionOutbound.Sum(profit) / 10000)
```

This leaves organic outbound traffic (jobs starting from the city but attributed to a different garage) in the signal, while stripping garage-driven outbound history.

`VisitCount`, `OutboundProfit`, `InboundProfit`, and `BidirectionalProfit` are **not changed** — they continue to reflect all-time totals. Only the expansion score is filtered.

Inbound counts are not adjusted: deliveries arriving at a city from other companies' drivers are legitimate demand regardless of whether the city ever had a garage.

## Change Locations

The expansion score is computed in two places that must be updated together.

### 1. `StatisticsProjection.cs` — in-memory path

`ExtractGarageCitySlug` is already a private static helper in this class.

Around line 718, replace:

```csharp
var expansionScore = (hasOwnedGarage || !isGarageEligible)
    ? 0m
    : Math.Round(outbound.Count + inbound.Count + (outbound.Sum(mission => mission.Profit) / 10000m), 2, MidpointRounding.AwayFromZero);
```

With:

```csharp
var expansionOutbound = outbound
    .Where(m => m.GarageId is null ||
                !StringComparer.OrdinalIgnoreCase.Equals(ExtractGarageCitySlug(m.GarageId), city))
    .ToList();
var expansionScore = (hasOwnedGarage || !isGarageEligible)
    ? 0m
    : Math.Round(expansionOutbound.Count + inbound.Count + (expansionOutbound.Sum(m => m.Profit) / 10000m), 2, MidpointRounding.AwayFromZero);
```

### 2. `StatisticsDashboardMapper.cs` — active path (time-filtered missions)

This is the path exercised in normal UI operation. `ExtractGarageCitySlug` is not available here — add it as a private static method:

```csharp
private static string? ExtractGarageCitySlug(string garageId)
{
    var dot = garageId.IndexOf('.');
    return dot >= 0 && dot + 1 < garageId.Length ? garageId[(dot + 1)..] : null;
}
```

Around line 368, apply the same outbound filter before the expansion score calculation.

## Verification

After the change, Roswell (and any other ex-garage city) should show a dramatically reduced expansion score reflecting only organic non-garage outbound traffic. Cities that never had a garage should be unaffected. Cities that currently have a garage continue to score 0.
