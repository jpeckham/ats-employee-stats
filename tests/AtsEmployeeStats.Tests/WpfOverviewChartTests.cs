using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Wpf.ViewModels;
using LiveChartsCore.SkiaSharpView;

namespace AtsEmployeeStats.Tests;

public sealed class WpfOverviewChartTests
{
    [Fact]
    public void Company_daily_charts_exclude_the_latest_timestamped_day()
    {
        var company = new CompanyDto(
            "company-1",
            "Company One",
            600,
            [],
            [],
            [],
            [
                Mission("job-1", 100, 10),
                Mission("job-2", 200, 11),
                Mission("job-3", 300, 12),
            ],
            []);

        var overview = CompanyOverviewBuilder.Build(company);

        AssertChart(overview, "Profit by Day", ["10", "11"], [100, 200]);
        AssertChart(overview, "Jobs by Day", ["10", "11"], [1, 1]);
    }

    private static MissionDto Mission(string id, long profit, int timestampDay) =>
        new(id, null, null, null, null, null, null, profit, timestampDay);

    private static void AssertChart(
        OverviewViewModel overview,
        string title,
        string[] expectedLabels,
        long[] expectedValues)
    {
        var chart = Assert.Single(overview.TrendCharts, chart => chart.Title == title);
        var series = Assert.IsType<LineSeries<long>>(Assert.Single(chart.Series));

        Assert.Equal(expectedLabels, chart.XAxes.Single().Labels);
        Assert.Equal(expectedValues, series.Values);
    }
}
