namespace AtsEmployeeStats.Tests;

public sealed class CommandLineOptionsTests
{
    [Fact]
    public void Parse_reads_once_history_days_and_view()
    {
        var options = CommandLineOptions.Parse(
        [
            "--save-root",
            "C:\\ATS",
            "--once",
            "--history-days",
            "14",
            "--view",
            "trailers"
        ]);

        Assert.Equal("C:\\ATS", options.SaveRoot);
        Assert.True(options.Once);
        Assert.Equal(14, options.HistoryDays);
        Assert.Equal(DashboardView.Trailers, options.View);
    }
}
