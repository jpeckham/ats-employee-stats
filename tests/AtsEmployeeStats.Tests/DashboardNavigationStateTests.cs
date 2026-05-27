using AtsEmployeeStats.Web.Services;

namespace AtsEmployeeStats.Tests;

public sealed class DashboardNavigationStateTests
{
    [Fact]
    public void Breadcrumb_tracks_company_garage_and_driver_selection()
    {
        var state = new DashboardNavigationState();

        state.SelectCompany("desert-line", "Desert Line");
        state.SelectGarage("garage.phoenix", "phoenix");
        state.SelectDriver("driver.alice", "Alice Ramirez");

        Assert.Equal(["Desert Line", "phoenix", "Alice Ramirez"], state.Breadcrumb);
        Assert.Equal("driver.alice", state.DriverId);
    }

    [Fact]
    public void Back_moves_out_one_drilldown_level_at_a_time()
    {
        var state = new DashboardNavigationState();
        state.SelectCompany("desert-line", "Desert Line");
        state.SelectGarage("garage.phoenix", "phoenix");
        state.SelectDriver("driver.alice", "Alice Ramirez");

        state.Back();
        Assert.Null(state.DriverId);
        Assert.Equal(["Desert Line", "phoenix"], state.Breadcrumb);

        state.Back();
        Assert.Null(state.GarageId);
        Assert.Equal(["Desert Line"], state.Breadcrumb);

        state.Back();
        Assert.Null(state.CompanyId);
        Assert.Empty(state.Breadcrumb);
    }
}
