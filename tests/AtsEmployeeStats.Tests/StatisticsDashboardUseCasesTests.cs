using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Tests;

public sealed class StatisticsDashboardUseCasesTests
{
    [Fact]
    public async Task GetDashboardAsync_loads_mapped_dashboard()
    {
        var useCases = CreateUseCases();

        var dashboard = await useCases.GetDashboardAsync(new DashboardQueryOptions(), CancellationToken.None);

        Assert.Equal(2, dashboard.Companies.Count);
        Assert.Contains(dashboard.Companies, company => company.Id == "desert-line");
        Assert.Contains(dashboard.Companies, company => company.Id == "mountain-haul");
    }

    [Fact]
    public async Task GetDashboardAsync_filters_companies_to_selected_source_key()
    {
        var useCases = CreateUseCases(new AtsStatistics(
            new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero),
            [
                CreateCompany("ats-506c61796572-autosave:acme", "Acme Trucking"),
                CreateCompany("ets2-45545332-manual:acme", "Acme Trucking")
            ]));

        var dashboard = await useCases.GetDashboardAsync(
            new DashboardQueryOptions(SourceKey: "Ats:506C61796572:autosave"),
            CancellationToken.None);

        var company = Assert.Single(dashboard.Companies);
        Assert.Equal("ats-506c61796572-autosave:acme", company.Id);
    }

    [Fact]
    public async Task ListCompaniesAsync_returns_dashboard_companies()
    {
        var useCases = CreateUseCases();

        var companies = await useCases.ListCompaniesAsync(new DashboardQueryOptions(), CancellationToken.None);

        Assert.Collection(
            companies,
            company => Assert.Equal("Desert Line", company.DisplayName),
            company => Assert.Equal("Mountain Haul", company.DisplayName));
    }

    [Fact]
    public async Task GetCompanyAsync_matches_company_id_case_insensitively()
    {
        var useCases = CreateUseCases();

        var company = await useCases.GetCompanyAsync("DESERT-LINE", new DashboardQueryOptions(), CancellationToken.None);

        Assert.NotNull(company);
        Assert.Equal("desert-line", company.Id);
    }

    [Fact]
    public async Task Nested_lookup_methods_match_ids_case_insensitively()
    {
        var useCases = CreateUseCases();

        var driver = await useCases.GetDriverAsync("DESERT-LINE", "DRIVER.ALICE", new DashboardQueryOptions(), CancellationToken.None);
        var garage = await useCases.GetGarageAsync("DESERT-LINE", "GARAGE.PHOENIX", new DashboardQueryOptions(), CancellationToken.None);
        var truck = await useCases.GetTruckAsync("DESERT-LINE", "TRUCK.ALICE", new DashboardQueryOptions(), CancellationToken.None);
        var trailer = await useCases.GetTrailerAsync("DESERT-LINE", "200B-420 TEXAS", new DashboardQueryOptions(), CancellationToken.None);
        var job = await useCases.GetJobAsync("DESERT-LINE", "JOB.1", new DashboardQueryOptions(), CancellationToken.None);
        var city = await useCases.GetCityAsync("DESERT-LINE", "PHOENIX", new DashboardQueryOptions(), CancellationToken.None);
        var route = await useCases.GetRouteAsync("DESERT-LINE", "PHOENIX", "DENVER", new DashboardQueryOptions(), CancellationToken.None);

        Assert.Equal("driver.alice", driver?.Id);
        Assert.Equal("garage.phoenix", garage?.Id);
        Assert.Equal("truck.alice", truck?.Id);
        Assert.Equal("200B-420 Texas", trailer?.LicensePlate);
        Assert.Equal("job.1", job?.Id);
        Assert.Equal("phoenix", city?.Id);
        Assert.Equal("denver", route?.DestinationCityId);
    }

    [Fact]
    public async Task Lookup_methods_return_null_when_company_or_entity_is_missing()
    {
        var useCases = CreateUseCases();

        Assert.Null(await useCases.GetCompanyAsync("missing", new DashboardQueryOptions(), CancellationToken.None));
        Assert.Null(await useCases.GetDriverAsync("desert-line", "missing", new DashboardQueryOptions(), CancellationToken.None));
        Assert.Null(await useCases.GetGarageAsync("desert-line", "missing", new DashboardQueryOptions(), CancellationToken.None));
        Assert.Null(await useCases.GetTruckAsync("desert-line", "missing", new DashboardQueryOptions(), CancellationToken.None));
        Assert.Null(await useCases.GetTrailerAsync("desert-line", "missing", new DashboardQueryOptions(), CancellationToken.None));
        Assert.Null(await useCases.GetJobAsync("desert-line", "missing", new DashboardQueryOptions(), CancellationToken.None));
        Assert.Null(await useCases.GetCityAsync("desert-line", "missing", new DashboardQueryOptions(), CancellationToken.None));
        Assert.Null(await useCases.GetRouteAsync("desert-line", "phoenix", "missing", new DashboardQueryOptions(), CancellationToken.None));
    }

    private static StatisticsDashboardUseCases CreateUseCases() =>
        CreateUseCases(CreateStatistics());

    private static StatisticsDashboardUseCases CreateUseCases(AtsStatistics statistics) =>
        new(new StatisticsService(new StubStatisticsSource(statistics)));

    private static CompanyStatistics CreateCompany(string id, string displayName)
    {
        var updated = new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);
        return new CompanyStatistics(
            id,
            displayName,
            updated,
            [],
            [],
            [],
            [],
            []);
    }

    private static AtsStatistics CreateStatistics()
    {
        var updated = new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);
        return new AtsStatistics(
            updated,
            [
                new CompanyStatistics(
                    "desert-line",
                    "Desert Line",
                    updated,
                    [new GarageStatistic("garage.phoenix", "Phoenix", 3000, 1, 1)],
                    [new DriverStatistic("driver.alice", "Alice", 3000, "garage.phoenix", "truck.alice")],
                    [new TruckStatistic("truck.alice", "Kenworth T680", 3000, "garage.phoenix", "driver.alice")],
                    [new MissionStatistic("job.1", "driver.alice", "truck.alice", "trailer.reefer.1", "reefer", "medicine", "phoenix", "denver", 3000, 42, "garage.phoenix", "200B-420 Texas")],
                    [],
                    [],
                    [new TrailerStatistic("trailer.reefer.1", "reefer", 3000, 1, GarageId: "garage.phoenix", LicensePlate: "200B-420 Texas")],
                    [new CityStatistic("phoenix", "Phoenix", true, true, 1, 3000, 0, 3000, 1.25m)],
                    [new RouteStatistic("phoenix", "denver", 3000, 1, 2.5m, 0.75m)],
                    []),
                new CompanyStatistics(
                    "mountain-haul",
                    "Mountain Haul",
                    updated,
                    [],
                    [],
                    [],
                    [],
                    [])
            ]);
    }

    private sealed class StubStatisticsSource(AtsStatistics statistics) : ISaveSnapshotSource, IStatisticsQuerySource
    {
        public Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            Task.FromResult<IReadOnlyList<SaveSnapshot>>([]);

        public Task<AtsStatistics> ReadStatisticsAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            Task.FromResult(statistics);
    }
}
