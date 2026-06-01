namespace AtsEmployeeStats.Tests;

public sealed class MauiScreenParityTests
{
    [Fact]
    public void Maui_declares_screen_equivalents_for_web_pages()
    {
        var root = FindRepositoryRoot();
        var mauiRoot = Path.Combine(root, "src", "AtsEmployeeStats.Maui");

        AssertFile(mauiRoot, "MainPage.xaml");
        AssertFile(mauiRoot, "Views", "CompanyDetailPage.xaml");
        AssertFile(mauiRoot, "Views", "GarageDetailPage.xaml");
        AssertFile(mauiRoot, "Views", "DriverDetailPage.xaml");
        AssertFile(mauiRoot, "Views", "TruckDetailPage.xaml");
        AssertFile(mauiRoot, "Views", "TrailerDetailPage.xaml");
        AssertFile(mauiRoot, "Views", "JobDetailPage.xaml");
        AssertFile(mauiRoot, "Views", "CityDetailPage.xaml");

        AssertFile(mauiRoot, "Controllers", "DashboardController.cs");
        AssertFile(mauiRoot, "Controllers", "DetailNavigationController.cs");
        AssertFile(mauiRoot, "Presentation", "MauiDetailPresenters.cs");
        AssertFile(mauiRoot, "Presentation", "DetailPresentationModels.cs");
    }

    [Fact]
    public void Maui_uses_local_use_cases_without_http_api_client_dependencies()
    {
        var root = FindRepositoryRoot();
        var mauiRoot = Path.Combine(root, "src", "AtsEmployeeStats.Maui");
        var source = Directory
            .EnumerateFiles(mauiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText);

        var combined = string.Join(Environment.NewLine, source);

        Assert.DoesNotContain("StatisticsClient", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", combined, StringComparison.Ordinal);
        Assert.Contains("IStatisticsDashboardUseCases", combined, StringComparison.Ordinal);
    }

    [Fact]
    public void Maui_detail_rows_expose_routed_drilldown_actions()
    {
        var root = FindRepositoryRoot();
        var mauiRoot = Path.Combine(root, "src", "AtsEmployeeStats.Maui");
        var presentationModels = File.ReadAllText(Path.Combine(mauiRoot, "Presentation", "DetailPresentationModels.cs"));
        var presenters = File.ReadAllText(Path.Combine(mauiRoot, "Presentation", "MauiDetailPresenters.cs"));
        var layout = File.ReadAllText(Path.Combine(mauiRoot, "Views", "DetailPageLayout.cs"));
        var controller = File.ReadAllText(Path.Combine(mauiRoot, "Controllers", "DetailNavigationController.cs"));

        Assert.Contains("ActionRoute", presentationModels, StringComparison.Ordinal);
        Assert.Contains("ActionText", presentationModels, StringComparison.Ordinal);
        Assert.Contains("Button", layout, StringComparison.Ordinal);
        Assert.Contains("GoToRouteAsync", controller, StringComparison.Ordinal);

        Assert.Contains("RouteToGarage", presenters, StringComparison.Ordinal);
        Assert.Contains("RouteToDriver", presenters, StringComparison.Ordinal);
        Assert.Contains("RouteToTruck", presenters, StringComparison.Ordinal);
        Assert.Contains("RouteToTrailer", presenters, StringComparison.Ordinal);
        Assert.Contains("RouteToJob", presenters, StringComparison.Ordinal);
        Assert.Contains("RouteToCity", presenters, StringComparison.Ordinal);
    }

    [Fact]
    public void Maui_shell_home_page_is_service_provider_backed()
    {
        var root = FindRepositoryRoot();
        var mauiRoot = Path.Combine(root, "src", "AtsEmployeeStats.Maui");
        var shellXaml = File.ReadAllText(Path.Combine(mauiRoot, "AppShell.xaml"));
        var shellCode = File.ReadAllText(Path.Combine(mauiRoot, "AppShell.xaml.cs"));

        Assert.DoesNotContain("ContentTemplate=\"{DataTemplate local:MainPage}\"", shellXaml, StringComparison.Ordinal);
        Assert.Contains("MainShellContent.Content", shellCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Maui_dashboard_refresh_uses_controller_and_presenters()
    {
        var root = FindRepositoryRoot();
        var mauiRoot = Path.Combine(root, "src", "AtsEmployeeStats.Maui");
        var mainPageCode = File.ReadAllText(Path.Combine(mauiRoot, "MainPage.xaml.cs"));
        var controllerCode = File.ReadAllText(Path.Combine(mauiRoot, "Controllers", "DashboardController.cs"));

        Assert.Contains("DashboardController", mainPageCode, StringComparison.Ordinal);
        Assert.Contains("_dashboardController.RefreshAsync", mainPageCode, StringComparison.Ordinal);
        Assert.Contains("MauiDashboardPresenter", controllerCode, StringComparison.Ordinal);
        Assert.Contains("MauiProgressPresenter", controllerCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Maui_detail_layout_is_theme_aware_and_has_empty_state()
    {
        var root = FindRepositoryRoot();
        var mauiRoot = Path.Combine(root, "src", "AtsEmployeeStats.Maui");
        var model = File.ReadAllText(Path.Combine(mauiRoot, "Presentation", "DetailPresentationModels.cs"));
        var layout = File.ReadAllText(Path.Combine(mauiRoot, "Views", "DetailPageLayout.cs"));

        Assert.Contains("HasNoContent", model, StringComparison.Ordinal);
        Assert.Contains("SetAppThemeColor", layout, StringComparison.Ordinal);
        Assert.Contains("MaximumWidthRequest", layout, StringComparison.Ordinal);
        Assert.Contains("EmptyState", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("BackgroundColor = Colors.White", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void Maui_dashboard_uses_desktop_explorer_master_detail_patterns()
    {
        var root = FindRepositoryRoot();
        var mauiRoot = Path.Combine(root, "src", "AtsEmployeeStats.Maui");
        var mainPageCode = File.ReadAllText(Path.Combine(mauiRoot, "MainPage.xaml.cs"));
        var mainPageXaml = File.ReadAllText(Path.Combine(mauiRoot, "MainPage.xaml"));

        Assert.Contains("ExplorerNavigationItem", mainPageCode, StringComparison.Ordinal);
        Assert.Contains("DetailTabItem", mainPageCode, StringComparison.Ordinal);
        Assert.Contains("SortableExplorerRowItem", mainPageCode, StringComparison.Ordinal);
        Assert.Contains("SortActiveRowsCommand", mainPageCode, StringComparison.Ordinal);
        Assert.Contains("SparklineText", mainPageCode, StringComparison.Ordinal);

        Assert.Contains("ExplorerNavigationItems", mainPageXaml, StringComparison.Ordinal);
        Assert.Contains("DetailTabs", mainPageXaml, StringComparison.Ordinal);
        Assert.Contains("ActiveDetailRows", mainPageXaml, StringComparison.Ordinal);
        Assert.Contains("SortActiveRowsCommand", mainPageXaml, StringComparison.Ordinal);
        Assert.Contains("SparklineText", mainPageXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"View\"", mainPageXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Open\"", mainPageXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Maui_detail_pages_use_tabbed_sortable_related_sections()
    {
        var root = FindRepositoryRoot();
        var mauiRoot = Path.Combine(root, "src", "AtsEmployeeStats.Maui");
        var model = File.ReadAllText(Path.Combine(mauiRoot, "Presentation", "DetailPresentationModels.cs"));
        var layout = File.ReadAllText(Path.Combine(mauiRoot, "Views", "DetailPageLayout.cs"));

        Assert.Contains("DetailSectionTabItem", model, StringComparison.Ordinal);
        Assert.Contains("ActiveSectionRows", model, StringComparison.Ordinal);
        Assert.Contains("SelectSectionCommand", model, StringComparison.Ordinal);
        Assert.Contains("SortSectionRowsCommand", model, StringComparison.Ordinal);

        Assert.Contains("SectionTabs", layout, StringComparison.Ordinal);
        Assert.Contains("ActiveSectionRows", layout, StringComparison.Ordinal);
        Assert.Contains("SortSectionRowsCommand", layout, StringComparison.Ordinal);
    }

    private static void AssertFile(string root, params string[] segments)
    {
        var path = Path.Combine([root, .. segments]);
        Assert.True(File.Exists(path), $"Expected Maui screen parity file '{path}' to exist.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AtsEmployeeStats.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
