namespace AtsEmployeeStats.Tests;

public sealed class WpfPresentationMigrationTests
{
    [Fact]
    public void Presentation_project_is_wpf_not_maui()
    {
        var root = FindRepositoryRoot();
        var sourceRoot = Path.Combine(root, "src");
        var solution = File.ReadAllText(Path.Combine(root, "AtsEmployeeStats.sln"));

        Assert.False(Directory.Exists(Path.Combine(sourceRoot, "AtsEmployeeStats.Maui")), "MAUI presentation project should be removed.");
        AssertFile(sourceRoot, "AtsEmployeeStats.Wpf", "AtsEmployeeStats.Wpf.csproj");
        Assert.Contains("AtsEmployeeStats.Wpf", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("AtsEmployeeStats.Maui", solution, StringComparison.Ordinal);

        var project = File.ReadAllText(Path.Combine(sourceRoot, "AtsEmployeeStats.Wpf", "AtsEmployeeStats.Wpf.csproj"));
        Assert.Contains("<UseWPF>true</UseWPF>", project, StringComparison.Ordinal);
        Assert.Contains("net10.0-windows", project, StringComparison.Ordinal);
        Assert.Contains("CommunityToolkit.Mvvm", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Maui", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_uses_desktop_explorer_mvvm_patterns()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var mainWindow = File.ReadAllText(Path.Combine(wpfRoot, "MainWindow.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(wpfRoot, "MainWindow.xaml.cs"));
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("TreeView", mainWindow, StringComparison.Ordinal);
        Assert.Contains("DataGrid", mainWindow, StringComparison.Ordinal);
        Assert.Contains("TabControl", mainWindow, StringComparison.Ordinal);
        Assert.Contains("DockPanel", mainWindow, StringComparison.Ordinal);
        Assert.Contains("SparklineControl", codeBehind, StringComparison.Ordinal);
        Assert.Contains("MouseDoubleClick", mainWindow, StringComparison.Ordinal);

        Assert.Contains("partial class MainWindowViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("CompanyExplorerViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("CompanyDetailViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("GarageDetailViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("DriverDetailViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("TruckDetailViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("TrailerDetailViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("JobDetailViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("CityDetailViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("ObservableObject", viewModels, StringComparison.Ordinal);
        Assert.Contains("RelayCommand", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_detail_tables_have_actionable_rows_and_web_city_columns()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var mainWindow = File.ReadAllText(Path.Combine(wpfRoot, "MainWindow.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(wpfRoot, "MainWindow.xaml.cs"));
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("DetailGrid_DataContextChanged", mainWindow, StringComparison.Ordinal);
        Assert.Contains("DetailGrid_Loaded", mainWindow, StringComparison.Ordinal);
        Assert.Contains("DetailGrid_MouseDoubleClick", mainWindow, StringComparison.Ordinal);
        Assert.Contains("DataGridTemplateColumn", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ConfigureDetailGridColumns", codeBehind, StringComparison.Ordinal);
        Assert.Contains("OpenRowCommand", codeBehind, StringComparison.Ordinal);
        Assert.Contains("OpenRow(GridRowViewModel? row)", viewModels, StringComparison.Ordinal);
        Assert.Contains("RowNavigationTarget", viewModels, StringComparison.Ordinal);

        Assert.Contains("\"Garage\"", viewModels, StringComparison.Ordinal);
        Assert.Contains("\"Eligible\"", viewModels, StringComparison.Ordinal);
        Assert.Contains("\"Visits\"", viewModels, StringComparison.Ordinal);
        Assert.Contains("\"Outbound\"", viewModels, StringComparison.Ordinal);
        Assert.Contains("\"Inbound\"", viewModels, StringComparison.Ordinal);
        Assert.Contains("\"Total\"", viewModels, StringComparison.Ordinal);
        Assert.Contains("\"Expansion\"", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_explorer_collection_nodes_select_matching_company_detail_tabs()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var mainWindow = File.ReadAllText(Path.Combine(wpfRoot, "MainWindow.xaml"));
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("SelectedTabIndex", viewModels, StringComparison.Ordinal);
        Assert.Contains("SelectedIndex=\"{Binding SelectedDetail.SelectedTabIndex, Mode=OneWay}\"", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedItem=\"{Binding SelectedDetail.SelectedTab", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Garages => new CompanyDetailViewModel(company, \"Garages\")", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Drivers => new CompanyDetailViewModel(company, \"Drivers\")", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Trucks => new CompanyDetailViewModel(company, \"Trucks\")", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Trailers => new CompanyDetailViewModel(company, \"Trailers\")", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Jobs => new CompanyDetailViewModel(company, \"Jobs\")", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Cities => new CompanyDetailViewModel(company, \"Cities\")", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_has_reusable_overview_controls_and_livecharts()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var project = File.ReadAllText(Path.Combine(wpfRoot, "AtsEmployeeStats.Wpf.csproj"));
        var mainWindow = File.ReadAllText(Path.Combine(wpfRoot, "MainWindow.xaml"));

        Assert.Contains("LiveChartsCore.SkiaSharpView.WPF", project, StringComparison.Ordinal);
        AssertFile(wpfRoot, "Controls", "OverviewHeaderControl.xaml");
        AssertFile(wpfRoot, "Controls", "SummaryCardsControl.xaml");
        AssertFile(wpfRoot, "Controls", "TrendChartControl.xaml");
        AssertFile(wpfRoot, "Controls", "TopListControl.xaml");
        AssertFile(wpfRoot, "Controls", "RecentActivityControl.xaml");
        Assert.Contains("OverviewPageControl", mainWindow, StringComparison.Ordinal);
        Assert.Contains("IsOverview", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_every_entity_detail_starts_with_non_empty_overview_tab()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("OverviewViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("SummaryCardViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("TrendChartViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("TopListViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("RecentActivityViewModel", viewModels, StringComparison.Ordinal);

        Assert.Contains("CompanyOverviewBuilder.Build(company)", viewModels, StringComparison.Ordinal);
        Assert.Contains("GarageOverviewBuilder.Build(company, garage)", viewModels, StringComparison.Ordinal);
        Assert.Contains("DriverOverviewBuilder.Build(company, driver)", viewModels, StringComparison.Ordinal);
        Assert.Contains("TruckOverviewBuilder.Build(company, truck)", viewModels, StringComparison.Ordinal);
        Assert.Contains("TrailerOverviewBuilder.Build(company, trailer)", viewModels, StringComparison.Ordinal);
        Assert.Contains("CityOverviewBuilder.Build(company, city)", viewModels, StringComparison.Ordinal);
        Assert.Contains("JobOverviewBuilder.Build(company, job)", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_money_and_count_columns_sort_by_numeric_values_not_display_text()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("long ProfitSort", viewModels, StringComparison.Ordinal);
        Assert.Contains("long OutboundSort", viewModels, StringComparison.Ordinal);
        Assert.Contains("long InboundSort", viewModels, StringComparison.Ordinal);
        Assert.Contains("long TotalSort", viewModels, StringComparison.Ordinal);
        Assert.Contains("int VisitsSort", viewModels, StringComparison.Ordinal);
        Assert.Contains("decimal ExpansionSort", viewModels, StringComparison.Ordinal);

        Assert.Contains("new(\"Profit\", nameof(GridRowViewModel.Profit), nameof(GridRowViewModel.ProfitSort))", viewModels, StringComparison.Ordinal);
        Assert.Contains("new(\"Visits\", nameof(GridRowViewModel.Visits), nameof(GridRowViewModel.VisitsSort))", viewModels, StringComparison.Ordinal);
        Assert.Contains("new(\"Outbound\", nameof(GridRowViewModel.Outbound), nameof(GridRowViewModel.OutboundSort))", viewModels, StringComparison.Ordinal);
        Assert.Contains("new(\"Inbound\", nameof(GridRowViewModel.Inbound), nameof(GridRowViewModel.InboundSort))", viewModels, StringComparison.Ordinal);
        Assert.Contains("new(\"Total\", nameof(GridRowViewModel.Total), nameof(GridRowViewModel.TotalSort))", viewModels, StringComparison.Ordinal);
        Assert.Contains("new(\"Expansion\", nameof(GridRowViewModel.Expansion), nameof(GridRowViewModel.ExpansionSort))", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_preserves_clean_architecture_boundaries()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var project = File.ReadAllText(Path.Combine(wpfRoot, "AtsEmployeeStats.Wpf.csproj"));
        var views = ReadAllSource(wpfRoot, "Views");
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("AtsEmployeeStats.Application.csproj", project, StringComparison.Ordinal);
        Assert.Contains("AtsEmployeeStats.Contracts.csproj", project, StringComparison.Ordinal);
        Assert.Contains("AtsEmployeeStats.Infrastructure.csproj", project, StringComparison.Ordinal);

        Assert.DoesNotContain("AtsEmployeeStats.Domain", views, StringComparison.Ordinal);
        Assert.DoesNotContain("AtsEmployeeStats.Infrastructure", views, StringComparison.Ordinal);
        Assert.DoesNotContain("AtsEmployeeStats.Domain", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_notes_describe_architectural_change()
    {
        var root = FindRepositoryRoot();
        var notes = File.ReadAllText(Path.Combine(root, "docs", "migration", "maui-to-wpf.md"));

        Assert.Contains("MAUI", notes, StringComparison.Ordinal);
        Assert.Contains("WPF", notes, StringComparison.Ordinal);
        Assert.Contains("composition root", notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MVVM", notes, StringComparison.Ordinal);
        Assert.Contains("Clean Architecture", notes, StringComparison.Ordinal);
    }

    private static string ReadAllSource(string root, string folder)
    {
        var path = Path.Combine(root, folder);
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
    }

    private static void AssertFile(string root, params string[] segments)
    {
        var path = Path.Combine([root, .. segments]);
        Assert.True(File.Exists(path), $"Expected file '{path}' to exist.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AtsEmployeeStats.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
