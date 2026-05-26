using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Domain.Statistics;
using System.Data;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace AtsEmployeeStats.Tests;

public sealed class TerminalDashboardAppTests
{
    [Fact]
    public async Task RunAsync_renders_once_and_returns_when_key_input_is_not_available()
    {
        var service = new StatisticsService(new EmptySaveSnapshotSource());
        var app = new TerminalDashboardApp(service, Path.GetTempPath(), canReadKeys: () => false);

        await app.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_uses_terminal_gui_dashboard_runner_when_key_input_is_available()
    {
        var loadedStatistics = new LoadedStatistics(
            new AtsStatistics(
                new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
                [
                    new CompanyStatistics(
                        "desert-line",
                        "Desert Line",
                        new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
                        [],
                        [],
                        [],
                        [],
                        [])
                ]),
            "Loaded from SQLite gold models.");
        var runner = new CapturingDashboardRunner();
        var service = new StatisticsService(new EmptySaveSnapshotSource());
        var app = new TerminalDashboardApp(
            service,
            "C:\\ATS",
            canReadKeys: () => true,
            dashboardRunner: runner,
            initialLoad: (_, _, _) => Task.FromResult(loadedStatistics));

        await app.RunAsync(CancellationToken.None);

        Assert.Equal("C:\\ATS", runner.SaveRoot);
        Assert.Same(service, runner.Service);
        Assert.Equal(loadedStatistics, runner.Initial);
    }


    [Fact]
    public void BuildWindow_starts_with_trucking_company_selection()
    {
        var statistics = SampleStatistics();

        var window = TerminalGuiDashboard.BuildWindow(statistics, "C:\\ATS", "Loaded from SQLite gold models.");

        var views = Flatten(window).ToList();
        Assert.Contains(views, view => view is MenuBar);
        Assert.Contains(views, view => view is ListView && view.Title == "Time Range");
        Assert.Contains(views, view => view is TableView && view.Title == "Trucking Companies");
        Assert.Equal(["Desert Line"], ColumnValues(window, "Trucking Companies", "Company"));
        Assert.Equal(["$6,000"], ColumnValues(window, "Trucking Companies", "Profit"));
    }

    [Fact]
    public void BuildWindow_shows_garage_profitability_for_selected_company()
    {
        var statistics = SampleStatistics();
        var state = new DrilldownDashboardState(
            DrilldownDashboardScreen.Garages,
            RangeDays: 14,
            CompanyId: "desert-line");

        var window = TerminalGuiDashboard.BuildWindow(statistics, "C:\\ATS", "Loaded.", state);

        Assert.Equal(["phoenix", "tucson"], ColumnValues(window, "Garages", "Garage"));
        Assert.Equal(["$5,000", "$1,000"], ColumnValues(window, "Garages", "Profit"));
        Assert.Equal(["$357", "$71"], ColumnValues(window, "Garages", "$/Day"));
    }

    [Fact]
    public void BuildWindow_shows_drivers_for_selected_garage()
    {
        var statistics = SampleStatistics();
        var state = new DrilldownDashboardState(
            DrilldownDashboardScreen.Drivers,
            RangeDays: 7,
            CompanyId: "desert-line",
            GarageId: "garage.phoenix");

        var window = TerminalGuiDashboard.BuildWindow(statistics, "C:\\ATS", "Loaded.", state);

        Assert.Equal(["Alice Ramirez"], ColumnValues(window, "Drivers", "Driver"));
        Assert.Equal(["$3,000"], ColumnValues(window, "Drivers", "Profit"));
        Assert.Equal(["$429"], ColumnValues(window, "Drivers", "$/Day"));
        Assert.Equal(["truck.alice"], ColumnValues(window, "Drivers", "Truck"));
    }

    [Fact]
    public void BuildWindow_shows_job_type_and_route_pair_profitability_for_selected_driver()
    {
        var statistics = SampleStatistics();
        var state = new DrilldownDashboardState(
            DrilldownDashboardScreen.DriverJobs,
            RangeDays: 14,
            CompanyId: "desert-line",
            GarageId: "garage.phoenix",
            DriverId: "driver.alice");

        var window = TerminalGuiDashboard.BuildWindow(statistics, "C:\\ATS", "Loaded.", state);

        Assert.Equal(["reefer", "flatbed"], ColumnValues(window, "Job Types", "Job Type"));
        Assert.Equal(["Denver <-> Phoenix", "Phoenix <-> Vegas"], ColumnValues(window, "Job Pairs", "Route Pair"));
        Assert.Equal(["$5,500", "$1,500"], ColumnValues(window, "Job Pairs", "Profit"));
        Assert.Equal(["job.alice.outbound", "job.alice.inbound", "job.alice.flatbed"], ColumnValues(window, "Jobs", "Job"));
    }

    [Fact]
    public void DrilldownDashboardState_transitions_through_company_garage_driver_and_range_selection()
    {
        var state = DrilldownDashboardState.Initial;

        state = state.SelectCompany("desert-line");
        Assert.Equal(DrilldownDashboardScreen.Garages, state.Screen);
        Assert.Equal("desert-line", state.CompanyId);

        state = state.SelectGarage("garage.phoenix");
        Assert.Equal(DrilldownDashboardScreen.Drivers, state.Screen);
        Assert.Equal("garage.phoenix", state.GarageId);

        state = state.SelectDriver("driver.alice");
        Assert.Equal(DrilldownDashboardScreen.DriverJobs, state.Screen);
        Assert.Equal("driver.alice", state.DriverId);

        state = state.SelectRangeDays(7);
        Assert.Equal(7, state.RangeDays);
        Assert.Equal("desert-line", state.CompanyId);
        Assert.Equal("garage.phoenix", state.GarageId);
        Assert.Equal("driver.alice", state.DriverId);
    }

    private static AtsStatistics SampleStatistics() =>
        new(
            new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
            [
                new CompanyStatistics(
                    "desert-line",
                    "Desert Line",
                    new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
                    [
                        new GarageStatistic("garage.phoenix", "phoenix", 5000, 1, 1),
                        new GarageStatistic("garage.tucson", "tucson", 1000, 1, 1)
                    ],
                    [
                        new DriverStatistic("driver.alice", "Alice Ramirez", 3000, "garage.phoenix", "truck.alice"),
                        new DriverStatistic("driver.bob", "Bob Reyes", 1000, "garage.tucson", "truck.bob")
                    ],
                    [],
                    [
                        new MissionStatistic("job.alice.outbound", "driver.alice", "truck.alice", "trailer.1", "reefer", "medicine", "phoenix", "denver", 3000),
                        new MissionStatistic("job.alice.inbound", "driver.alice", "truck.alice", "trailer.2", "reefer", "medicine", "denver", "phoenix", 2500),
                        new MissionStatistic("job.alice.flatbed", "driver.alice", "truck.alice", "trailer.3", "flatbed", "steel", "phoenix", "vegas", 1500),
                        new MissionStatistic("job.bob.reefer", "driver.bob", "truck.bob", "trailer.4", "reefer", "apples", "tucson", "phoenix", 1000)
                    ],
                    [])
            ]);

    private sealed class EmptySaveSnapshotSource : ISaveSnapshotSource
    {
        public Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync(
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            Task.FromResult<IReadOnlyList<SaveSnapshot>>([]);
    }

    private sealed class CapturingDashboardRunner : ITerminalDashboardRunner
    {
        public StatisticsService? Service { get; private set; }
        public string? SaveRoot { get; private set; }
        public LoadedStatistics? Initial { get; private set; }

        public Task RunAsync(
            StatisticsService service,
            string saveRoot,
            LoadedStatistics initial,
            CancellationToken cancellationToken)
        {
            Service = service;
            SaveRoot = saveRoot;
            Initial = initial;
            return Task.CompletedTask;
        }
    }

    private static IEnumerable<View> Flatten(View view)
    {
        yield return view;
        foreach (var child in view.SubViews)
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }

    private static string[] ColumnValues(View root, string tableTitle, string columnName)
    {
        var tableView = Flatten(root).OfType<TableView>().Single(view => view.Title == tableTitle);
        var data = Assert.IsType<DataTableSource>(tableView.Table).DataTable;
        return data.Rows
            .Cast<DataRow>()
            .Select(row => row[columnName].ToString() ?? string.Empty)
            .ToArray();
    }
}
