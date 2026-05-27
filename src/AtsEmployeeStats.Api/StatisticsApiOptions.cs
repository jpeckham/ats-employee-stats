namespace AtsEmployeeStats.Api;

public sealed class StatisticsApiOptions
{
    public string SaveRoot { get; set; } = string.Empty;
    public string DatabasePath { get; set; } = string.Empty;
    public string? AtsInstallRoot { get; set; }
    public int HistoryDays { get; set; } = 14;
    public bool ReferenceDataEnabled { get; set; } = true;
}
