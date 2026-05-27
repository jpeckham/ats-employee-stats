namespace AtsEmployeeStats.Web.Services;

public sealed class DashboardNavigationState
{
    private readonly List<string> _breadcrumb = [];

    public string? CompanyId { get; private set; }
    public string? GarageId { get; private set; }
    public string? DriverId { get; private set; }
    public IReadOnlyList<string> Breadcrumb => _breadcrumb;
    public bool CanGoBack => _breadcrumb.Count > 0;

    public void SelectCompany(string companyId, string displayName)
    {
        CompanyId = companyId;
        GarageId = null;
        DriverId = null;
        _breadcrumb.Clear();
        _breadcrumb.Add(displayName);
    }

    public void SelectGarage(string garageId, string displayName)
    {
        GarageId = garageId;
        DriverId = null;
        ReplaceBreadcrumbFrom(index: 1, displayName);
    }

    public void SelectDriver(string driverId, string displayName)
    {
        DriverId = driverId;
        ReplaceBreadcrumbFrom(index: 2, displayName);
    }

    public void Back()
    {
        if (DriverId is not null)
        {
            DriverId = null;
            RemoveBreadcrumbFrom(index: 2);
            return;
        }

        if (GarageId is not null)
        {
            GarageId = null;
            RemoveBreadcrumbFrom(index: 1);
            return;
        }

        if (CompanyId is not null)
        {
            CompanyId = null;
            _breadcrumb.Clear();
        }
    }

    private void ReplaceBreadcrumbFrom(int index, string displayName)
    {
        RemoveBreadcrumbFrom(index);
        _breadcrumb.Add(displayName);
    }

    private void RemoveBreadcrumbFrom(int index)
    {
        while (_breadcrumb.Count > index)
        {
            _breadcrumb.RemoveAt(_breadcrumb.Count - 1);
        }
    }
}
