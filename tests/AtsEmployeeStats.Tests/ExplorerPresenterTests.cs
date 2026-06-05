using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Wpf.Controllers;
using AtsEmployeeStats.Wpf.ViewModels;

namespace AtsEmployeeStats.Tests;

public sealed class ExplorerPresenterTests
{
    [Fact]
    public void BuildExplorer_groups_save_location_companies_by_game_and_save_root()
    {
        var presenter = new ExplorerPresenter();
        var companies = new[]
        {
            Company("ats-source:company-a", "Northwind", garageId: "garage-a"),
            Company("ats-source:company-b", "Northwind", garageId: "garage-b"),
            Company("loose-company", "Loose Company", garageId: "garage-c")
        };
        var sources = new[]
        {
            new GameSourceRowViewModel("Ats", "ATS", "ats-", true, null, null, null, [])
        };
        var saves = new[]
        {
            new GameSaveRowViewModel("Ats", "Profile", "Save 1", @"C:\ATS\Save 1", "ats source", @"C:\ATS")
        };

        presenter.BuildExplorer(companies, sources, saves);

        var root = Assert.Single(presenter.Explorer.Roots);
        Assert.Equal("Games", root.Name);
        Assert.True(root.IsExpanded);

        var game = Assert.Single(root.Children, node => node.Kind == ExplorerNodeKind.GameSource);
        Assert.Equal("ATS", game.Name);

        var saveLocation = Assert.Single(game.Children.Single().Children);
        Assert.Equal(ExplorerNodeKind.SaveLocation, saveLocation.Kind);
        Assert.Equal(@"C:\ATS", saveLocation.EntityId);

        var saveCompanies = Assert.Single(saveLocation.Children);
        Assert.Equal(ExplorerNodeKind.Companies, saveCompanies.Kind);
        Assert.Equal(@"C:\ATS", saveCompanies.EntityId);
        var groupedCompany = Assert.Single(saveCompanies.Children);
        Assert.Equal("Northwind", groupedCompany.Name);
        Assert.Equal(ExplorerNodeKind.SaveLocationCompany, groupedCompany.Kind);
        Assert.Equal(@"C:\ATS", groupedCompany.EntityId);
        Assert.Contains(groupedCompany.Children, child => child.Kind == ExplorerNodeKind.Garages);

        var looseCompanies = Assert.Single(root.Children, node => node.Kind == ExplorerNodeKind.Companies);
        Assert.Equal("Loose Company", Assert.Single(looseCompanies.Children).Name);
    }

    [Fact]
    public void SelectNode_returns_company_collection_detail_for_save_location_companies_node()
    {
        var presenter = new ExplorerPresenter();
        var companies = new[]
        {
            Company("ats-source:company-a", "Northwind A", profit: 100),
            Company("ats-source:company-b", "Northwind B", profit: 200),
            Company("other-source:company-c", "Other", profit: 400)
        };
        var saves = new[]
        {
            new GameSaveRowViewModel("Ats", "Profile", "Save 1", @"C:\ATS\Save 1", "ats source", @"C:\ATS")
        };
        presenter.BuildExplorer(companies, [], saves);

        var result = presenter.SelectNode(
            new ExplorerNodeViewModel("Companies", ExplorerNodeKind.Companies, entityId: @"C:\ATS"),
            companies,
            saves);

        Assert.NotNull(result);
        var detail = Assert.IsType<CompaniesDetailViewModel>(result.Detail);
        Assert.Equal("Companies selected: Companies", result.StatusText);
        Assert.Equal("$300", detail.ProfitText);
        Assert.Equal(2, detail.Tabs.Single().Rows.Count);
    }

    [Fact]
    public void SelectNode_expands_ancestor_path_and_returns_entity_detail()
    {
        var presenter = new ExplorerPresenter();
        var companies = new[]
        {
            Company("company-a", "Northwind", garageId: "garage-a")
        };
        presenter.BuildExplorer(companies, [], []);

        var result = presenter.SelectNode(
            new ExplorerNodeViewModel("Garage A", ExplorerNodeKind.Garage, "company-a", "garage-a"),
            companies,
            []);

        Assert.NotNull(result);
        Assert.IsType<GarageDetailViewModel>(result.Detail);
        var companyNode = presenter.Explorer.Roots.Single()
            .Children.Single()
            .Children.Single();
        var garagesNode = companyNode.Children.Single(child => child.Kind == ExplorerNodeKind.Garages);
        Assert.True(companyNode.IsExpanded);
        Assert.True(garagesNode.IsExpanded);
    }

    private static CompanyDto Company(
        string id,
        string displayName,
        long profit = 0,
        string garageId = "garage") =>
        new(
            id,
            displayName,
            profit,
            [new GarageDto(garageId, GarageName(garageId), profit, profit, 0, 0)],
            [],
            [],
            [],
            []);

    private static string GarageName(string id) =>
        id switch
        {
            "garage-a" => "Garage A",
            "garage-b" => "Garage B",
            "garage-c" => "Garage C",
            _ => id
        };
}
