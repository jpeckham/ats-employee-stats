using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public interface IStatisticsDashboardUseCases
{
    Task ExecuteDashboardAsync(
        IOutputBoundaryAdapter<DashboardStatisticsDto> output,
        DashboardQueryRequest request,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task ExecuteListCompaniesAsync(
        IOutputBoundaryAdapter<IReadOnlyList<CompanyDto>> output,
        ListCompaniesInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task ExecuteCompanyAsync(
        IOutputBoundaryAdapter<CompanyDto?> output,
        CompanyInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task ExecuteDriverAsync(
        IOutputBoundaryAdapter<DriverDto?> output,
        DriverInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task ExecuteGarageAsync(
        IOutputBoundaryAdapter<GarageDto?> output,
        GarageInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task ExecuteTruckAsync(
        IOutputBoundaryAdapter<TruckDto?> output,
        TruckInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task ExecuteTrailerAsync(
        IOutputBoundaryAdapter<TrailerDto?> output,
        TrailerInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task ExecuteJobAsync(
        IOutputBoundaryAdapter<MissionDto?> output,
        JobInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task ExecuteCityAsync(
        IOutputBoundaryAdapter<CityDto?> output,
        CityInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task ExecuteRouteAsync(
        IOutputBoundaryAdapter<RouteDto?> output,
        RouteInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task<DashboardStatisticsDto> GetDashboardAsync(
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);

    Task<IReadOnlyList<CompanyDto>> ListCompaniesAsync(
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);

    Task<CompanyDto?> GetCompanyAsync(
        string companyId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);

    Task<DriverDto?> GetDriverAsync(
        string companyId,
        string driverId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);

    Task<GarageDto?> GetGarageAsync(
        string companyId,
        string garageId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);

    Task<TruckDto?> GetTruckAsync(
        string companyId,
        string truckId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);

    Task<TrailerDto?> GetTrailerAsync(
        string companyId,
        string licensePlate,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);

    Task<MissionDto?> GetJobAsync(
        string companyId,
        string jobId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);

    Task<CityDto?> GetCityAsync(
        string companyId,
        string cityId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);

    Task<RouteDto?> GetRouteAsync(
        string companyId,
        string originCityId,
        string destinationCityId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);
}
