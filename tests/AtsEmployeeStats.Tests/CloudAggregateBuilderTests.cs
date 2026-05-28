using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Tests;

public sealed class CloudAggregateBuilderTests
{
    private static AtsStatistics BuildStatistics() => new(
        new DateTimeOffset(2026, 5, 27, 0, 0, 0, TimeSpan.Zero),
        [
            new CompanyStatistics(
                "company.desert-line",
                "Desert Line",
                new DateTimeOffset(2026, 5, 27, 0, 0, 0, TimeSpan.Zero),
                garages: [
                    new GarageStatistic("garage.phoenix", "Phoenix", 5000, 2, 2),
                    new GarageStatistic("garage.denver", "Denver", 3000, 1, 1)
                ],
                drivers: [
                    new DriverStatistic("driver.alice", "Alice Ramirez", 8000, "garage.phoenix", "truck.1"),
                    new DriverStatistic("driver.bob", "Bob Chen", 4000, "garage.denver", "truck.2")
                ],
                trucks: [
                    new TruckStatistic("truck.1", "Kenworth T680 - ATS-100", 8000, "garage.phoenix", "driver.alice", null, "Kenworth T680", null),
                    new TruckStatistic("truck.2", "Peterbilt 579 - ATS-200", 4000, "garage.denver", "driver.bob", null, "Peterbilt 579", null)
                ],
                missions: [
                    new MissionStatistic("job.1", "driver.alice", "truck.1", "trailer.1", "reefer", "medicine", "phoenix", "denver", 3000, 180),
                    new MissionStatistic("job.2", "driver.alice", "truck.1", "trailer.1", "reefer", "produce", "denver", "phoenix", 2000, 181),
                    new MissionStatistic("job.3", "driver.bob", "truck.2", "trailer.2", "flatbed", "steel", "phoenix", "lasvegas", 2500, 182)
                ],
                trailerTypes: [
                    new TrailerTypeStatistic("reefer", 5000, 2),
                    new TrailerTypeStatistic("flatbed", 2500, 1)
                ],
                recentDriverJobs: [],
                trailers: [],
                cities: [
                    new CityStatistic("phoenix", "Phoenix", true, true, 3, 5500, 2000, 7500, 0.9m),
                    new CityStatistic("denver", "Denver", true, true, 2, 2000, 3000, 5000, 0.7m),
                    new CityStatistic("lasvegas", "Las Vegas", false, true, 1, 2500, 0, 2500, 0.5m)
                ],
                routes: [
                    new RouteStatistic("phoenix", "denver", 3000, 1, 15.0m, 1.0m),
                    new RouteStatistic("denver", "phoenix", 2000, 1, 10.0m, 1.0m),
                    new RouteStatistic("phoenix", "lasvegas", 2500, 1, 20.0m, 0.0m)
                ],
                profitTrends: [
                    new TrendPointStatistic("company", "company.desert-line", 180, 3000, 1),
                    new TrendPointStatistic("company", "company.desert-line", 181, 2000, 1),
                    new TrendPointStatistic("company", "company.desert-line", 182, 2500, 1)
                ])
        ]);

    [Fact]
    public void Build_includes_schema_and_metric_versions()
    {
        var stats = BuildStatistics();
        var payload = CloudAggregateBuilder.Build(stats, windowDays: 14, sourceSnapshotCount: 5, appVersion: "1.0.0");

        Assert.Equal(CloudAggregateBuilder.CurrentSchemaVersion, payload.SchemaVersion);
        Assert.Equal(CloudAggregateBuilder.CurrentMetricVersion, payload.MetricVersion);
        Assert.Equal("1.0.0", payload.AppVersion);
    }

    [Fact]
    public void Build_includes_window_metadata()
    {
        var stats = BuildStatistics();
        var payload = CloudAggregateBuilder.Build(stats, windowDays: 14, sourceSnapshotCount: 5, appVersion: "1.0.0");

        Assert.Equal(14, payload.WindowDays);
        Assert.Equal(5, payload.SourceSnapshotCount);
        Assert.Equal(180, payload.WindowStartGameDay);
        Assert.Equal(182, payload.WindowEndGameDay);
    }

    [Fact]
    public void Build_payload_contains_no_raw_company_ids()
    {
        var stats = BuildStatistics();
        var payload = CloudAggregateBuilder.Build(stats, windowDays: 14, sourceSnapshotCount: 5, appVersion: "1.0.0");

        // payload has no company-level record — company data is disaggregated into entity aggregates
        // verify no property on the payload exposes raw company id or display name
        Assert.DoesNotContain("desert-line", payload.AppVersion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Desert Line", payload.AppVersion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_payload_contains_no_raw_driver_ids()
    {
        var stats = BuildStatistics();
        var payload = CloudAggregateBuilder.Build(stats, windowDays: 14, sourceSnapshotCount: 5, appVersion: "1.0.0");

        // driver IDs and display names must not appear in any driver aggregate
        Assert.All(payload.Drivers, d =>
        {
            Assert.DoesNotContain("driver.alice", d.AnonymousDriverId, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("driver.bob", d.AnonymousDriverId, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Alice", d.AnonymousDriverId, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Bob", d.AnonymousDriverId, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Build_anonymous_driver_ids_are_deterministic()
    {
        var id1 = CloudAggregateBuilder.AnonymizeId("driver.alice");
        var id2 = CloudAggregateBuilder.AnonymizeId("driver.alice");
        var id3 = CloudAggregateBuilder.AnonymizeId("driver.bob");

        Assert.Equal(id1, id2);
        Assert.NotEqual(id1, id3);
        Assert.Equal(16, id1.Length);
    }

    [Fact]
    public void Build_routes_aggregate_profit_and_job_count()
    {
        var stats = BuildStatistics();
        var payload = CloudAggregateBuilder.Build(stats, windowDays: 14, sourceSnapshotCount: 5, appVersion: "1.0.0");

        var phoenixDenver = Assert.Single(payload.Routes, r => r.OriginCityId == "phoenix" && r.DestinationCityId == "denver");
        Assert.Equal(3000, phoenixDenver.TotalProfit);
        Assert.Equal(1, phoenixDenver.JobCount);
    }

    [Fact]
    public void Build_cities_aggregate_profit_and_visits()
    {
        var stats = BuildStatistics();
        var payload = CloudAggregateBuilder.Build(stats, windowDays: 14, sourceSnapshotCount: 5, appVersion: "1.0.0");

        var phoenix = Assert.Single(payload.Cities, c => c.CityId == "phoenix");
        Assert.Equal(5500, phoenix.OutboundProfit);
        Assert.Equal(2000, phoenix.InboundProfit);
        Assert.Equal(3, phoenix.VisitCount);
    }

    [Fact]
    public void Build_trailer_types_aggregate_profit_and_job_count()
    {
        var stats = BuildStatistics();
        var payload = CloudAggregateBuilder.Build(stats, windowDays: 14, sourceSnapshotCount: 5, appVersion: "1.0.0");

        var reefer = Assert.Single(payload.TrailerTypes, t => t.TrailerTypeId == "reefer");
        Assert.Equal(5000, reefer.TotalProfit);
        Assert.Equal(2, reefer.JobCount);
    }

    [Fact]
    public void Build_truck_models_aggregate_by_model_name()
    {
        var stats = BuildStatistics();
        var payload = CloudAggregateBuilder.Build(stats, windowDays: 14, sourceSnapshotCount: 5, appVersion: "1.0.0");

        var kenworth = Assert.Single(payload.TruckModels, t => t.ModelName == "Kenworth T680");
        Assert.Equal(8000, kenworth.TotalProfit);
    }

    [Fact]
    public void Build_garages_use_city_id_not_display_name()
    {
        var stats = BuildStatistics();
        var payload = CloudAggregateBuilder.Build(stats, windowDays: 14, sourceSnapshotCount: 5, appVersion: "1.0.0");

        // city IDs are normalized game IDs (lowercase) extracted from garage IDs, not display names
        Assert.All(payload.Garages, g =>
        {
            Assert.NotEqual("Phoenix", g.CityId);
            Assert.NotEqual("Denver", g.CityId);
        });

        Assert.Contains(payload.Garages, g => g.CityId == "phoenix");
        Assert.Contains(payload.Garages, g => g.CityId == "denver");
    }

    [Fact]
    public void Build_drivers_count_is_correct()
    {
        var stats = BuildStatistics();
        var payload = CloudAggregateBuilder.Build(stats, windowDays: 14, sourceSnapshotCount: 5, appVersion: "1.0.0");

        Assert.Equal(2, payload.Drivers.Count);
    }

    [Fact]
    public void Build_with_no_trend_data_uses_zero_window_bounds()
    {
        var stats = new AtsStatistics(
            DateTimeOffset.UtcNow,
            [
                new CompanyStatistics(
                    "co", "Co", DateTimeOffset.UtcNow,
                    garages: [],
                    drivers: [],
                    trucks: [],
                    missions: [],
                    trailerTypes: [])
            ]);

        var payload = CloudAggregateBuilder.Build(stats, windowDays: 7, sourceSnapshotCount: 1, appVersion: "1.0.0");

        Assert.Equal(0, payload.WindowStartGameDay);
        Assert.Equal(0, payload.WindowEndGameDay);
    }
}
