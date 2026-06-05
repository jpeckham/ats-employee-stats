using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using AtsEmployeeStats.Application.Statistics;
using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Domain.Statistics;
using AtsEmployeeStats.Infrastructure.Saves;
using AtsEmployeeStats.Wpf;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace AtsEmployeeStats.Tests;

public sealed class ArchitectureDependencyTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(AtsStatistics).Assembly,
            typeof(StatisticsService).Assembly,
            typeof(CompanyDto).Assembly,
            typeof(SqliteMedallionSaveSnapshotSource).Assembly,
            typeof(App).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInAssembly(typeof(AtsStatistics).Assembly).As("Domain layer");

    private static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInAssembly(typeof(StatisticsService).Assembly).As("Application layer");

    private static readonly IObjectProvider<IType> ContractsLayer =
        Types().That().ResideInAssembly(typeof(CompanyDto).Assembly).As("Contracts layer");

    private static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInAssembly(typeof(SqliteMedallionSaveSnapshotSource).Assembly).As("Infrastructure layer");

    private static readonly IObjectProvider<IType> WpfLayer =
        Types().That().ResideInAssembly(typeof(App).Assembly).As("WPF delivery layer");

    private static readonly IObjectProvider<IType> WpfViewModels =
        Types().That().ResideInNamespace("AtsEmployeeStats.Wpf.ViewModels")
            .And().HaveNameEndingWith("ViewModel").As("WPF view models");

    private static readonly IObjectProvider<IType> WpfViews =
        Types().That().ResideInNamespace("AtsEmployeeStats.Wpf.Controls").As("WPF views");

    private static readonly IObjectProvider<IType> WpfControllers =
        Types().That().ResideInNamespace("AtsEmployeeStats.Wpf.Controllers").As("WPF controllers");

    [Fact]
    public void Domain_has_no_outward_dependencies()
    {
        Check(Types().That().Are(DomainLayer).Should()
            .NotDependOnAny(ApplicationLayer)
            .AndShould().NotDependOnAny(ContractsLayer)
            .AndShould().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(WpfLayer)
            .Because("entities and enterprise rules sit at the center of the architecture"));
    }

    [Fact]
    public void Application_depends_only_on_inner_domain_and_stable_contracts()
    {
        Check(Types().That().Are(ApplicationLayer).Should()
            .NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(WpfLayer)
            .Because("use cases own input and output boundaries and must not know delivery or gateway implementations"));
    }

    [Fact]
    public void Infrastructure_is_outer_ring_and_does_not_depend_on_delivery()
    {
        Check(Types().That().Are(InfrastructureLayer).Should()
            .NotDependOnAny(WpfLayer)
            .Because("infrastructure implements inward-facing gateway abstractions and is not part of delivery"));
    }

    [Fact]
    public void Wpf_view_models_are_output_state_not_controllers_or_gateways()
    {
        Check(Types().That().Are(WpfViewModels).Should()
            .NotDependOnAny(ApplicationLayer)
            .AndShould().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(DomainLayer)
            .Because("commands/controllers call input boundaries, presenters adapt output data, and view models are bound output state"));
    }

    [Fact]
    public void Wpf_views_do_not_depend_on_inner_application_details()
    {
        Check(Types().That().Are(WpfViews).Should()
            .NotDependOnAny(ApplicationLayer)
            .AndShould().NotDependOnAny(DomainLayer)
            .Because("views should bind to presentation state and delegate input through controllers or composition"));
    }

    [Fact]
    public void View_model_types_use_view_model_suffix()
    {
        Check(Classes().That().ResideInNamespace("AtsEmployeeStats.Wpf.ViewModels")
            .And().ArePublic().Should()
            .HaveNameEndingWith("ViewModel")
            .Because("public presentation output models should be recognizable by name"));
    }

    [Fact]
    public void Wpf_controller_types_use_controller_or_presenter_suffix()
    {
        Check(Classes().That().Are(WpfControllers).Should()
            .HaveNameEndingWith("Controller")
            .OrShould().HaveNameEndingWith("Presenter")
            .Because("behavioral WPF adapters should be named by their Clean Architecture role"));
    }

    [Fact]
    public void Use_case_input_boundaries_are_named_as_use_cases()
    {
        Check(Interfaces().That().ResideInAssembly(typeof(StatisticsService).Assembly)
            .And().HaveNameEndingWith("UseCase").Should()
            .HaveNameStartingWith("I")
            .Because("use case input boundaries are application-owned abstractions"));
    }

    [Fact]
    public void Use_case_interactors_use_use_case_suffix()
    {
        Check(Classes().That().ResideInAssembly(typeof(StatisticsService).Assembly)
            .And().HaveNameEndingWith("UseCase").Should()
            .ResideInAssembly(typeof(StatisticsService).Assembly)
            .Because("use case interactors belong to the application ring"));
    }

    private static void Check(IArchRule rule) =>
        ArchRuleAssert.CheckRule(Architecture, rule);
}
