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
                new TruckDto("truck.current", "ATS-100", 725, "garage.phoenix", null),
                new TruckDto("truck.assigned", "ATS-200", 425, "garage.phoenix", "driver.alice"),
                new TruckDto("truck.historical", "ATS-300", 225, "garage.denver", null),
                new TruckDto("truck.other", "ATS-400", 125, "garage.denver", "driver.bob")
            ],
            [
                new MissionDto("job.1", "driver.alice", "truck.current", "reefer", "medicine", "phoenix", "denver", 3_000),
                new MissionDto("job.2", "driver.alice", "truck.historical", "flatbed", "steel", "denver", "phoenix", 1_500),
                new MissionDto("job.3", "driver.bob", "truck.other", "dryvan", "paper", "denver", "vegas", 900)
            ],
            []);
}
