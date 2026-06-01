using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Tests;

public sealed class StatisticsDashboardOutputBoundaryTests
{
    [Fact]
    public async Task Dashboard_use_case_presents_dashboard_response_through_output_boundary()
    {
        var useCases = CreateUseCases();
        var output = new CapturingOutput<DashboardStatisticsDto>();

        await useCases.ExecuteDashboardAsync(
            output,
            new DashboardQueryRequest(),
            progress: null,
            CancellationToken.None);

        var dashboard = Assert.IsType<DashboardStatisticsDto>(output.Response);
        Assert.Contains(dashboard.Companies, company => company.Id == "desert-line");
    }

    [Fact]
    public async Task Reload_use_case_presents_dashboard_response_through_output_boundary()
    {
        var useCase = CreateReloadUseCase();
        var output = new CapturingOutput<DashboardStatisticsDto>();

        await useCase.ExecuteReloadAsync(
            output,
            new DashboardQueryRequest(),
            progress: null,
            CancellationToken.None);

        var dashboard = Assert.IsType<DashboardStatisticsDto>(output.Response);
        Assert.Contains(dashboard.Companies, company => company.Id == "desert-line");
    }

    private static StatisticsDashboardUseCases CreateUseCases() =>
        new(new StatisticsService(new StubStatisticsSource(CreateStatistics())));

    private static StatisticsReloadUseCase CreateReloadUseCase() =>
        new(new StatisticsService(new StubStatisticsSource(CreateStatistics())));

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
                    [])
            ]);
    }

    private sealed class CapturingOutput<T> : IOutputBoundaryAdapter<T>
    {
        public T? Response { get; private set; }

        public Task PresentAsync(T response, CancellationToken cancellationToken)
        {
            Response = response;
            return Task.CompletedTask;
        }
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
