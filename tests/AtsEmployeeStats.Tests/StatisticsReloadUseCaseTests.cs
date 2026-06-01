using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;

namespace AtsEmployeeStats.Tests;

public sealed class StatisticsReloadUseCaseTests
{
    [Fact]
    public async Task ReloadAsync_forces_ingestion_before_returning_dashboard()
    {
        var source = new RecordingStatisticsSource();
        var useCase = new StatisticsReloadUseCase(new StatisticsService(source));

        var dashboard = await useCase.ReloadAsync(new DashboardQueryOptions(), CancellationToken.None);

        Assert.True(source.ForceReceived);
        Assert.Equal(["ingest", "read"], source.Calls);
        Assert.Equal("Reloaded Line", Assert.Single(dashboard.Companies).DisplayName);
    }

    [Fact]
    public async Task ReloadAsync_applies_dashboard_query_options()
    {
        var source = new RecordingStatisticsSource();
        var useCase = new StatisticsReloadUseCase(new StatisticsService(source));

        var dashboard = await useCase.ReloadAsync(new DashboardQueryOptions(FromDay: 10, ToDay: 10), CancellationToken.None);

        var company = Assert.Single(dashboard.Companies);
        Assert.Equal(1000, company.Profit);
        Assert.Equal(1, Assert.Single(company.Drivers).JobCount);
    }

    private sealed class RecordingStatisticsSource : ISaveSnapshotSource, IStatisticsQuerySource, IStatisticsIngestor
    {
        public List<string> Calls { get; } = [];
        public bool ForceReceived { get; private set; }

        public Task IngestAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null,
            bool force = false)
        {
            Calls.Add("ingest");
            ForceReceived = force;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            Task.FromResult<IReadOnlyList<SaveSnapshot>>([]);

        public Task<AtsStatistics> ReadStatisticsAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null)
        {
            Calls.Add("read");
            var updated = new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);
            return Task.FromResult(new AtsStatistics(
                updated,
                [
                    new CompanyStatistics(
                        "reloaded-line",
                        "Reloaded Line",
                        updated,
                        [],
                        [new DriverStatistic("driver.alice", "Alice", 0, null, null)],
                        [],
                        [
                            new MissionStatistic("job.in", "driver.alice", null, null, null, null, null, null, 1000, 10),
                            new MissionStatistic("job.out", "driver.alice", null, null, null, null, null, null, 2000, 20)
                        ],
                        [])
                ]));
        }
    }
}
