using AtsEmployeeStats.Infrastructure.Saves;

namespace AtsEmployeeStats.Tests;

public sealed class SiiSaveParserTests
{
    [Fact]
    public void Parse_reads_units_with_scalar_and_array_fields()
    {
        const string save = """
            SiiNunit
            {
            garage : garage.phoenix {
              city: phoenix
              profit_log: 2
              profit_log[0]: 12500
              profit_log[1]: -250
              employees[0]: driver.alice
              vehicles[0]: truck.alice
            }

            driver : driver.alice {
              name: "Alice Ramirez"
              assigned_truck: truck.alice
            }
            }
            """;

        var document = SiiSaveParser.Parse(save);

        var garage = Assert.Single(document.Units, unit => unit.Type == "garage");
        Assert.Equal("garage.phoenix", garage.Id);
        Assert.Equal("phoenix", garage.GetValue("city"));
        var profitLog = garage.GetArray("profit_log");
        Assert.Equal(2, profitLog.Count);
        Assert.Equal("12500", profitLog["0"]);
        Assert.Equal("-250", profitLog["1"]);
        var employees = garage.GetArray("employees");
        Assert.Single(employees);
        Assert.Equal("driver.alice", employees["0"]);

        var driver = Assert.Single(document.Units, unit => unit.Type == "driver");
        Assert.Equal("Alice Ramirez", driver.GetValue("name"));
        Assert.Equal("truck.alice", driver.GetValue("assigned_truck"));
    }
}
