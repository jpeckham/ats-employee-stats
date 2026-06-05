using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Wpf.Controllers;
using AtsEmployeeStats.Wpf.Threading;
using AtsEmployeeStats.Wpf.ViewModels;

namespace AtsEmployeeStats.Tests;

public sealed class DashboardPresenterTests
{
    [Fact]
    public void ApplyLoadProgress_maps_save_file_and_content_progress_to_wpf_state()
    {
        var presenter = CreatePresenter();

        presenter.ApplyLoadProgress(new SaveLoadProgress(
            SaveLoadStage.LoadingFiles,
            CompletedFiles: 3,
            TotalFiles: 12,
            CompletedUnits: 0,
            TotalUnits: 0,
            Message: "Loading save 3 of 12.",
            CurrentFileCompletedUnits: 42,
            CurrentFileTotalUnits: 100));

        Assert.Equal(25, presenter.SaveFileProgressValue);
        Assert.Equal(42, presenter.SaveContentProgressValue);
        Assert.Equal("Save files: 3 of 12", presenter.SaveFileProgressText);
        Assert.Equal("Current save contents: 42 of 100", presenter.SaveContentProgressText);
        Assert.Equal("Loading save 3 of 12.", presenter.StatusText);
    }

    [Fact]
    public void ApplyLoadProgress_maps_medallion_phase_progress_to_wpf_state()
    {
        var presenter = CreatePresenter();

        presenter.ApplyLoadProgress(new SaveLoadProgress(
            SaveLoadStage.BuildingStatistics,
            CompletedFiles: 0,
            TotalFiles: 8,
            CompletedUnits: 0,
            TotalUnits: 0,
            Message: "Building gold statistics.",
            PhaseCompleted: 2,
            PhaseTotal: 5));

        Assert.Equal(40, presenter.SaveFileProgressValue);
        Assert.Equal(0, presenter.SaveContentProgressValue);
        Assert.Equal("Building gold statistics.", presenter.SaveFileProgressText);
        Assert.Equal("Phase progress: 2 of 5", presenter.SaveContentProgressText);
        Assert.Equal("Building gold statistics.", presenter.StatusText);
    }

    [Fact]
    public async Task ReloadAsync_sets_busy_progress_status_and_uses_exclude_player_query_option()
    {
        var dashboardUseCases = new StubDashboardUseCases();
        var backgroundRunner = new RecordingBackgroundRunner();
        var reloadUseCase = new StubReloadUseCase
        {
            ReloadResult = Dashboard(Company("company-a", "Northwind")),
            OnReloadStarted = presenter =>
            {
                Assert.True(presenter.IsBusy);
                Assert.True(presenter.IsLoadProgressVisible);
                Assert.Equal("Reloading local save statistics...", presenter.StatusText);
            }
        };
        var presenter = CreatePresenter(dashboardUseCases, reloadUseCase, backgroundRunner);
        presenter.ExcludePlayerDriver = true;

        await presenter.ReloadAsync([], []);

        Assert.Equal(1, backgroundRunner.RunCount);
        Assert.False(presenter.IsBusy);
        Assert.False(presenter.IsLoadProgressVisible);
        Assert.Equal("Reloaded 1 companies", presenter.StatusText);
        Assert.True(reloadUseCase.ReloadOptions.ExcludePlayerDriver);
        Assert.True(presenter.CanRefreshDashboard);
    }

    [Fact]
    public async Task RefreshAsync_restores_selected_detail_tab_after_dashboard_reload()
    {
        var backgroundRunner = new RecordingBackgroundRunner();
        var dashboardUseCases = new StubDashboardUseCases
        {
            DashboardResult = Dashboard(Company("company-a", "Northwind", garageId: "garage-a", driverId: "driver-a"))
        };
        var presenter = CreatePresenter(dashboardUseCases, backgroundRunner: backgroundRunner);
        await presenter.RefreshAsync([], []);
        presenter.SelectExplorerNode(new ExplorerNodeViewModel("Northwind", ExplorerNodeKind.Company, "company-a"), []);
        presenter.NotifyTabSelected("Drivers");
        dashboardUseCases.DashboardResult = Dashboard(Company("company-a", "Northwind Updated", garageId: "garage-b", driverId: "driver-b"));

        await presenter.RefreshAsync([], []);

        var detail = Assert.IsType<CompanyDetailViewModel>(presenter.SelectedDetail);
        Assert.Equal("Northwind Updated", detail.Title);
        Assert.Equal("Drivers", detail.Tabs[detail.SelectedTabIndex].Title);
        Assert.Equal("Loaded 1 companies", presenter.StatusText);
        Assert.Equal(2, backgroundRunner.RunCount);
    }

    [Fact]
    public async Task SyncOnStartupAsync_resets_progress_and_sets_loaded_company_status()
    {
        var reloadUseCase = new StubReloadUseCase
        {
            SyncResult = Dashboard(Company("company-a", "Northwind"))
        };
        var presenter = CreatePresenter(reloadUseCase: reloadUseCase);
        presenter.ApplyLoadProgress(new SaveLoadProgress(
            SaveLoadStage.LoadingFiles,
            CompletedFiles: 1,
            TotalFiles: 1,
            CompletedUnits: 10,
            TotalUnits: 10,
            Message: "Previous progress",
            CurrentFileCompletedUnits: 10,
            CurrentFileTotalUnits: 10));

        await presenter.SyncOnStartupAsync([], []);

        Assert.False(presenter.IsBusy);
        Assert.False(presenter.IsLoadProgressVisible);
        Assert.Equal("Loaded 1 companies", presenter.StatusText);
        Assert.IsType<CompaniesDetailViewModel>(presenter.SelectedDetail);
        Assert.True(presenter.Explorer.Roots.Count > 0);
    }

    private static DashboardPresenter CreatePresenter(
        StubDashboardUseCases? dashboardUseCases = null,
        StubReloadUseCase? reloadUseCase = null,
        IBackgroundRunner? backgroundRunner = null)
    {
        var presenter = new DashboardPresenter(
            dashboardUseCases ?? new StubDashboardUseCases(),
            reloadUseCase ?? new StubReloadUseCase(),
            new ExplorerPresenter(),
            backgroundRunner ?? new ImmediateBackgroundRunner());
        if (reloadUseCase is not null)
            reloadUseCase.Presenter = presenter;
        return presenter;
    }

    private sealed class RecordingBackgroundRunner : IBackgroundRunner
    {
        public int RunCount { get; private set; }

        public Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken = default)
        {
            RunCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(work());
        }

        public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default)
        {
            RunCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return await work();
        }
    }

    private static DashboardStatisticsDto Dashboard(params CompanyDto[] companies) =>
        new(DateTimeOffset.UtcNow, companies);

    private static CompanyDto Company(
        string id,
        string displayName,
        string garageId = "garage",
        string driverId = "driver") =>
        new(
            id,
            displayName,
            100,
            [new GarageDto(garageId, "Garage", 100, 10, 1, 1)],
            [new DriverDto(driverId, "Driver", 100, 10, garageId, "truck", 1)],
            [new TruckDto("truck", "Truck", 100, garageId, driverId)],
            [],
            []);

    private sealed class StubDashboardUseCases : IStatisticsDashboardUseCases
    {
        public DashboardStatisticsDto DashboardResult { get; set; } = Dashboard();
        public DashboardQueryOptions DashboardOptions { get; private set; } = new();

        public Task<DashboardStatisticsDto> GetDashboardAsync(DashboardQueryOptions options, CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null)
        {
            DashboardOptions = options;
            return Task.FromResult(DashboardResult);
        }

        public Task ExecuteDashboardAsync(IOutputBoundaryAdapter<DashboardStatisticsDto> output, DashboardQueryRequest request, IProgressOutputBoundaryAdapter? progress, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task ExecuteListCompaniesAsync(IOutputBoundaryAdapter<IReadOnlyList<CompanyDto>> output, ListCompaniesInputData input, IProgressOutputBoundaryAdapter? progress, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task ExecuteCompanyAsync(IOutputBoundaryAdapter<CompanyDto?> output, CompanyInputData input, IProgressOutputBoundaryAdapter? progress, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task ExecuteDriverAsync(IOutputBoundaryAdapter<DriverDto?> output, DriverInputData input, IProgressOutputBoundaryAdapter? progress, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task ExecuteGarageAsync(IOutputBoundaryAdapter<GarageDto?> output, GarageInputData input, IProgressOutputBoundaryAdapter? progress, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task ExecuteTruckAsync(IOutputBoundaryAdapter<TruckDto?> output, TruckInputData input, IProgressOutputBoundaryAdapter? progress, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task ExecuteTrailerAsync(IOutputBoundaryAdapter<TrailerDto?> output, TrailerInputData input, IProgressOutputBoundaryAdapter? progress, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task ExecuteJobAsync(IOutputBoundaryAdapter<MissionDto?> output, JobInputData input, IProgressOutputBoundaryAdapter? progress, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task ExecuteCityAsync(IOutputBoundaryAdapter<CityDto?> output, CityInputData input, IProgressOutputBoundaryAdapter? progress, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task ExecuteRouteAsync(IOutputBoundaryAdapter<RouteDto?> output, RouteInputData input, IProgressOutputBoundaryAdapter? progress, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<CompanyDto>> ListCompaniesAsync(DashboardQueryOptions options, CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null) => throw new NotImplementedException();
        public Task<CompanyDto?> GetCompanyAsync(string companyId, DashboardQueryOptions options, CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null) => throw new NotImplementedException();
        public Task<DriverDto?> GetDriverAsync(string companyId, string driverId, DashboardQueryOptions options, CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null) => throw new NotImplementedException();
        public Task<GarageDto?> GetGarageAsync(string companyId, string garageId, DashboardQueryOptions options, CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null) => throw new NotImplementedException();
        public Task<TruckDto?> GetTruckAsync(string companyId, string truckId, DashboardQueryOptions options, CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null) => throw new NotImplementedException();
        public Task<TrailerDto?> GetTrailerAsync(string companyId, string licensePlate, DashboardQueryOptions options, CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null) => throw new NotImplementedException();
        public Task<MissionDto?> GetJobAsync(string companyId, string jobId, DashboardQueryOptions options, CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null) => throw new NotImplementedException();
        public Task<CityDto?> GetCityAsync(string companyId, string cityId, DashboardQueryOptions options, CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null) => throw new NotImplementedException();
        public Task<RouteDto?> GetRouteAsync(string companyId, string originCityId, string destinationCityId, DashboardQueryOptions options, CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null) => throw new NotImplementedException();
    }

    private sealed class StubReloadUseCase : IStatisticsReloadUseCase
    {
        public DashboardPresenter? Presenter { get; set; }
        public DashboardStatisticsDto ReloadResult { get; set; } = Dashboard();
        public DashboardStatisticsDto SyncResult { get; set; } = Dashboard();
        public DashboardQueryOptions ReloadOptions { get; private set; } = new();
        public Action<DashboardPresenter>? OnReloadStarted { get; set; }

        public Task<DashboardStatisticsDto> ReloadAsync(DashboardQueryOptions options, CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null)
        {
            ReloadOptions = options;
            if (Presenter is not null)
                OnReloadStarted?.Invoke(Presenter);
            return Task.FromResult(ReloadResult);
        }

        public Task<DashboardStatisticsDto> SyncAsync(DashboardQueryOptions options, CancellationToken cancellationToken, IProgress<SaveLoadProgress>? progress = null) =>
            Task.FromResult(SyncResult);

        public Task ExecuteReloadAsync(IOutputBoundaryAdapter<DashboardStatisticsDto> output, DashboardQueryRequest request, IProgressOutputBoundaryAdapter? progress, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
