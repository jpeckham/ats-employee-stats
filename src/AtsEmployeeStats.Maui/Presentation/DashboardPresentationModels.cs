using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Maui.Presentation;

internal sealed record DashboardPresentation(
    IReadOnlyList<CompanyPresentationItem> Companies,
    IReadOnlyDictionary<string, CompanyDto> CompanyDetails,
    string CompanyCountText,
    string TotalProfitText,
    string DriverCountText,
    string RefreshedStatusText);

internal sealed record CompanyPresentationItem(
    string Id,
    string DisplayName,
    string ProfitText,
    string DriverCountText,
    string AssetsText);

internal sealed record DashboardProgressPresentation(
    string StatusText,
    double OverallProgress,
    double CurrentFileProgress,
    string OverallProgressText,
    string CurrentFileProgressText);
