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

        Assert.Contains("partial class MainWindowPresenter", viewModels, StringComparison.Ordinal);
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
    public void Wpf_games_explorer_filters_companies_by_game_source_prefix()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("SourcePrefix", viewModels, StringComparison.Ordinal);
        Assert.Contains("company.Id.StartsWith(gameSource.SourcePrefix", viewModels, StringComparison.Ordinal);
        Assert.Contains("unpartitionedCompanies", viewModels, StringComparison.Ordinal);
        Assert.Contains("if (unpartitionedCompanies.Count > 0)", viewModels, StringComparison.Ordinal);
        Assert.Contains("foreach (var company in unpartitionedCompanies.OrderBy", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_games_explorer_groups_by_save_location_and_company_name_not_save_file()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");
        var app = File.ReadAllText(Path.Combine(wpfRoot, "App.xaml.cs"));

        Assert.Contains("GameSaveCatalogUseCase", app, StringComparison.Ordinal);
        Assert.Contains("GameSaveRowViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("GameSaves", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.SaveLocation", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.SaveLocationCompany", viewModels, StringComparison.Ordinal);
        Assert.Contains("GroupBy(save => save.SaveRootPath", viewModels, StringComparison.Ordinal);
        Assert.Contains("GroupBy(company => company.DisplayName", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildSaveNode(save)", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_configured_game_sources_enable_reference_data_for_fresh_rebuilds()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var app = File.ReadAllText(Path.Combine(wpfRoot, "App.xaml.cs"));

        Assert.Contains("new AtsReferenceDataOptions", app, StringComparison.Ordinal);
        Assert.Contains("Enabled: true", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Enabled: false", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_explorer_expands_startup_path_to_visible_save_location_companies()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var mainWindow = File.ReadAllText(Path.Combine(wpfRoot, "MainWindow.xaml"));
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("IsExpanded", viewModels, StringComparison.Ordinal);
        Assert.Contains("Property=\"IsExpanded\"", mainWindow, StringComparison.Ordinal);
        Assert.Contains("IsExpanded = true", viewModels, StringComparison.Ordinal);
        Assert.Contains("new ExplorerNodeViewModel(gameSource.GameName", viewModels, StringComparison.Ordinal);
        Assert.Contains("new ExplorerNodeViewModel(\"Save Locations\"", viewModels, StringComparison.Ordinal);
        Assert.Contains("new ExplorerNodeViewModel(\"Companies\", ExplorerNodeKind.Companies, entityId: saveLocation.Key)", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("companyNode.IsExpanded = true", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExpandExplorerToNode", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExpandAncestorPath", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_save_location_company_nodes_have_detail_collection_children()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("BuildSaveLocationCompanyNode", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.SaveLocationCompany => new CompanyDetailViewModel", viewModels, StringComparison.Ordinal);
        Assert.Contains("AddCollection(companyNode, \"Garages\"", viewModels, StringComparison.Ordinal);
        Assert.Contains("AddCollection(companyNode, \"Drivers\"", viewModels, StringComparison.Ordinal);
        Assert.Contains("AddCollection(companyNode, \"Trucks\"", viewModels, StringComparison.Ordinal);
        Assert.Contains("AddCollection(companyNode, \"Trailers\"", viewModels, StringComparison.Ordinal);
        Assert.Contains("AddCollection(companyNode, \"Jobs\"", viewModels, StringComparison.Ordinal);
        Assert.Contains("AddCollection(companyNode, \"Cities\"", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_company_navigation_expands_explorer_to_matching_company_context()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("ExpandExplorerToNode(target)", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExpandExplorerToNode(node)", viewModels, StringComparison.Ordinal);
        Assert.Contains("matching.Node.IsExpanded = true", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Company or ExplorerNodeKind.SaveLocationCompany", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Garages", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Garage", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Drivers", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Driver", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Trucks", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Truck", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Trailers", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Trailer", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Jobs", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Job", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.Cities", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.City", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_save_location_company_selection_aggregates_company_context()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("ExplorerNodeKind.SaveLocation", viewModels, StringComparison.Ordinal);
        Assert.Contains("ExplorerNodeKind.SaveLocationCompany", viewModels, StringComparison.Ordinal);
        Assert.Contains("GetCompaniesForSaveLocation", viewModels, StringComparison.Ordinal);
        Assert.Contains("Save location selected:", viewModels, StringComparison.Ordinal);
        Assert.Contains("Company selected:", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_save_location_companies_node_shows_aggregated_company_list()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("node.Kind == ExplorerNodeKind.Companies && !string.IsNullOrWhiteSpace(node.EntityId)", viewModels, StringComparison.Ordinal);
        Assert.Contains("Companies selected:", viewModels, StringComparison.Ordinal);
        Assert.Contains("GetCompaniesForSaveLocation(node.EntityId, companies, saveRows)", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_reload_runs_with_progress_ui()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var mainWindow = File.ReadAllText(Path.Combine(wpfRoot, "MainWindow.xaml"));
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("IsLoadProgressVisible", mainWindow, StringComparison.Ordinal);
        Assert.Contains("SaveFileProgressValue", mainWindow, StringComparison.Ordinal);
        Assert.Contains("SaveContentProgressValue", mainWindow, StringComparison.Ordinal);
        Assert.Contains("new Progress<SaveLoadProgress>", viewModels, StringComparison.Ordinal);
        Assert.Contains("backgroundRunner.RunAsync(() => reloadUseCase.ReloadAsync", viewModels, StringComparison.Ordinal);
        Assert.Contains("ApplyLoadProgress", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_progress_values_bind_one_way_to_read_only_presenter_state()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var mainWindow = File.ReadAllText(Path.Combine(wpfRoot, "MainWindow.xaml"));

        Assert.Contains("Value=\"{Binding SaveFileProgressValue, Mode=OneWay}\"", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding SaveContentProgressValue, Mode=OneWay}\"", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_startup_and_source_discovery_do_not_run_blocking_work_on_dispatcher()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");
        var app = File.ReadAllText(Path.Combine(wpfRoot, "App.xaml.cs"));

        Assert.Contains("await Task.Yield()", viewModels, StringComparison.Ordinal);
        Assert.Contains("IBackgroundRunner", viewModels, StringComparison.Ordinal);
        Assert.Contains("TaskBackgroundRunner", app, StringComparison.Ordinal);
        Assert.Contains("backgroundRunner.RunAsync(() => gameSourceManagement.RequiresWizardAsync", viewModels, StringComparison.Ordinal);
        Assert.Contains("backgroundRunner.RunAsync(() => gameSourceManagement.DiscoverAsync", viewModels, StringComparison.Ordinal);
        Assert.Contains("backgroundRunner.RunAsync(() => gameSaveCatalog.FindSaveGamesAsync", viewModels, StringComparison.Ordinal);
        Assert.Contains("backgroundRunner.RunAsync(() => dashboardUseCases.GetDashboardAsync", viewModels, StringComparison.Ordinal);
        Assert.Contains("backgroundRunner.RunAsync(async ()", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run(", viewModels, StringComparison.Ordinal);
        Assert.Contains("DiscoverCandidatesAsync(game", viewModels, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_uses_first_run_game_source_wizard_instead_of_raw_source_textboxes()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var mainWindow = File.ReadAllText(Path.Combine(wpfRoot, "MainWindow.xaml"));
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");
        var app = File.ReadAllText(Path.Combine(wpfRoot, "App.xaml.cs"));

        Assert.Contains("SqliteGameSourceSettingsStore.CreateDefault()", app, StringComparison.Ordinal);
        Assert.Contains("IsSourceWizardVisible", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Do you have", mainWindow, StringComparison.Ordinal);
        Assert.Contains("InstallCandidates", mainWindow, StringComparison.Ordinal);
        Assert.Contains("SaveRootCandidates", mainWindow, StringComparison.Ordinal);
        Assert.Contains("FinishSourceWizardCommand", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Manage Sources", mainWindow, StringComparison.Ordinal);
        Assert.Contains("RequiresWizardAsync", viewModels, StringComparison.Ordinal);
        Assert.Contains("SaveValidatedAsync", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Save Sources\"", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolTip=\"Install path\"", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolTip=\"Profile path\"", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolTip=\"Save path\"", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_has_clear_empty_state_and_disables_unavailable_actions()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var mainWindow = File.ReadAllText(Path.Combine(wpfRoot, "MainWindow.xaml"));
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("No save sources configured", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Set Up Sources", mainWindow, StringComparison.Ordinal);
        Assert.Contains("IsEmptyStateVisible", mainWindow, StringComparison.Ordinal);
        Assert.Contains("CanReloadSaves", mainWindow, StringComparison.Ordinal);
        Assert.Contains("CanRefreshDashboard", mainWindow, StringComparison.Ordinal);
        Assert.Contains("IsExplorerVisible", mainWindow, StringComparison.Ordinal);
        Assert.Contains("public bool CanReloadSaves", viewModels, StringComparison.Ordinal);
        Assert.Contains("public bool CanRefreshDashboard", viewModels, StringComparison.Ordinal);
        Assert.Contains("private bool CanReloadSavesCommand()", viewModels, StringComparison.Ordinal);
        Assert.Contains("private bool CanRefreshDashboardCommand()", viewModels, StringComparison.Ordinal);
        Assert.Contains("RefreshCommand.NotifyCanExecuteChanged()", viewModels, StringComparison.Ordinal);
        Assert.Contains("ReloadCommand.NotifyCanExecuteChanged()", viewModels, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"#111827\"", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"Black\"", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void Wpf_source_wizard_read_only_text_bindings_are_one_way()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var mainWindow = File.ReadAllText(Path.Combine(wpfRoot, "MainWindow.xaml"));

        Assert.Contains("Text=\"{Binding FullGameName, Mode=OneWay}\"", mainWindow, StringComparison.Ordinal);
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
    public void Wpf_overview_charts_plot_actual_points_without_filtering_or_smoothing_final_values()
    {
        var root = FindRepositoryRoot();
        var wpfRoot = Path.Combine(root, "src", "AtsEmployeeStats.Wpf");
        var viewModels = ReadAllSource(wpfRoot, "ViewModels");

        Assert.Contains("LineSmoothness = 0", viewModels, StringComparison.Ordinal);
        Assert.DoesNotContain("points.Where(point => point.Value != 0)", viewModels, StringComparison.Ordinal);
        Assert.Contains("var materialized = points.ToArray()", viewModels, StringComparison.Ordinal);
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
    public void Wpf_numeric_secondary_columns_sort_by_numeric_values_not_display_text()
    {
        Assert.Equal(
            nameof(Wpf.ViewModels.GridRowViewModel.SecondarySort),
            Wpf.ViewModels.TableColumns.Garages.Single(column => column.Header == "Avg/Day").SortMemberPath);
        Assert.Equal(
            nameof(Wpf.ViewModels.GridRowViewModel.SecondarySort),
            Wpf.ViewModels.TableColumns.Drivers.Single(column => column.Header == "Jobs").SortMemberPath);
        Assert.Equal(
            nameof(Wpf.ViewModels.GridRowViewModel.SecondarySort),
            Wpf.ViewModels.TableColumns.Trailers.Single(column => column.Header == "Jobs").SortMemberPath);
        Assert.Equal(
            nameof(Wpf.ViewModels.GridRowViewModel.SecondarySort),
            Wpf.ViewModels.TableColumns.Jobs.Single(column => column.Header == "Day").SortMemberPath);
        Assert.Equal(
            nameof(Wpf.ViewModels.GridRowViewModel.DetailSort),
            Wpf.ViewModels.TableColumns.Routes.Single(column => column.Header == "Jobs").SortMemberPath);
        Assert.Equal(
            nameof(Wpf.ViewModels.GridRowViewModel.SecondarySort),
            Wpf.ViewModels.TableColumns.Routes.Single(column => column.Header == "Profit/Mile").SortMemberPath);

        var company = new AtsEmployeeStats.Contracts.CompanyDto(
            "company",
            "Company",
            0,
            Garages:
            [
                new("low", "Low", 100, 2, 1, 1),
                new("high", "High", 100, 10, 1, 1)
            ],
            Drivers:
            [
                new("few", "Few", 100, 1, null, null, 2),
                new("many", "Many", 100, 1, null, null, 10)
            ],
            Trucks: [],
            Missions:
            [
                new("early", null, null, null, "Early", null, null, 100, 2),
                new("late", null, null, null, "Late", null, null, 100, 10)
            ],
            TrailerTypes: [],
            Trailers:
            [
                new("few", "Dry Van", 100, 2),
                new("many", "Dry Van", 100, 10)
            ],
            Routes:
            [
                new("a", "b", 100, 2, 1.25m, 0),
                new("c", "d", 100, 10, 9.75m, 0)
            ]);

        Assert.Equal([2m, 10m], company.Garages.Select(garage => Wpf.ViewModels.Rows.Garage(company, garage).SecondarySort));
        Assert.Equal([2m, 10m], company.Drivers.Select(driver => Wpf.ViewModels.Rows.Driver(company, driver).SecondarySort));
        Assert.Equal([2m, 10m], company.Missions.Select(job => Wpf.ViewModels.Rows.Job(company, job).SecondarySort));
        Assert.Equal([2m, 10m], company.Trailers!.Select(trailer => Wpf.ViewModels.Rows.Trailer(company, trailer).SecondarySort));
    }

    [Fact]
    public void Wpf_route_rows_show_profit_per_mile_with_currency_in_value()
    {
        Assert.Contains(Wpf.ViewModels.TableColumns.Routes, column => column.Header == "Profit/Mile");
        Assert.DoesNotContain(Wpf.ViewModels.TableColumns.Routes, column => column.Header == "$/Mile");

        var company = new AtsEmployeeStats.Contracts.CompanyDto(
            "company",
            "Company",
            0,
            Garages: [],
            Drivers: [],
            Trucks: [],
            Missions: [],
            TrailerTypes: [],
            Cities:
            [
                new("phoenix", "Phoenix", true, true, 1, 5000, 0, 5000, 0)
            ],
            Routes:
            [
                new("phoenix", "denver", 5000, 2, 10m, 0)
            ],
            CurrencySymbol: "$");

        var row = new Wpf.ViewModels.CityDetailViewModel(company, company.Cities!.Single())
            .Tabs.Single(tab => tab.Title == "Routes")
            .Rows.Single();

        Assert.Equal("$10.00/mi", row.Secondary);
        Assert.Equal(10m, row.SecondarySort);
    }

    [Fact]
    public void Wpf_route_columns_use_game_specific_distance_unit()
    {
        var atsCompany = CompanyWithRoute("ats-company", "$");
        var etsCompany = CompanyWithRoute("ets2-company", "€");

        var atsRoutesTab = new Wpf.ViewModels.CityDetailViewModel(atsCompany, atsCompany.Cities!.Single())
            .Tabs.Single(tab => tab.Title == "Routes");
        var etsRoutesTab = new Wpf.ViewModels.CityDetailViewModel(etsCompany, etsCompany.Cities!.Single())
            .Tabs.Single(tab => tab.Title == "Routes");

        Assert.Contains(atsRoutesTab.Columns, column => column.Header == "Profit/Mile");
        Assert.Contains(etsRoutesTab.Columns, column => column.Header == "Profit/Kilometer");
        Assert.Equal("€10.00/km", etsRoutesTab.Rows.Single().Secondary);
    }

    [Fact]
    public void Wpf_trailer_rows_do_not_show_nameless_type_identifiers()
    {
        Assert.Contains(Wpf.ViewModels.TableColumns.Trailers, column => column.Header == "Body" && column.BindingPath == nameof(Wpf.ViewModels.GridRowViewModel.Body));
        Assert.Contains(Wpf.ViewModels.TableColumns.Trailers, column => column.Header == "Location" && column.BindingPath == nameof(Wpf.ViewModels.GridRowViewModel.Location));
        Assert.DoesNotContain(Wpf.ViewModels.TableColumns.Trailers, column => column.Header == "Body / Location");

        var company = new AtsEmployeeStats.Contracts.CompanyDto(
            "company",
            "Company",
            0,
            Garages:
            [
                new("garage.phoenix", "Phoenix", 0, 0, 0, 0)
            ],
            Drivers: [],
            Trucks: [],
            Missions: [],
            TrailerTypes: [],
            Trailers:
            [
                new("trailer.1", "_nameless.26b.dd5f.13a0", 100, 1, BodyType: "dry_van", GarageId: "garage.phoenix")
            ]);

        var row = Wpf.ViewModels.Rows.Trailer(company, company.Trailers!.Single());
        var detail = new Wpf.ViewModels.TrailerDetailViewModel(company, company.Trailers!.Single());

        Assert.Equal("Dry Van", row.Body);
        Assert.Equal("Phoenix", row.Location);
        Assert.DoesNotContain("_nameless", row.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("_nameless", detail.Subtitle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("_nameless", detail.Tabs.Single(tab => tab.Title == "Overview").Overview!.Header.Subtitle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Wpf_driver_rows_show_profit_per_distance()
    {
        Assert.Equal(
            nameof(Wpf.ViewModels.GridRowViewModel.ProfitPerDistanceSort),
            Wpf.ViewModels.TableColumns.Drivers.Single(column => column.Header == "Profit/Dist").SortMemberPath);

        var atsCompany = CompanyWithDriverDistance("ats-company", "$", 1000, 200);
        var atsRow = Wpf.ViewModels.Rows.Driver(atsCompany, atsCompany.Drivers.Single());

        Assert.Equal("$5.00/mi", atsRow.ProfitPerDistance);
        Assert.Equal(5m, atsRow.ProfitPerDistanceSort);

        var etsCompany = CompanyWithDriverDistance("ets2-company", "€", 1000, 200);
        var etsRow = Wpf.ViewModels.Rows.Driver(etsCompany, etsCompany.Drivers.Single());

        Assert.Equal("€5.00/km", etsRow.ProfitPerDistance);
        Assert.Equal(5m, etsRow.ProfitPerDistanceSort);

        var noDistanceCompany = CompanyWithDriverDistance("ats-no-distance", "$", 1000, 0);
        var noDistanceRow = Wpf.ViewModels.Rows.Driver(noDistanceCompany, noDistanceCompany.Drivers.Single());

        Assert.Equal("-", noDistanceRow.ProfitPerDistance);
        Assert.Equal(0m, noDistanceRow.ProfitPerDistanceSort);
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
        if (folder == "ViewModels")
        {
            return string.Join(
                Environment.NewLine,
                ReadAllSource(root, "Controllers"),
                ReadAllSourceFromSingleFolder(root, "ViewModels"));
        }

        return ReadAllSourceFromSingleFolder(root, folder);
    }

    private static string ReadAllSourceFromSingleFolder(string root, string folder)
    {
        var path = Path.Combine(root, folder);
        if (!Directory.Exists(path))
            return string.Empty;

        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
    }

    private static void AssertFile(string root, params string[] segments)
    {
        var path = Path.Combine([root, .. segments]);
        Assert.True(File.Exists(path), $"Expected file '{path}' to exist.");
    }

    private static AtsEmployeeStats.Contracts.CompanyDto CompanyWithDriverDistance(
        string companyId,
        string currencySymbol,
        long profit,
        int distance) =>
        new(
            companyId,
            "Company",
            profit,
            Garages: [],
            Drivers: [new("driver.alice", "Alice", profit, 0, null, null, 1)],
            Trucks: [],
            Missions: [],
            TrailerTypes: [],
            RecentDriverJobs:
            [
                new("job.1", "driver.alice", null, "Cargo", "a", "b", profit, 0, profit, distance, 1)
            ],
            CurrencySymbol: currencySymbol);

    private static AtsEmployeeStats.Contracts.CompanyDto CompanyWithRoute(
        string companyId,
        string currencySymbol) =>
        new(
            companyId,
            "Company",
            0,
            Garages: [],
            Drivers: [],
            Trucks: [],
            Missions: [],
            TrailerTypes: [],
            Cities:
            [
                new("phoenix", "Phoenix", true, true, 1, 5000, 0, 5000, 0)
            ],
            Routes:
            [
                new("phoenix", "denver", 5000, 2, 10m, 0)
            ],
            CurrencySymbol: currencySymbol);

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
