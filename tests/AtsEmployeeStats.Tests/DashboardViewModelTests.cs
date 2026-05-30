using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Web.Services;

namespace AtsEmployeeStats.Tests;

public sealed class DashboardViewModelTests
{
    [Fact]
    public void Garage_filters_return_only_entities_assigned_to_the_garage()
    {
        var company = CreateCompany();

        var drivers = DashboardViewModel.GetGarageDrivers(company, "garage.phoenix");
        var trucks = DashboardViewModel.GetGarageTrucks(company, "garage.phoenix");

        Assert.Equal(["driver.alice"], drivers.Select(driver => driver.Id));
        Assert.Equal(["truck.current", "truck.assigned"], trucks.Select(truck => truck.Id));
    }

    [Fact]
    public void Driver_jobs_return_only_missions_assigned_to_the_driver()
    {
        var company = CreateCompany();

        var jobs = DashboardViewModel.GetDriverJobs(company, "driver.alice");

        Assert.Equal(["job.1", "job.2"], jobs.Select(job => job.Id));
    }

    [Fact]
    public void Driver_trucks_include_current_assignment_truck_assignment_and_historical_mission_trucks_once()
    {
        var company = CreateCompany();

        var trucks = DashboardViewModel.GetDriverTrucks(company, "driver.alice");

        Assert.Equal(["truck.current", "truck.assigned", "truck.historical"], trucks.Select(truck => truck.Id));
    }

    [Fact]
    public void Driver_recent_jobs_return_latest_four_for_driver()
    {
        var company = CreateCompany();

        var jobs = DashboardViewModel.GetDriverRecentJobs(company, "driver.alice");

        Assert.Equal(["recent.5", "recent.4", "recent.3", "recent.2"], jobs.Select(job => job.Id));
    }

    [Fact]
    public void Truck_display_name_resolves_known_trucks_before_returning_ids()
    {
        var company = CreateCompany();

        Assert.Equal("Kenworth T680 - ATS-100 Arizona", DashboardViewModel.GetTruckDisplayName(company, "truck.current"));
        Assert.Equal("truck.missing", DashboardViewModel.GetTruckDisplayName(company, "truck.missing"));
        Assert.Equal("-", DashboardViewModel.GetTruckDisplayName(company, null));
    }

    [Fact]
    public void Find_helpers_resolve_job_city_truck_trailer_and_route()
    {
        var company = CreateCompany();

        Assert.Equal("job.1", DashboardViewModel.FindJob(company, "job.1")?.Id);
        Assert.Equal("phoenix", DashboardViewModel.FindCity(company, "phoenix")?.Id);
        Assert.Equal("truck.current", DashboardViewModel.FindTruck(company, "truck.current")?.Id);
        Assert.Equal("trailer.reefer.1", DashboardViewModel.FindTrailer(company, "200B-420 Texas")?.Id);
        Assert.Equal("denver", DashboardViewModel.FindRoute(company, "phoenix", "denver")?.DestinationCityId);
    }

    [Fact]
    public void Related_helpers_return_scoped_child_rows_for_detail_pages()
    {
        var company = CreateCompany();

        var reeferTrailer = Assert.Single(company.Trailers ?? [], t => t.Id == "trailer.reefer.1");
        Assert.Equal(["job.1"], DashboardViewModel.GetTruckJobs(company, "truck.current").Select(job => job.Id));
        Assert.Equal(["job.1", "job.2"], DashboardViewModel.GetTrailerJobs(company, reeferTrailer).Select(job => job.Id));
        Assert.Equal(["job.1", "job.2"], DashboardViewModel.GetCityJobs(company, "phoenix").Select(job => job.Id));
        Assert.Equal(["phoenix->denver", "denver->phoenix"], DashboardViewModel.GetCityRoutes(company, "phoenix").Select(route => $"{route.OriginCityId}->{route.DestinationCityId}"));
        Assert.Equal(["truck.current", "truck.historical"], DashboardViewModel.GetTrailerTrucks(company, reeferTrailer).Select(truck => truck.Id));
    }

    [Fact]
    public void City_trailer_type_breakdown_aggregates_profit_by_type_sorted_descending()
    {
        var company = CreateCompany();

        var breakdown = DashboardViewModel.GetCityTrailerTypeBreakdown(company, "denver");

        Assert.Equal(
            [("reefer", 4_500L), ("dryvan", 900L)],
            breakdown.Select(x => (x.TrailerType, x.Profit)));
    }

    [Fact]
    public void City_trailer_type_breakdown_excludes_null_and_unknown_trailer_types()
    {
        var company = CreateCompany() with
        {
            Missions = [
                new MissionDto("job.x", "driver.alice", "truck.current", null, "food", "phoenix", "denver", 5_000),
                new MissionDto("job.u", "driver.alice", "truck.current", "unknown", "food", "phoenix", "denver", 3_000),
                new MissionDto("job.y", "driver.alice", "truck.current", "flatbed", "food", "phoenix", "denver", 2_000)
            ]
        };

        var breakdown = DashboardViewModel.GetCityTrailerTypeBreakdown(company, "phoenix");

        Assert.Equal([("flatbed", 2_000L)], breakdown.Select(x => (x.TrailerType, x.Profit)));
    }

    [Fact]
    public void GetGarageTrailers_returns_only_player_owned_trailers_used_by_trucks_at_that_garage()
    {
        var company = CreateCompany();

        var phoenixTrailers = DashboardViewModel.GetGarageTrailers(company, "garage.phoenix");
        var denverTrailers = DashboardViewModel.GetGarageTrailers(company, "garage.denver");

        // trailer.reefer.1 has GarageId = "garage.phoenix"
        Assert.Equal(["trailer.reefer.1"], phoenixTrailers.Select(t => t.Id));
        // trailer.dryvan.1 has GarageId = "garage.denver"
        Assert.Equal(["trailer.dryvan.1"], denverTrailers.Select(t => t.Id));
    }

    [Fact]
    public void GetGarageTrailers_excludes_trailers_without_a_garage_id()
    {
        var company = CreateCompany() with
        {
            Trailers = [
                new TrailerDto("trailer.reefer.1", "reefer", 4_500, 2, LicensePlate: "200B-420 Texas"),
                new TrailerDto("trailer.dryvan.1", "dryvan", 900, 1)
                // Neither has a GarageId set
            ]
        };

        var phoenixTrailers = DashboardViewModel.GetGarageTrailers(company, "garage.phoenix");

        Assert.Empty(phoenixTrailers);
    }

    private static CompanyDto CreateCompany() =>
        new(
            "desert-line",
            "Desert Line",
            10_000,
            [
                new GarageDto("garage.phoenix", "phoenix", 5_000, 714, 1, 2),
                new GarageDto("garage.denver", "denver", 4_000, 571, 1, 1)
            ],
            [
                new DriverDto("driver.alice", "Alice Ramirez", 3_000, 429, "garage.phoenix", "truck.current", 2),
                new DriverDto("driver.bob", "Bob Lee", 2_000, 286, "garage.denver", "truck.other", 1)
            ],
            [
                new TruckDto(
                    "truck.current",
                    "Kenworth T680 - ATS-100 Arizona",
                    725,
                    "garage.phoenix",
                    null,
                    "ATS-100 Arizona",
                    "Kenworth T680",
                    "/def/vehicle/truck/kenworth.t680/data.sii"),
                new TruckDto("truck.assigned", "ATS-200", 425, "garage.phoenix", "driver.alice"),
                new TruckDto("truck.historical", "ATS-300", 225, "garage.denver", null),
                new TruckDto("truck.other", "ATS-400", 125, "garage.denver", "driver.bob")
            ],
            [
                new MissionDto("job.1", "driver.alice", "truck.current", "reefer", "medicine", "phoenix", "denver", 3_000, TrailerId: "trailer.reefer.1", GarageId: "garage.phoenix", TrailerLicensePlate: "200B-420 Texas"),
                new MissionDto("job.2", "driver.alice", "truck.historical", "reefer", "steel", "denver", "phoenix", 1_500, TrailerId: "trailer.reefer.1", GarageId: "garage.denver", TrailerLicensePlate: "200B-420 Texas"),
                new MissionDto("job.3", "driver.bob", "truck.other", "dryvan", "paper", "denver", "vegas", 900, TrailerId: "trailer.dryvan.1", GarageId: "garage.denver", TrailerLicensePlate: "425K-180 Arizona")
            ],
            [],
            [
                new DriverRecentJobDto("recent.1", "driver.alice", "truck.current", "food", "phoenix", "tucson", 1_100, 100, 1_000, 120, 101),
                new DriverRecentJobDto("recent.2", "driver.alice", "truck.current", "paper", "tucson", "phoenix", 1_200, 100, 1_100, 130, 102),
                new DriverRecentJobDto("recent.3", "driver.alice", "truck.assigned", "steel", "phoenix", "flagstaff", 1_300, 100, 1_200, 140, 103),
                new DriverRecentJobDto("recent.4", "driver.alice", "truck.historical", "logs", "flagstaff", "phoenix", 1_400, 100, 1_300, 150, 104),
                new DriverRecentJobDto("recent.5", "driver.alice", "truck.current", "medicine", "phoenix", "denver", 1_500, 100, 1_400, 160, 105),
                new DriverRecentJobDto("recent.bob", "driver.bob", "truck.other", "paper", "denver", "vegas", 900, 100, 800, 90, 106)
            ],
            [
                new TrailerDto("trailer.reefer.1", "reefer", 4_500, 2, GarageId: "garage.phoenix", LicensePlate: "200B-420 Texas"),
                new TrailerDto("trailer.dryvan.1", "dryvan", 900, 1, GarageId: "garage.denver", LicensePlate: "425K-180 Arizona")
            ],
            [
                new CityDto("phoenix", "Phoenix", true, true, 2, 3_000, 1_500, 4_500, 0),
                new CityDto("denver", "Denver", false, true, 3, 2_400, 3_000, 4_500, 2.25m)
            ],
            [
                new RouteDto("phoenix", "denver", 3_000, 1, 0, 1),
                new RouteDto("denver", "phoenix", 1_500, 1, 0, 1),
                new RouteDto("denver", "vegas", 900, 1, 0, 0)
            ],
            new SparklineDto(
                7,
                [
                    new EntityTrendPointDto(200, null, 3_000, 1),
                    new EntityTrendPointDto(201, null, 1_500, 1)
                ]));
}
