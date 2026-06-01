using System.Globalization;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Maui.Presentation;

internal sealed class MauiDashboardPresenter(IDashboardPresentationTarget target)
    : IOutputBoundaryAdapter<DashboardStatisticsDto>
{
    public Task PresentAsync(DashboardStatisticsDto response, CancellationToken cancellationToken)
    {
        var companies = response.Companies
            .Select(company => new CompanyPresentationItem(
                company.Id,
                company.DisplayName,
                FormatMoney(company.Profit),
                $"{company.Drivers.Count} drivers",
                $"{company.Garages.Count} garages / {company.Trucks.Count} trucks"))
            .ToList();

        target.ShowDashboard(new DashboardPresentation(
            companies,
            response.Companies.ToDictionary(company => company.Id, StringComparer.OrdinalIgnoreCase),
            $"{response.Companies.Count} companies",
            FormatMoney(response.Companies.Sum(company => company.Profit)),
            $"{response.Companies.Sum(company => company.Drivers.Count)} drivers",
            response.LastUpdated is null
                ? "Refreshed statistics"
                : $"Refreshed statistics updated {response.LastUpdated.Value.LocalDateTime:g}"));
        return Task.CompletedTask;
    }

    private static string FormatMoney(long value) =>
        string.Create(CultureInfo.CurrentCulture, $"{value:C0}");
}
