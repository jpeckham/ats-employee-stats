using AtsEmployeeStats.Application.Statistics.Queries;

namespace AtsEmployeeStats.Api.Requests;

public interface IApiRequestMapper
{
    DashboardQueryRequest Map(StatisticsRouteRequest request);

    ListCompaniesInputData Map(CompaniesRouteRequest request);

    CompanyInputData Map(CompanyRouteRequest request);

    DriverInputData Map(DriverRouteRequest request);

    GarageInputData Map(GarageRouteRequest request);

    TruckInputData Map(TruckRouteRequest request);

    TrailerInputData Map(TrailerRouteRequest request);

    JobInputData Map(JobRouteRequest request);

    CityInputData Map(CityRouteRequest request);

    RouteInputData Map(RouteRouteRequest request);

    RecommendNextGarageCityInputData MapNextGarageCity(RecommendationRouteRequest request);

    RecommendTrailersForGarageInputData MapTrailerRecommendation(TrailerRecommendationRouteRequest request);

    DiagnoseUnderperformersInputData MapUnderperformers(RecommendationRouteRequest request);

    RecommendDriverSkillsInputData MapDriverSkills(RecommendationRouteRequest request);
}
