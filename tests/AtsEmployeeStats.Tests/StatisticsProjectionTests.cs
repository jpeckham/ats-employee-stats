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
        Assert.Equal("Phoenix", garage.DisplayName);
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
    public void Build_partitions_same_company_name_by_save_source()
    {
        var statistics = StatisticsProjection.Build(
        [
            Snapshot("ats-autosave", "Acme Trucking", 500, sourceKey: "ats:profile-a:autosave"),
            Snapshot("ets2-autosave", "Acme Trucking", 1200, sourceKey: "ets2:profile-b:autosave")
        ]);

        Assert.Collection(
            statistics.Companies.OrderBy(company => company.Id, StringComparer.OrdinalIgnoreCase),
            company =>
            {
                Assert.Equal("ats-profile-a-autosave:acme-trucking", company.Id);
                Assert.Equal("Acme Trucking", company.DisplayName);
                Assert.Equal(500, Assert.Single(company.Garages).Profit);
            },
            company =>
            {
                Assert.Equal("ets2-profile-b-autosave:acme-trucking", company.Id);
                Assert.Equal("Acme Trucking", company.DisplayName);
                Assert.Equal(1200, Assert.Single(company.Garages).Profit);
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
    public void Build_tracks_historical_driver_truck_and_garage_assignments_across_snapshots()
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
                  drivers: 1
                  drivers[0]: driver.alice
                  vehicles: 1
                  vehicles[0]: truck.old
                }

                driver_ai : driver.alice {
                  assigned_truck: truck.old
                }

                vehicle : truck.old {
                  license_plate: "OLD"
                }
                }
                """));

        var newer = new SaveSnapshot(
            "save-2",
            new DateTimeOffset(2026, 5, 25, 21, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                player : player {
                  company_name: "Desert Line"
                }

                garage : garage.denver {
                  city: denver
                  drivers: 1
                  drivers[0]: driver.alice
                  vehicles: 1
                  vehicles[0]: truck.new
                }

                driver_ai : driver.alice {
                  assigned_truck: truck.new
                }

                vehicle : truck.new {
                  license_plate: "NEW"
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([older, newer]).Companies);

        Assert.Collection(
            company.DriverTruckAssignments,
            assignment =>
            {
                Assert.Equal("driver.alice", assignment.DriverId);
                Assert.Equal("truck.old", assignment.TruckId);
                Assert.Equal("save-1", assignment.EffectiveFromSaveName);
                Assert.Equal("save-2", assignment.EffectiveToSaveName);
                Assert.False(assignment.IsCurrent);
            },
            assignment =>
            {
                Assert.Equal("driver.alice", assignment.DriverId);
                Assert.Equal("truck.new", assignment.TruckId);
                Assert.Equal("save-2", assignment.EffectiveFromSaveName);
                Assert.Null(assignment.EffectiveToSaveName);
                Assert.True(assignment.IsCurrent);
            });

        Assert.Collection(
            company.DriverGarageAssignments,
            assignment =>
            {
                Assert.Equal("garage.phoenix", assignment.GarageId);
                Assert.Equal("save-1", assignment.EffectiveFromSaveName);
                Assert.Equal("save-2", assignment.EffectiveToSaveName);
                Assert.False(assignment.IsCurrent);
            },
            assignment =>
            {
                Assert.Equal("garage.denver", assignment.GarageId);
                Assert.Equal("save-2", assignment.EffectiveFromSaveName);
                Assert.Null(assignment.EffectiveToSaveName);
                Assert.True(assignment.IsCurrent);
            });
    }

    [Fact]
    public void Build_attributes_profit_log_entry_missions_to_drivers_via_stats_data_reverse_map()
    {
        var snapshot = new SaveSnapshot(
            "reverse-map-test",
            new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                player : player {
                  company_name: "Test Co"
                }

                garage : garage.phoenix {
                  city: phoenix
                  drivers[0]: driver_ai.23
                  vehicles[0]: truck.1
                }

                driver_ai : driver_ai.23 {
                  profit_log: profit_log.driver_ai.23
                }

                vehicle : truck.1 {
                  license_plate: "TX-100"
                }

                profit_log : profit_log.driver_ai.23 {
                  stats_data[0]: _nameless.1ca.2b0e.8630
                  stats_data[1]: _nameless.268.d1f5.8870
                }

                profit_log_entry : _nameless.1ca.2b0e.8630 {
                  revenue: 56505
                  wage: 10000
                  maintenance: 1000
                  fuel: 500
                  cargo: space_cont
                  source_city: houston
                  destination_city: seattle
                  timestamp_day: 159
                }

                profit_log_entry : _nameless.268.d1f5.8870 {
                  revenue: 51314
                  wage: 8000
                  maintenance: 900
                  fuel: 450
                  cargo: ammonia
                  source_city: cheyenne
                  destination_city: tacoma
                  timestamp_day: 183
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([snapshot]).Companies);

        Assert.Equal(2, company.Missions.Count);
        Assert.All(company.Missions, m =>
        {
            Assert.Equal("driver_ai.23", m.DriverId);
        });

        var driver = Assert.Single(company.Drivers);
        Assert.Equal(2, company.Missions.Count(m => StringComparer.OrdinalIgnoreCase.Equals(m.DriverId, driver.Id)));

        var garage = Assert.Single(company.Garages);
        Assert.Equal("Phoenix", garage.DisplayName);
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
    public void Build_excludes_unowned_garages_with_zero_or_one_status()
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

                garage : garage.sacramento {
                  city: sacramento
                  status: 1
                  profit_log[0]: 800
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
    public void Build_creates_city_route_trailer_and_trend_read_models_from_jobs()
    {
        var snapshot = new SaveSnapshot(
            "city-route-models",
            new DateTimeOffset(2026, 5, 26, 4, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                player : player {
                  company_name: "Desert Line"
                }

                garage : garage.phoenix {
                  city: phoenix
                  employees[0]: driver.alice
                  vehicles[0]: truck.alice
                }

                garage : garage.denver {
                  city: denver
                  status: 0
                }

                driver : driver.alice {
                  name: "Alice Ramirez"
                  assigned_truck: truck.alice
                }

                vehicle : truck.alice {
                  license_plate: "ATS-100"
                }

                trailer : trailer.reefer.1 {
                  trailer_definition: trailer_def.scs.box.reefer
                  license_plate: "200B-420|texas"
                }

                job : job.outbound {
                  driver: driver.alice
                  truck: truck.alice
                  trailer: trailer.reefer.1
                  cargo: cargo.medicine
                  income: 3000
                  source_city: phoenix
                  target_city: denver
                  timestamp_day: 200
                }

                job : job.return {
                  driver: driver.alice
                  truck: truck.alice
                  trailer: trailer.reefer.1
                  cargo: cargo.paper
                  income: 2500
                  source_city: denver
                  target_city: phoenix
                  timestamp_day: 201
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([snapshot]).Companies);

        Assert.Collection(
            company.Cities,
            city =>
            {
                Assert.Equal("phoenix", city.Id);
                Assert.True(city.HasOwnedGarage);
                Assert.True(city.IsGarageEligible);
                Assert.Equal(2, city.VisitCount);
                Assert.Equal(3000, city.OutboundProfit);
                Assert.Equal(2500, city.InboundProfit);
                Assert.Equal(5500, city.BidirectionalProfit);
                Assert.True(city.ExpansionScore < 1);
            },
            city =>
            {
                Assert.Equal("denver", city.Id);
                Assert.False(city.HasOwnedGarage);
                Assert.True(city.IsGarageEligible);
                Assert.Equal(2, city.VisitCount);
                Assert.Equal(2500, city.OutboundProfit);
                Assert.Equal(3000, city.InboundProfit);
                Assert.Equal(5500, city.BidirectionalProfit);
                Assert.True(city.ExpansionScore > 0);
            });

        Assert.Collection(
            company.Routes,
            route =>
            {
                Assert.Equal("denver", route.OriginCityId);
                Assert.Equal("phoenix", route.DestinationCityId);
                Assert.Equal(2500, route.Profit);
                Assert.Equal(1, route.JobCount);
                Assert.Equal(1m, route.ReturnCoverageRatio);
            },
            route =>
            {
                Assert.Equal("phoenix", route.OriginCityId);
                Assert.Equal("denver", route.DestinationCityId);
                Assert.Equal(3000, route.Profit);
                Assert.Equal(1, route.JobCount);
                Assert.Equal(1m, route.ReturnCoverageRatio);
            });

        var trailer = Assert.Single(company.Trailers);
        Assert.Equal("trailer.reefer.1", trailer.Id);
        Assert.Equal("trailer_def.scs.box.reefer", trailer.TrailerType);
        Assert.Equal(5500, trailer.Profit);
        // JobCount comes from trailer_utilization_log; no log in this snapshot so it's 0
        Assert.Equal(0, trailer.JobCount);
        Assert.Equal("200B-420 Texas", trailer.LicensePlate);

        Assert.Collection(
            company.ProfitTrends.Where(point => point.EntityKind == "company"),
            point =>
            {
                Assert.Equal("desert-line", point.EntityId);
                Assert.Equal(200, point.GameDay);
                Assert.Equal(3000, point.Profit);
                Assert.Equal(1, point.SampleCount);
            },
            point =>
            {
                Assert.Equal(201, point.GameDay);
                Assert.Equal(2500, point.Profit);
                Assert.Equal(1, point.SampleCount);
            });

        Assert.Collection(
            company.ProfitTrends.Where(point => point.EntityKind == "trailer").OrderBy(p => p.GameDay),
            point =>
            {
                Assert.Equal("200B-420 Texas", point.EntityId);
                Assert.Equal(200, point.GameDay);
                Assert.Equal(3000, point.Profit);
            },
            point =>
            {
                Assert.Equal("200B-420 Texas", point.EntityId);
                Assert.Equal(201, point.GameDay);
                Assert.Equal(2500, point.Profit);
            });
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

    [Fact]
    public void Build_sets_trailer_garage_id_from_garage_trailers_array_and_job_count_from_utilization_log()
    {
        var snapshot = new SaveSnapshot(
            "trailer-garage-jobcount",
            new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                player : player {
                  company_name: "Desert Line"
                  trailers[0]: trailer.reefer.1
                  trailer_utilization_logs[0]: trailer_log.reefer.1
                }

                garage : garage.phoenix {
                  profit_log[0]: 1000
                  employees[0]: driver.alice
                  vehicles[0]: truck.alice
                  trailers[0]: trailer.reefer.1
                }

                driver : driver.alice {
                  name: "Alice"
                  profit_log[0]: 500
                }

                vehicle : truck.alice {
                  profit_log[0]: 300
                }

                trailer : trailer.reefer.1 {
                  trailer_definition: trailer_def.scs.box.reefer
                }

                trailer_utilization_log : trailer_log.reefer.1 {
                  total_transported_cargoes: 42
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([snapshot]).Companies);

        var trailer = Assert.Single(company.Trailers);
        Assert.Equal("trailer.reefer.1", trailer.Id);
        Assert.Equal("garage.phoenix", trailer.GarageId);
        Assert.Equal(42, trailer.JobCount);
    }

    [Fact]
    public void Build_attributes_missions_from_all_snapshots_to_trailer_by_license_plate()
    {
        // Snapshot 1: trailer has unit_id "trailer.A" — an older save before a game reload
        var snapshot1 = new SaveSnapshot(
            "save-1",
            new DateTimeOffset(2026, 5, 29, 10, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                garage : garage.phoenix {
                  employees[0]: driver.alice
                  trailers[0]: trailer.A
                }

                driver : driver.alice {
                }

                trailer : trailer.A {
                  trailer_definition: trailer_def.scs.box.reefer
                  license_plate: "200B-420|texas"
                }

                trailer_def : trailer_def.scs.box.reefer {
                  body_type: "box"
                  chain_type: "double"
                }

                trailer_utilization_log : trailer_log.A {
                  total_transported_cargoes: 1
                }

                job : job.old {
                  trailer: trailer.A
                  income: 2000
                  source_city: phoenix
                  target_city: denver
                  timestamp_day: 150
                }
                }
                """));

        // Snapshot 2: same physical trailer, unit_id reassigned to "trailer.B" after game reload
        var snapshot2 = new SaveSnapshot(
            "save-2",
            new DateTimeOffset(2026, 5, 30, 10, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                garage : garage.phoenix {
                  employees[0]: driver.alice
                  trailers[0]: trailer.B
                }

                driver : driver.alice {
                }

                trailer : trailer.B {
                  trailer_definition: trailer_def.scs.box.reefer
                  license_plate: "200B-420|texas"
                }

                trailer_def : trailer_def.scs.box.reefer {
                  body_type: "box"
                  chain_type: "double"
                }

                trailer_utilization_log : trailer_log.B {
                  total_transported_cargoes: 1
                }

                job : job.new {
                  trailer: trailer.B
                  income: 3000
                  source_city: denver
                  target_city: phoenix
                  timestamp_day: 200
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([snapshot1, snapshot2]).Companies);
        var trailer = Assert.Single(company.Trailers);

        // Both jobs (2000 + 3000) should be attributed to the license plate "200B-420 Texas"
        Assert.Equal("200B-420 Texas", trailer.LicensePlate);
        Assert.Equal(5000, trailer.Profit);

        // Both missions should carry the trailer's license plate
        Assert.All(company.Missions, m => Assert.Equal("200B-420 Texas", m.TrailerLicensePlate));

        // Trend points should use "200B-420 Texas" as EntityId
        var trailerTrends = company.ProfitTrends.Where(p => p.EntityKind == "trailer").ToList();
        Assert.Equal(2, trailerTrends.Count);
        Assert.All(trailerTrends, p => Assert.Equal("200B-420 Texas", p.EntityId));
    }

    [Fact]
    public void Build_extracts_license_plate_from_trailer_unit()
    {
        var snapshot = new SaveSnapshot(
            "trailer-plate",
            new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                garage : garage.phoenix {
                  employees[0]: driver.alice
                  trailers[0]: trailer.reefer.1
                }

                driver : driver.alice {
                }

                trailer : trailer.reefer.1 {
                  trailer_definition: trailer_def.scs.box.reefer
                  license_plate: "TRL-001|california"
                }

                job : job.1 {
                  trailer: trailer.reefer.1
                  income: 1000
                  source_city: phoenix
                  target_city: los_angeles
                }
                }
                """));

        var trailer = Assert.Single(Assert.Single(StatisticsProjection.Build([snapshot]).Companies).Trailers);

        Assert.Equal("TRL-001 California", trailer.LicensePlate);
    }

    private static SaveSnapshot Snapshot(string name, string companyName, long garageProfit, string? sourceKey = null) =>
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
                """),
            sourceKey);

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

    [Fact]
    public void Build_includes_historical_garage_when_sold_in_latest_snapshot_and_attributes_missions_to_it()
    {
        var older = new SaveSnapshot(
            "save-1",
            new DateTimeOffset(2026, 5, 25, 20, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                player : player { company_name: "Desert Line" }

                garage : garage.sacramento {
                  city: sacramento
                  drivers[0]: driver.bob
                  vehicles[0]: truck.1
                }

                driver_ai : driver.bob { assigned_truck: truck.1 }

                vehicle : truck.1 { license_plate: "CA-001" }

                job : job.sacramento.run {
                  driver: driver.bob
                  truck: truck.1
                  income: 5000
                  source_city: sacramento
                  target_city: reno
                  timestamp_day: 100
                }
                }
                """));

        var latest = new SaveSnapshot(
            "save-2",
            new DateTimeOffset(2026, 5, 25, 22, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                player : player { company_name: "Desert Line" }

                garage : garage.sacramento {
                  city: sacramento
                  status: 1
                }

                garage : garage.fresno {
                  city: fresno
                  drivers[0]: driver.bob
                  vehicles[0]: truck.1
                }

                driver_ai : driver.bob { assigned_truck: truck.1 }

                vehicle : truck.1 { license_plate: "CA-001" }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([older, latest]).Companies);

        // Both garages should appear — sacramento is historical, fresno is current
        Assert.Equal(2, company.Garages.Count);

        var sacramento = Assert.Single(company.Garages, g => g.Id == "garage.sacramento");
        Assert.Equal(0, sacramento.EmployeeCount);
        Assert.Equal(0, sacramento.TruckCount);

        var fresno = Assert.Single(company.Garages, g => g.Id == "garage.fresno");
        Assert.Equal(1, fresno.EmployeeCount);
        Assert.Equal(1, fresno.TruckCount);

        // The mission done while bob was at Sacramento must attribute to Sacramento
        var mission = Assert.Single(company.Missions);
        Assert.Equal("job.sacramento.run", mission.Id);
        Assert.Equal("garage.sacramento", mission.GarageId);

        // The sold city must not show as owned in the cities view
        var sacramentoCity = Assert.Single(company.Cities, c => c.Id == "sacramento");
        Assert.False(sacramentoCity.HasOwnedGarage);
    }

    [Fact]
    public void Build_rescues_driver_attribution_from_older_snapshot_when_entry_ages_out_of_profit_log()
    {
        // Simulates the real-world ATS rolling profit_log:
        // older save has entry in stats_data (attributed), newer save dropped it (orphaned).
        // The deduplication must not discard the driverId from the older snapshot.
        const string entryId = "_nameless.1f3.aaa.bbb";

        var older = new SaveSnapshot(
            "profiles/save/older/game.sii",
            new DateTimeOffset(2026, 5, 25, 0, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse($$"""
                SiiNunit
                {
                player : player {
                  company_name: "Desert Line"
                }
                garage : garage.phoenix {
                  city: phoenix
                  employees[0]: driver.ai.1
                  vehicles[0]: truck.1
                }
                driver_ai : driver.ai.1 {
                  profit_log: profit_log.1
                }
                profit_log : profit_log.1 {
                  stats_data[0]: {{entryId}}
                }
                profit_log_entry : {{entryId}} {
                  revenue: 3000
                  cargo: cargo.medicine
                  source_city: phoenix
                  destination_city: denver
                  timestamp_day: 100
                }
                vehicle : truck.1 {
                  license_plate: "T-1"
                }
                }
                """));

        // newer save: entry aged out of stats_data but unit still present in save file
        var newer = new SaveSnapshot(
            "profiles/save/newer/game.sii",
            new DateTimeOffset(2026, 5, 26, 0, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse($$"""
                SiiNunit
                {
                player : player {
                  company_name: "Desert Line"
                }
                garage : garage.phoenix {
                  city: phoenix
                  employees[0]: driver.ai.1
                  vehicles[0]: truck.1
                }
                driver_ai : driver.ai.1 {
                  profit_log: profit_log.1
                }
                profit_log : profit_log.1 {
                  stats_data[0]: _nameless.26b.new.entry
                }
                profit_log_entry : {{entryId}} {
                  revenue: 3000
                  cargo: cargo.medicine
                  source_city: phoenix
                  destination_city: denver
                  timestamp_day: 100
                }
                profit_log_entry : _nameless.26b.new.entry {
                  revenue: 2500
                  cargo: cargo.paper
                  source_city: denver
                  destination_city: phoenix
                  timestamp_day: 200
                }
                vehicle : truck.1 {
                  license_plate: "T-1"
                }
                }
                """));

        var statistics = StatisticsProjection.Build([older, newer]);

        var company = Assert.Single(statistics.Companies);
        var missions = company.Missions.OrderBy(m => m.TimestampDay).ToList();
        Assert.Equal(2, missions.Count);
        Assert.All(missions, m => Assert.Equal("driver.ai.1", m.DriverId));
    }

    [Fact]
    public void Build_attributes_driver_profit_log_jobs_to_assigned_truck_when_entry_has_no_truck()
    {
        var snapshot = new SaveSnapshot(
            "profiles/save/current/game.sii",
            new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero),
            SiiSaveParser.Parse("""
                SiiNunit
                {
                player : player {
                  company_name: "Desert Line"
                }

                garage : garage.phoenix {
                  city: phoenix
                  employees[0]: driver.ai.1
                  vehicles[0]: truck.1
                }

                driver_ai : driver.ai.1 {
                  profit_log: profit_log.1
                }

                profit_log : profit_log.1 {
                  stats_data[0]: profit.entry.1
                }

                profit_log_entry : profit.entry.1 {
                  revenue: 3000
                  cargo: cargo.medicine
                  source_city: phoenix
                  destination_city: denver
                  timestamp_day: 100
                }

                vehicle : truck.1 {
                  license_plate: "T-1"
                }
                }
                """));

        var company = Assert.Single(StatisticsProjection.Build([snapshot]).Companies);

        var truck = Assert.Single(company.Trucks);
        Assert.Equal("truck.1", truck.Id);
        Assert.Equal("driver.ai.1", truck.DriverId);
        Assert.Equal(3000, truck.Profit);

        var mission = Assert.Single(company.Missions);
        Assert.Equal("driver.ai.1", mission.DriverId);
        Assert.Equal("truck.1", mission.TruckId);
        Assert.Equal(3000, mission.Profit);
    }
}
