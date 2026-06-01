using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public sealed record ListCompaniesInputData(DashboardQueryRequest Query);

public sealed record CompanyInputData(string CompanyId, DashboardQueryRequest Query);

public sealed record DriverInputData(string CompanyId, string DriverId, DashboardQueryRequest Query);

public sealed record GarageInputData(string CompanyId, string GarageId, DashboardQueryRequest Query);

public sealed record TruckInputData(string CompanyId, string TruckId, DashboardQueryRequest Query);

public sealed record TrailerInputData(string CompanyId, string LicensePlate, DashboardQueryRequest Query);

public sealed record JobInputData(string CompanyId, string JobId, DashboardQueryRequest Query);

public sealed record CityInputData(string CompanyId, string CityId, DashboardQueryRequest Query);

public sealed record RouteInputData(
    string CompanyId,
    string OriginCityId,
    string DestinationCityId,
    DashboardQueryRequest Query);

public sealed record RecommendNextGarageCityInputData(string CompanyId, DashboardQueryRequest Query);

public sealed record RecommendTrailersForGarageInputData(
    string CompanyId,
    string GarageId,
    DashboardQueryRequest Query,
    int Count = 3);

public sealed record DiagnoseUnderperformersInputData(
    string CompanyId,
    DashboardQueryRequest Query,
    int Count = 5);

public sealed record RecommendDriverSkillsInputData(
    string CompanyId,
    DashboardQueryRequest Query,
    int Count = 5);
