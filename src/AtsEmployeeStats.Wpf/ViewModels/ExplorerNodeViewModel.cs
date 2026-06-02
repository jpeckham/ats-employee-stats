using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AtsEmployeeStats.Wpf.ViewModels;

public enum ExplorerNodeKind
{
    Companies,
    Company,
    Garages,
    Garage,
    Drivers,
    Driver,
    Trucks,
    Truck,
    Trailers,
    Trailer,
    Jobs,
    Job,
    Cities,
    City
}

public sealed partial class ExplorerNodeViewModel(
    string name,
    ExplorerNodeKind kind,
    string? companyId = null,
    string? entityId = null) : ObservableObject
{
    public string Name { get; } = name;

    public ExplorerNodeKind Kind { get; } = kind;

    public string? CompanyId { get; } = companyId;

    public string? EntityId { get; } = entityId;

    public ObservableCollection<ExplorerNodeViewModel> Children { get; } = [];
}
