using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Api.Requests;

public class DateRangeRouteRequest
{
    public int? FromDay { get; set; }

    public int? ToDay { get; set; }
}

public sealed class StatisticsRouteRequest : DateRangeRouteRequest
{
    public string? GaragesSortBy { get; set; }

    public string? GaragesSortDir { get; set; }

    public string? DriversSortBy { get; set; }

    public string? DriversSortDir { get; set; }

    public string? TrucksSortBy { get; set; }

    public string? TrucksSortDir { get; set; }

    public string? TrailersSortBy { get; set; }

    public string? TrailersSortDir { get; set; }

    public string? MissionsSortBy { get; set; }

    public string? MissionsSortDir { get; set; }

    public string? CitiesSortBy { get; set; }

    public string? CitiesSortDir { get; set; }

    public string? RoutesSortBy { get; set; }

    public string? RoutesSortDir { get; set; }

    public CollectionSortDto ToSort() =>
        new(
            GaragesSortBy,
            GaragesSortDir,
            DriversSortBy,
            DriversSortDir,
            TrucksSortBy,
            TrucksSortDir,
            TrailersSortBy,
            TrailersSortDir,
            MissionsSortBy,
            MissionsSortDir,
            CitiesSortBy,
            CitiesSortDir,
            RoutesSortBy,
            RoutesSortDir);
}

public sealed class CompaniesRouteRequest : DateRangeRouteRequest;

public class CompanyRouteRequest : DateRangeRouteRequest
{
    public string CompanyId { get; set; } = string.Empty;
}

public sealed class DriverRouteRequest : CompanyRouteRequest
{
    public string DriverId { get; set; } = string.Empty;
}

public class GarageRouteRequest : CompanyRouteRequest
{
    public string GarageId { get; set; } = string.Empty;
}

public sealed class TruckRouteRequest : CompanyRouteRequest
{
    public string TruckId { get; set; } = string.Empty;
}

public sealed class TrailerRouteRequest : CompanyRouteRequest
{
    public string LicensePlate { get; set; } = string.Empty;
}

public sealed class JobRouteRequest : CompanyRouteRequest
{
    public string JobId { get; set; } = string.Empty;
}

public sealed class CityRouteRequest : CompanyRouteRequest
{
    public string CityId { get; set; } = string.Empty;
}

public sealed class RouteRouteRequest : CompanyRouteRequest
{
    public string OriginCityId { get; set; } = string.Empty;

    public string DestinationCityId { get; set; } = string.Empty;
}

public sealed class RecommendationRouteRequest : CompanyRouteRequest
{
    public int? Count { get; set; }
}

public sealed class TrailerRecommendationRouteRequest : GarageRouteRequest
{
    public int? Count { get; set; }
}
