namespace AtsEmployeeStats.Maui.Presentation;

internal interface IDashboardPresentationTarget
{
    void ShowDashboard(DashboardPresentation presentation);

    void ShowProgress(DashboardProgressPresentation presentation);
}
