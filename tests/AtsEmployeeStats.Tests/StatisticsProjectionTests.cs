using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Domain.Saves;
using AtsEmployeeStats.Infrastructure.Saves;

namespace AtsEmployeeStats.Tests;

public sealed class StatisticsProjectionTests
{
    [Fact]
    public void Build_aggregates_garages_drivers_trucks_missions_and_trailer_types_by_profit()
    {
        var snapshot = new SaveSnapshot(
            "autosave",
            new DateTimeOffset(2026, 5, 25, 21, 30, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                garage : garage.phoenix {
                  city: phoenix
                  profit_log[0]: 1000
                  profit_log[1]: 400
                  employees[0]: driver.alice
                  vehicles[0]: truck.alice
                }

                driver : driver.alice {
                  name: "Alice Ramirez"
                  profit_log[0]: 850
                  assigned_truck: truck.alice
                }

                vehicle : truck.alice {
                  license_plate: "ATS-100"
                  profit_log[0]: 725
                }

                job : _nameless.job.1 {
                  driver: driver.alice
                  truck: truck.alice
                  trailer: trailer.reefer.1
                  cargo: cargo.medicine
                  income: 2400
                  source_city: phoenix
                  target_city: denver
                }

                trailer : trailer.reefer.1 {
                  trailer_definition: trailer_def.scs.box.reefer
                }
                }
                """));

        var statistics = StatisticsProjection.Build([snapshot]);

        var company = Assert.Single(statistics.Companies);
        Assert.Equal("default", company.Id);

        var garage = Assert.Single(company.Garages);
        Assert.Equal("garage.phoenix", garage.Id);
        Assert.Equal("phoenix", garage.DisplayName);
        Assert.Equal(1400, garage.Profit);
        Assert.Equal(1, garage.EmployeeCount);
        Assert.Equal(1, garage.TruckCount);

        var driver = Assert.Single(company.Drivers);
        Assert.Equal("Alice Ramirez", driver.DisplayName);
        Assert.Equal(850, driver.Profit);
        Assert.Equal("garage.phoenix", driver.GarageId);
        Assert.Equal("truck.alice", driver.TruckId);

        var truck = Assert.Single(company.Trucks);
        Assert.Equal("ATS-100", truck.DisplayName);
        Assert.Equal(725, truck.Profit);
        Assert.Equal("garage.phoenix", truck.GarageId);
        Assert.Equal("driver.alice", truck.DriverId);

        var mission = Assert.Single(company.Missions);
        Assert.Equal(2400, mission.Profit);
        Assert.Equal("phoenix", mission.SourceCity);
        Assert.Equal("denver", mission.TargetCity);
        Assert.Equal("trailer_def.scs.box.reefer", mission.TrailerType);

        var trailerType = Assert.Single(company.TrailerTypes);
        Assert.Equal("trailer_def.scs.box.reefer", trailerType.Id);
        Assert.Equal(2400, trailerType.Profit);
        Assert.Equal(1, trailerType.MissionCount);
    }

    [Fact]
    public void Build_partitions_historical_saves_by_player_company()
    {
        var snapshots = new[]
        {
            Snapshot("profile-a-save-1", "Desert Line", 500),
            Snapshot("profile-b-save-1", "Copper State Haulage", 1200),
            Snapshot("profile-a-save-2", "Desert Line", 900)
        };

        var statistics = StatisticsProjection.Build(snapshots);

        Assert.Collection(
            statistics.Companies,
            company =>
            {
                Assert.Equal("Copper State Haulage", company.DisplayName);
                Assert.Equal(1200, Assert.Single(company.Garages).Profit);
            },
            company =>
            {
                Assert.Equal("Desert Line", company.DisplayName);
                Assert.Equal(900, Assert.Single(company.Garages).Profit);
            });
    }

    [Fact]
    public void Build_partitions_by_profile_path_when_company_name_is_missing()
    {
        var profileAPath = Path.Combine("profiles", "446573657274204C696E65", "save", "autosave", "game.sii");
        var profileBPath = Path.Combine("steam_profiles", "436F70706572204C696E65", "save", "autosave", "game.sii");

        var statistics = StatisticsProjection.Build(
        [
            SnapshotWithoutCompany(profileAPath, 100),
            SnapshotWithoutCompany(profileBPath, 200)
        ]);

        Assert.Collection(
            statistics.Companies,
            company =>
            {
                Assert.Equal("Copper Line", company.DisplayName);
                Assert.Equal(200, Assert.Single(company.Garages).Profit);
            },
            company =>
            {
                Assert.Equal("Desert Line", company.DisplayName);
                Assert.Equal(100, Assert.Single(company.Garages).Profit);
            });
    }

    [Fact]
    public void Build_keeps_unique_historical_missions_that_are_absent_from_latest_save()
    {
        var older = new SaveSnapshot(
            "save-1",
            new DateTimeOffset(2026, 5, 25, 20, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                player : player {
                  company_name: "Desert Line"
                }

                garage : garage.phoenix {
                  city: phoenix
                  profit_log[0]: 100
                }

                job : _nameless.job.old {
                  trailer: trailer.flatbed.1
                  income: 3000
                  source_city: phoenix
                  target_city: tucson
                }

                trailer : trailer.flatbed.1 {
                  trailer_definition: trailer_def.scs.flatbed
                }
                }
                """));

        var latest = new SaveSnapshot(
            "save-2",
            new DateTimeOffset(2026, 5, 25, 22, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                player : player {
                  company_name: "Desert Line"
                }

                garage : garage.phoenix {
                  city: phoenix
                  profit_log[0]: 250
                }

                job : _nameless.job.new {
                  trailer: trailer.reefer.1
                  income: 5000
                  source_city: phoenix
                  target_city: denver
                }

                trailer : trailer.reefer.1 {
                  trailer_definition: trailer_def.scs.box.reefer
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([older, latest]).Companies);

        Assert.Equal(250, Assert.Single(company.Garages).Profit);
        Assert.Collection(
            company.Missions,
            mission =>
            {
                Assert.Equal("_nameless.job.new", mission.Id);
                Assert.Equal(5000, mission.Profit);
            },
            mission =>
            {
                Assert.Equal("_nameless.job.old", mission.Id);
                Assert.Equal(3000, mission.Profit);
            });
        Assert.Equal(2, company.TrailerTypes.Count);
        Assert.Contains(company.TrailerTypes, trailer => trailer.Id == "trailer_def.scs.flatbed" && trailer.Profit == 3000);
        Assert.Contains(company.TrailerTypes, trailer => trailer.Id == "trailer_def.scs.box.reefer" && trailer.Profit == 5000);
    }

    [Fact]
    public void Build_reads_real_ats_profit_log_references_for_garages_and_ai_drivers()
    {
        var snapshot = new SaveSnapshot(
            "real-shape",
            new DateTimeOffset(2026, 5, 25, 23, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                garage : garage.sacramento {
                  vehicles: 1
                  vehicles[0]: truck.1
                  drivers: 1
                  drivers[0]: driver.23
                  profit_log: log.garage
                }

                driver_ai : driver.23 {
                  profit_log: log.driver
                  assigned_truck: truck.1
                }

                vehicle : truck.1 {
                  license_plate: "7M77679|california"
                }

                profit_log : log.garage {
                  stats_data: 2
                  stats_data[0]: entry.garage.1
                  stats_data[1]: entry.garage.2
                }

                profit_log_entry : entry.garage.1 {
                  revenue: 10000
                  wage: 2000
                  maintenance: 500
                  fuel: 250
                }

                profit_log_entry : entry.garage.2 {
                  revenue: 5000
                  wage: 1000
                  maintenance: 100
                  fuel: 50
                }

                profit_log : log.driver {
                  stats_data: 1
                  stats_data[0]: entry.driver.1
                }

                profit_log_entry : entry.driver.1 {
                  revenue: 8000
                  wage: 1500
                  maintenance: 400
                  fuel: 100
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([snapshot]).Companies);

        var garage = Assert.Single(company.Garages);
        Assert.Equal(11100, garage.Profit);
        Assert.Equal(1, garage.EmployeeCount);
        Assert.Equal(1, garage.TruckCount);

        var driver = Assert.Single(company.Drivers);
        Assert.Equal("driver.23", driver.DisplayName);
        Assert.Equal(6000, driver.Profit);
        Assert.Equal("garage.sacramento", driver.GarageId);
    }

    [Fact]
    public void Build_infers_driver_truck_assignment_from_garage_array_positions()
    {
        var snapshot = new SaveSnapshot(
            "garage-array-assignment",
            new DateTimeOffset(2026, 5, 26, 8, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                garage : garage.phoenix {
                  city: phoenix
                  drivers: 1
                  drivers[0]: driver.alice
                  vehicles: 1
                  vehicles[0]: truck.alice
                }

                driver_ai : driver.alice {
                  profit_log[0]: 1000
                }

                vehicle : truck.alice {
                  license_plate: "ATS-100"
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([snapshot]).Companies);

        var driver = Assert.Single(company.Drivers);
        Assert.Equal("truck.alice", driver.TruckId);

        var truck = Assert.Single(company.Trucks);
        Assert.Equal("driver.alice", truck.DriverId);
    }

    [Fact]
    public void Build_normalizes_pseudo_null_assignments_and_extracts_recent_driver_jobs()
    {
        var snapshot = new SaveSnapshot(
            "recent-driver-jobs",
            new DateTimeOffset(2026, 5, 26, 10, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                garage : garage.phoenix {
                  city: phoenix
                  drivers: 1
                  drivers[0]: driver.alice
                  vehicles: 1
                  vehicles[0]: truck.alice
                }

                driver_ai : driver.alice {
                  assigned_truck: null
                  profit_log: log.driver
                }

                profit_log : log.driver {
                  stats_data: 2
                  stats_data[0]: entry.old
                  stats_data[1]: entry.new
                }

                profit_log_entry : entry.old {
                  revenue: 1200
                  wage: 200
                  maintenance: 50
                  fuel: 25
                  distance: 300
                  cargo: cargo.apples
                  source_city: phoenix
                  destination_city: tucson
                  timestamp_day: 177
                }

                profit_log_entry : entry.new {
                  revenue: 2400
                  wage: 300
                  maintenance: 75
                  fuel: 25
                  distance: 450
                  cargo: cargo.medicine
                  source_city: tucson
                  destination_city: denver
                  timestamp_day: 178
                }

                vehicle : truck.alice {
                  license_plate: "ATS-100|arizona"
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([snapshot]).Companies);

        var driver = Assert.Single(company.Drivers);
        Assert.Equal("truck.alice", driver.TruckId);

        Assert.Collection(
            company.RecentDriverJobs,
            job =>
            {
                Assert.Equal("entry.new", job.Id);
                Assert.Equal("driver.alice", job.DriverId);
                Assert.Equal("cargo.medicine", job.Cargo);
                Assert.Equal("tucson", job.SourceCity);
                Assert.Equal("denver", job.TargetCity);
                Assert.Equal(2400, job.Revenue);
                Assert.Equal(400, job.Expenses);
                Assert.Equal(2000, job.Profit);
                Assert.Equal(450, job.Distance);
                Assert.Equal(178, job.TimestampDay);
            },
            job =>
            {
                Assert.Equal("entry.old", job.Id);
                Assert.Equal(925, job.Profit);
                Assert.Equal(177, job.TimestampDay);
            });
    }

    [Fact]
    public void Build_derives_truck_model_and_clean_license_plate_from_vehicle_accessories()
    {
        var snapshot = new SaveSnapshot(
            "truck-display",
            new DateTimeOffset(2026, 5, 26, 10, 30, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                garage : garage.billings {
                  city: billings
                  drivers: 1
                  drivers[0]: driver.alice
                  vehicles: 1
                  vehicles[0]: truck.alice
                }

                driver_ai : driver.alice {
                  assigned_truck: nil
                }

                vehicle : truck.alice {
                  license_plate: "<color value=FF000000> PA76356|montana"
                  accessories: 1
                  accessories[0]: accessory.base
                }

                vehicle_accessory : accessory.base {
                  data_path: "/def/vehicle/truck/kenworth.t680/data.sii"
                }
                }
                """));

        var truck = Assert.Single(Assert.Single(StatisticsProjection.Build([snapshot]).Companies).Trucks);

        Assert.Equal("Kenworth T680 - PA76356 Montana", truck.DisplayName);
        Assert.Equal("PA76356 Montana", truck.LicensePlate);
        Assert.Equal("Kenworth T680", truck.ModelName);
        Assert.Equal("/def/vehicle/truck/kenworth.t680/data.sii", truck.DefinitionPath);
    }

    [Fact]
    public void Build_formats_truck_model_tokens_with_years_and_suffixes()
    {
        var snapshot = new SaveSnapshot(
            "truck-display-year",
            new DateTimeOffset(2026, 5, 26, 10, 30, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                garage : garage.billings {
                  city: billings
                  vehicles: 2
                  vehicles[0]: truck.freightliner
                  vehicles[1]: truck.westernstar
                }

                vehicle : truck.freightliner {
                  accessories: 1
                  accessories[0]: accessory.freightliner
                }

                vehicle_accessory : accessory.freightliner {
                  data_path: "/def/vehicle/truck/freightliner.cascadia2019/data.sii"
                }

                vehicle : truck.westernstar {
                  accessories: 1
                  accessories[0]: accessory.westernstar
                }

                vehicle_accessory : accessory.westernstar {
                  data_path: "/def/vehicle/truck/westernstar.49x/data.sii"
                }
                }
                """));

        var trucks = Assert.Single(StatisticsProjection.Build([snapshot]).Companies).Trucks;

        Assert.Contains(trucks, truck => truck.ModelName == "Freightliner Cascadia 2019");
        Assert.Contains(trucks, truck => truck.ModelName == "Western Star 49X");
    }

    [Fact]
    public void Build_excludes_unowned_garages_with_zero_status()
    {
        var snapshot = new SaveSnapshot(
            "owned-garages",
            new DateTimeOffset(2026, 5, 26, 9, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                garage : garage.phoenix {
                  city: phoenix
                  status: 3
                  profit_log[0]: 1000
                }

                garage : garage.tucson {
                  city: tucson
                  status: 0
                  profit_log[0]: 500
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([snapshot]).Companies);

        var garage = Assert.Single(company.Garages);
        Assert.Equal("garage.phoenix", garage.Id);
    }

    [Fact]
    public void Build_treats_profit_log_entries_as_completed_missions()
    {
        var snapshot = new SaveSnapshot(
            "mission-history",
            new DateTimeOffset(2026, 5, 25, 23, 30, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                profit_log_entry : entry.1 {
                  revenue: 21925
                  wage: 14005
                  maintenance: 891
                  fuel: 552
                  distance: 786
                  cargo: clothes
                  source_city: albuquerque
                  destination_city: phoenix
                }
                }
                """));

        var mission = Assert.Single(Assert.Single(StatisticsProjection.Build([snapshot]).Companies).Missions);

        Assert.Equal("entry.1", mission.Id);
        Assert.Equal(6477, mission.Profit);
        Assert.Equal("clothes", mission.Cargo);
        Assert.Equal("albuquerque", mission.SourceCity);
        Assert.Equal("phoenix", mission.TargetCity);
    }

    [Fact]
    public void Build_reads_delivery_log_params_as_completed_missions_with_unknown_trailer_type()
    {
        var snapshot = new SaveSnapshot(
            "delivery-log",
            new DateTimeOffset(2026, 5, 26, 1, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                delivery_log_entry : delivery.1 {
                  params: 23
                  params[1]: "company.volatile.pns_con_sit.los_angeles"
                  params[2]: "company.volatile.pns_con_whs.phoenix"
                  params[3]: "cargo.const_house"
                  params[16]: "vehicle.kenworth.w900"
                  params[22]: "7030.700"
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([snapshot]).Companies);
        var mission = Assert.Single(company.Missions);

        Assert.Equal("delivery.1", mission.Id);
        Assert.Equal(7031, mission.Profit);
        Assert.Equal("cargo.const_house", mission.Cargo);
        Assert.Equal("los_angeles", mission.SourceCity);
        Assert.Equal("phoenix", mission.TargetCity);
        Assert.Equal("vehicle.kenworth.w900", mission.TruckId);
        Assert.Equal("unknown", mission.TrailerType);

        var trailerType = Assert.Single(company.TrailerTypes);
        Assert.Equal("unknown", trailerType.Id);
        Assert.Equal(7031, trailerType.Profit);
        Assert.Equal(1, trailerType.MissionCount);
    }

    [Fact]
    public void Build_deduplicates_same_delivery_log_mission_across_multiple_saves()
    {
        var older = DeliveryLogSnapshot("delivery.old.id", new DateTimeOffset(2026, 5, 26, 1, 0, 0, TimeSpan.Zero));
        var newer = DeliveryLogSnapshot("delivery.new.id", new DateTimeOffset(2026, 5, 26, 2, 0, 0, TimeSpan.Zero));

        var company = Assert.Single(StatisticsProjection.Build([older, newer]).Companies);

        var mission = Assert.Single(company.Missions);
        Assert.Equal(7031, mission.Profit);
        var trailerType = Assert.Single(company.TrailerTypes);
        Assert.Equal(7031, trailerType.Profit);
        Assert.Equal(1, trailerType.MissionCount);
    }

    [Fact]
    public void Build_ignores_delivery_log_entries_without_route_or_cargo()
    {
        var snapshot = new SaveSnapshot(
            "incomplete-delivery-log",
            new DateTimeOffset(2026, 5, 26, 2, 30, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                delivery_log_entry : delivery.incomplete {
                  params: 23
                  params[22]: "306318.000"
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([snapshot]).Companies);

        Assert.Empty(company.Missions);
        Assert.Empty(company.TrailerTypes);
    }

    [Fact]
    public void Build_ignores_profit_log_entries_without_route_or_cargo_as_missions()
    {
        var snapshot = new SaveSnapshot(
            "incomplete-profit-log",
            new DateTimeOffset(2026, 5, 26, 3, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                profit_log_entry : entry.incomplete {
                  revenue: 306318
                  wage: 0
                  maintenance: 0
                  fuel: 0
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([snapshot]).Companies);

        Assert.Empty(company.Missions);
        Assert.Empty(company.TrailerTypes);
    }

    private static SaveSnapshot Snapshot(string name, string companyName, long garageProfit) =>
        new(
            name,
            name.EndsWith('2')
                ? new DateTimeOffset(2026, 5, 25, 22, 0, 0, TimeSpan.Zero)
                : new DateTimeOffset(2026, 5, 25, 21, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse($$"""
                SiiNunit
                {
                player : player {
                  company_name: "{{companyName}}"
                }

                garage : garage.phoenix {
                  city: phoenix
                  profit_log[0]: {{garageProfit}}
                }
                }
                """));

    private static SaveSnapshot SnapshotWithoutCompany(string name, long garageProfit) =>
        new(
            name,
            new DateTimeOffset(2026, 5, 25, 21, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse($$"""
                SiiNunit
                {
                garage : garage.phoenix {
                  city: phoenix
                  profit_log[0]: {{garageProfit}}
                }
                }
                """));

    private static SaveSnapshot DeliveryLogSnapshot(string deliveryId, DateTimeOffset lastWritten) =>
        new(
            $"profiles{Path.DirectorySeparatorChar}446573657274204C696E65{Path.DirectorySeparatorChar}save{Path.DirectorySeparatorChar}{lastWritten.Ticks}{Path.DirectorySeparatorChar}game.sii",
            lastWritten,
            SiiSaveParser.Parse($$"""
                SiiNunit
                {
                delivery_log_entry : {{deliveryId}} {
                  params: 23
                  params[1]: "company.volatile.pns_con_sit.los_angeles"
                  params[2]: "company.volatile.pns_con_whs.phoenix"
                  params[3]: "cargo.const_house"
                  params[16]: "vehicle.kenworth.w900"
                  params[22]: "7030.700"
                }
                }
                """));
}
