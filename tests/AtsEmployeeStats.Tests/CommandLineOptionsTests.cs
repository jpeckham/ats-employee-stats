namespace AtsEmployeeStats.Tests;

public sealed class CommandLineOptionsTests
{
    [Fact]
    public void Parse_defaults_history_days_to_14()
    {
        var options = CommandLineOptions.Parse([]);

        Assert.Equal(14, options.HistoryDays);
    }

    [Fact]
    public void Parse_reads_once_history_days_and_view()
    {
        var options = CommandLineOptions.Parse(
        [
            "--save-root",
            "C:\\ATS",
            "--db-path",
            "C:\\ATS\\stats.db",
            "--once",
            "--history-days",
            "14",
            "--view",
            "trailers"
        ]);

        Assert.Equal("C:\\ATS", options.SaveRoot);
        Assert.Equal("C:\\ATS\\stats.db", options.DbPath);
        Assert.True(options.Once);
        Assert.Equal(14, options.HistoryDays);
        Assert.Equal(DashboardView.Trailers, options.View);
    }

    [Fact]
    public void DefaultDatabasePath_uses_local_app_data()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtsEmployeeStats",
            "ats-employee-stats.db");

        Assert.Equal(expected, CommandLineOptions.DefaultDatabasePath());
    }
}
