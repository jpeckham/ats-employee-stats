using AtsEmployeeStats.Application.Statistics.Queries;

namespace AtsEmployeeStats.Api.Requests;

public sealed class ApiRequestMapper : IApiRequestMapper
{
    public DashboardQueryRequest Map(StatisticsRouteRequest request) =>
        new(request.FromDay, request.ToDay, request.ToSort());

    public ListCompaniesInputData Map(CompaniesRouteRequest request) =>
        new(ToQuery(request));

    public CompanyInputData Map(CompanyRouteRequest request) =>
        new(request.CompanyId, ToQuery(request));

    public DriverInputData Map(DriverRouteRequest request) =>
        new(request.CompanyId, request.DriverId, ToQuery(request));

    public GarageInputData Map(GarageRouteRequest request) =>
        new(request.CompanyId, request.GarageId, ToQuery(request));

    public TruckInputData Map(TruckRouteRequest request) =>
        new(request.CompanyId, request.TruckId, ToQuery(request));

    public TrailerInputData Map(TrailerRouteRequest request) =>
        new(request.CompanyId, request.LicensePlate, ToQuery(request));

    public JobInputData Map(JobRouteRequest request) =>
        new(request.CompanyId, request.JobId, ToQuery(request));

    public CityInputData Map(CityRouteRequest request) =>
        new(request.CompanyId, request.CityId, ToQuery(request));

    public RouteInputData Map(RouteRouteRequest request) =>
        new(request.CompanyId, request.OriginCityId, request.DestinationCityId, ToQuery(request));

    public RecommendNextGarageCityInputData MapNextGarageCity(RecommendationRouteRequest request) =>
        new(request.CompanyId, ToQuery(request));

    public RecommendTrailersForGarageInputData MapTrailerRecommendation(TrailerRecommendationRouteRequest request) =>
        new(request.CompanyId, request.GarageId, ToQuery(request), request.Count ?? 3);

    public DiagnoseUnderperformersInputData MapUnderperformers(RecommendationRouteRequest request) =>
        new(request.CompanyId, ToQuery(request), request.Count ?? 5);

    public RecommendDriverSkillsInputData MapDriverSkills(RecommendationRouteRequest request) =>
        new(request.CompanyId, ToQuery(request), request.Count ?? 5);

    private static DashboardQueryRequest ToQuery(DateRangeRouteRequest request) =>
        new(request.FromDay, request.ToDay);
}
