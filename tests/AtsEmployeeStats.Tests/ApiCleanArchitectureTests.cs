using AtsEmployeeStats.Api.Controllers;
using AtsEmployeeStats.Api;
using AtsEmployeeStats.Api.Requests;
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Application.Statistics.Queries;
using AtsEmployeeStats.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace AtsEmployeeStats.Tests;

public sealed class ApiCleanArchitectureTests
{
    [Fact]
    public void Program_routes_to_clean_code_controllers_instead_of_use_cases()
    {
        var program = File.ReadAllText(GetRepositoryPath("src", "AtsEmployeeStats.Api", "Program.cs"));

        Assert.Contains("StatisticsController", program);
        Assert.Contains("CompaniesController", program);
        Assert.Contains("DriversController", program);
        Assert.Contains("GaragesController", program);
        Assert.Contains("TrucksController", program);
        Assert.Contains("TrailersController", program);
        Assert.Contains("JobsController", program);
        Assert.Contains("CitiesController", program);
        Assert.Contains("CompanyPerformanceController", program);
        Assert.DoesNotContain("RecommendationsController", program);
        Assert.Contains("StatisticsRouteRequest request", program);
        Assert.Contains("CompanyRouteRequest request", program);
        Assert.Contains("RecommendationRouteRequest request", program);
        Assert.DoesNotContain("IStatisticsDashboardUseCases useCases", program);
        Assert.DoesNotContain("IStatisticsReloadUseCase reloadUseCase", program);
        Assert.DoesNotContain("IRecommendNextGarageCityUseCase useCase", program);
        Assert.DoesNotContain("BuildSignalRProgress", program);
        Assert.DoesNotContain("int? fromDay", program);
        Assert.DoesNotContain("string companyId", program);
    }

    [Fact]
    public void Api_controllers_are_decomposed_by_cohesive_route_nouns()
    {
        var controllersPath = GetRepositoryPath("src", "AtsEmployeeStats.Api", "Controllers");

        Assert.True(File.Exists(Path.Combine(controllersPath, "StatisticsController.cs")));
        Assert.True(File.Exists(Path.Combine(controllersPath, "CompaniesController.cs")));
        Assert.True(File.Exists(Path.Combine(controllersPath, "DriversController.cs")));
        Assert.True(File.Exists(Path.Combine(controllersPath, "GaragesController.cs")));
        Assert.True(File.Exists(Path.Combine(controllersPath, "TrucksController.cs")));
        Assert.True(File.Exists(Path.Combine(controllersPath, "TrailersController.cs")));
        Assert.True(File.Exists(Path.Combine(controllersPath, "JobsController.cs")));
        Assert.True(File.Exists(Path.Combine(controllersPath, "CitiesController.cs")));
        Assert.True(File.Exists(Path.Combine(controllersPath, "CompanyPerformanceController.cs")));
        Assert.False(File.Exists(Path.Combine(controllersPath, "RecommendationsController.cs")));

        var statisticsController = File.ReadAllText(Path.Combine(controllersPath, "StatisticsController.cs"));
        Assert.DoesNotContain("GetCompanyAsync", statisticsController);
        Assert.DoesNotContain("GetDriverAsync", statisticsController);
        Assert.DoesNotContain("GetGarageAsync", statisticsController);
        Assert.DoesNotContain("GetTruckAsync", statisticsController);
        Assert.DoesNotContain("GetTrailerAsync", statisticsController);
        Assert.DoesNotContain("GetJobAsync", statisticsController);
        Assert.DoesNotContain("GetCityAsync", statisticsController);
    }

    [Fact]
    public async Task Statistics_controller_maps_route_request_to_input_data_and_returns_presenter_view_model()
    {
        var inputBoundary = new CapturingDashboardInputBoundary();
        var controller = new CompaniesController(
            inputBoundary,
            new ApiRequestMapper(),
            new NullHubContext<StatisticsHub>());

        var result = await controller.GetCompanyAsync(
            new CompanyRouteRequest
            {
                CompanyId = "desert-line",
                FromDay = 10,
                ToDay = 20
            },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("desert-line", inputBoundary.CompanyInput?.CompanyId);
        Assert.Equal(10, inputBoundary.CompanyInput?.Query.FromDay);
        Assert.Equal(20, inputBoundary.CompanyInput?.Query.ToDay);
        Assert.True(inputBoundary.PresenterWasUsed);
    }

    [Fact]
    public void Api_presenters_expose_view_model_not_result()
    {
        var presenter = File.ReadAllText(GetRepositoryPath("src", "AtsEmployeeStats.Api", "Presentation", "HttpResultPresenter.cs"));
        var nullablePresenter = File.ReadAllText(GetRepositoryPath("src", "AtsEmployeeStats.Api", "Presentation", "NullableHttpResultPresenter.cs"));
        var reloadPresenter = File.ReadAllText(GetRepositoryPath("src", "AtsEmployeeStats.Api", "Presentation", "SignalRReloadPresenter.cs"));

        Assert.Contains("ViewModel", presenter);
        Assert.Contains("ViewModel", nullablePresenter);
        Assert.Contains("ViewModel", reloadPresenter);
        Assert.DoesNotContain("public IResult Result", presenter);
        Assert.DoesNotContain("public IResult Result", nullablePresenter);
        Assert.DoesNotContain("public IResult Result", reloadPresenter);
    }

    private static string GetRepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AtsEmployeeStats.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. parts]);
    }

    private sealed class CapturingDashboardInputBoundary : IStatisticsDashboardUseCases, IStatisticsReloadUseCase
    {
        public CompanyInputData? CompanyInput { get; private set; }
        public bool PresenterWasUsed { get; private set; }

        public async Task ExecuteCompanyAsync(
            IOutputBoundaryAdapter<CompanyDto?> output,
            CompanyInputData input,
            IProgressOutputBoundaryAdapter? progress,
            CancellationToken cancellationToken)
        {
            CompanyInput = input;
            await output.PresentAsync(
                new CompanyDto(
                    input.CompanyId,
                    "Desert Line",
                    0,
                    [],
                    [],
                    [],
                    [],
                    []),
                cancellationToken);
            PresenterWasUsed = true;
        }

        public Task ExecuteDashboardAsync(
            IOutputBoundaryAdapter<DashboardStatisticsDto> output,
            DashboardQueryRequest request,
            IProgressOutputBoundaryAdapter? progress,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task ExecuteListCompaniesAsync(
            IOutputBoundaryAdapter<IReadOnlyList<CompanyDto>> output,
            ListCompaniesInputData input,
            IProgressOutputBoundaryAdapter? progress,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task ExecuteDriverAsync(
            IOutputBoundaryAdapter<DriverDto?> output,
            DriverInputData input,
            IProgressOutputBoundaryAdapter? progress,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task ExecuteGarageAsync(
            IOutputBoundaryAdapter<GarageDto?> output,
            GarageInputData input,
            IProgressOutputBoundaryAdapter? progress,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task ExecuteTruckAsync(
            IOutputBoundaryAdapter<TruckDto?> output,
            TruckInputData input,
            IProgressOutputBoundaryAdapter? progress,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task ExecuteTrailerAsync(
            IOutputBoundaryAdapter<TrailerDto?> output,
            TrailerInputData input,
            IProgressOutputBoundaryAdapter? progress,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task ExecuteJobAsync(
            IOutputBoundaryAdapter<MissionDto?> output,
            JobInputData input,
            IProgressOutputBoundaryAdapter? progress,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task ExecuteCityAsync(
            IOutputBoundaryAdapter<CityDto?> output,
            CityInputData input,
            IProgressOutputBoundaryAdapter? progress,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task ExecuteRouteAsync(
            IOutputBoundaryAdapter<RouteDto?> output,
            RouteInputData input,
            IProgressOutputBoundaryAdapter? progress,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task ExecuteReloadAsync(
            IOutputBoundaryAdapter<DashboardStatisticsDto> output,
            DashboardQueryRequest request,
            IProgressOutputBoundaryAdapter? progress,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<DashboardStatisticsDto> GetDashboardAsync(
            DashboardQueryOptions options,
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            throw new NotImplementedException();

        public Task<DashboardStatisticsDto> ReloadAsync(
            DashboardQueryOptions options,
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<CompanyDto>> ListCompaniesAsync(
            DashboardQueryOptions options,
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            throw new NotImplementedException();

        public Task<CompanyDto?> GetCompanyAsync(
            string companyId,
            DashboardQueryOptions options,
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            throw new NotImplementedException();

        public Task<DriverDto?> GetDriverAsync(
            string companyId,
            string driverId,
            DashboardQueryOptions options,
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            throw new NotImplementedException();

        public Task<GarageDto?> GetGarageAsync(
            string companyId,
            string garageId,
            DashboardQueryOptions options,
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            throw new NotImplementedException();

        public Task<TruckDto?> GetTruckAsync(
            string companyId,
            string truckId,
            DashboardQueryOptions options,
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            throw new NotImplementedException();

        public Task<TrailerDto?> GetTrailerAsync(
            string companyId,
            string licensePlate,
            DashboardQueryOptions options,
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            throw new NotImplementedException();

        public Task<MissionDto?> GetJobAsync(
            string companyId,
            string jobId,
            DashboardQueryOptions options,
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            throw new NotImplementedException();

        public Task<CityDto?> GetCityAsync(
            string companyId,
            string cityId,
            DashboardQueryOptions options,
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            throw new NotImplementedException();

        public Task<RouteDto?> GetRouteAsync(
            string companyId,
            string originCityId,
            string destinationCityId,
            DashboardQueryOptions options,
            CancellationToken cancellationToken,
            IProgress<SaveLoadProgress>? progress = null) =>
            throw new NotImplementedException();
    }

    private sealed class NullHubContext<THub> : IHubContext<THub>
        where THub : Hub
    {
        public IHubClients Clients { get; } = new NullHubClients();

        public IGroupManager Groups { get; } = new NullGroupManager();
    }

    private sealed class NullHubClients : IHubClients
    {
        private static readonly IClientProxy Proxy = new NullClientProxy();

        public IClientProxy All => Proxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;

        public IClientProxy Client(string connectionId) => Proxy;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;

        public IClientProxy Group(string groupName) => Proxy;

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;

        public IClientProxy User(string userId) => Proxy;

        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class NullClientProxy : IClientProxy
    {
        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NullGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
