using AtsEmployeeStats.Contracts;
using AtsEmployeeStats.Wpf.ViewModels;
using static AtsEmployeeStats.Wpf.ViewModels.DetailHelpers;

namespace AtsEmployeeStats.Wpf.Controllers;

public sealed record ExplorerSelectionPresenter(
    EntityDetailViewModel Detail,
    ExplorerNodeViewModel SelectedNode,
    string? StatusText = null);

public sealed class ExplorerPresenter
{
    public CompanyExplorerViewModel Explorer { get; } = new();

    public void BuildExplorer(
        IReadOnlyList<CompanyDto> companies,
        IEnumerable<GameSourceRowViewModel> gameSources,
        IEnumerable<GameSaveRowViewModel> gameSaves)
    {
        var sourceRows = gameSources.ToList();
        var saveRows = gameSaves.ToList();
        var root = new ExplorerNodeViewModel("Games", ExplorerNodeKind.Games)
        {
            IsExpanded = true
        };
        var unpartitionedCompanies = companies
            .Where(company => !sourceRows.Any(gameSource => company.Id.StartsWith(gameSource.SourcePrefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        foreach (var gameSource in sourceRows.OrderBy(source => source.GameName, StringComparer.CurrentCultureIgnoreCase))
        {
            var gameNode = new ExplorerNodeViewModel(gameSource.GameName, ExplorerNodeKind.GameSource)
            {
                IsExpanded = true
            };
            var savesNode = new ExplorerNodeViewModel("Save Locations", ExplorerNodeKind.GameSaves)
            {
                IsExpanded = true
            };
            foreach (var saveLocation in saveRows
                .Where(save => save.GameKey == gameSource.GameKey)
                .Where(save => !string.IsNullOrWhiteSpace(save.SaveRootPath))
                .GroupBy(save => save.SaveRootPath, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                var saveRootPath = saveLocation.Key ?? string.Empty;
                var locationNode = new ExplorerNodeViewModel(
                    saveLocation.Key ?? "Unknown save location",
                    ExplorerNodeKind.SaveLocation,
                    entityId: saveLocation.Key)
                {
                    IsExpanded = true
                };
                var companiesNode = new ExplorerNodeViewModel("Companies", ExplorerNodeKind.Companies, entityId: saveLocation.Key)
                {
                    IsExpanded = true
                };
                var locationCompanies = GetCompaniesForSaveLocation(saveRootPath, companies, saveRows)
                    .GroupBy(company => company.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase);
                foreach (var company in locationCompanies)
                {
                    companiesNode.Children.Add(BuildSaveLocationCompanyNode(
                        company.Key,
                        company,
                        saveRootPath));
                }

                locationNode.Children.Add(companiesNode);
                savesNode.Children.Add(locationNode);
            }

            gameNode.Children.Add(savesNode);
            root.Children.Add(gameNode);
        }

        if (unpartitionedCompanies.Count > 0)
        {
            var companiesNode = new ExplorerNodeViewModel("Companies", ExplorerNodeKind.Companies)
            {
                IsExpanded = true
            };
            foreach (var company in unpartitionedCompanies.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase))
                companiesNode.Children.Add(BuildCompanyNode(company));
            root.Children.Add(companiesNode);
        }

        Explorer.Roots.Clear();
        Explorer.Roots.Add(root);
    }

    public ExplorerSelectionPresenter? SelectNode(
        ExplorerNodeViewModel? node,
        IReadOnlyList<CompanyDto> companies,
        IEnumerable<GameSaveRowViewModel> gameSaves,
        EntityDetailViewModel? currentDetail = null)
    {
        if (node is null)
            return null;

        var saveRows = gameSaves.ToList();
        if (node.Kind == ExplorerNodeKind.Companies)
            CollapseCompanyDetailNodes();
        else
            ExpandExplorerToNode(node);

        if (node.Kind == ExplorerNodeKind.SaveLocation && !string.IsNullOrWhiteSpace(node.EntityId))
        {
            var locationCompanies = GetCompaniesForSaveLocation(node.EntityId, companies, saveRows);
            return new(new CompaniesDetailViewModel(locationCompanies), node, $"Save location selected: {node.Name}");
        }

        if (node.Kind == ExplorerNodeKind.Companies && !string.IsNullOrWhiteSpace(node.EntityId))
        {
            var locationCompanies = GetCompaniesForSaveLocation(node.EntityId, companies, saveRows);
            return new(new CompaniesDetailViewModel(locationCompanies), node, $"Companies selected: {node.Name}");
        }

        if (node.Kind == ExplorerNodeKind.Companies)
            return new(new CompaniesDetailViewModel(companies), node);

        var company = companies.FirstOrDefault(item => Same(item.Id, node.CompanyId));
        if (company is null)
            return null;

        var detail = node.Kind switch
        {
            ExplorerNodeKind.Company => new CompanyDetailViewModel(company),
            ExplorerNodeKind.SaveLocationCompany => new CompanyDetailViewModel(company),
            ExplorerNodeKind.Garages => new CompanyDetailViewModel(company, "Garages"),
            ExplorerNodeKind.Drivers => new CompanyDetailViewModel(company, "Drivers"),
            ExplorerNodeKind.Trucks => new CompanyDetailViewModel(company, "Trucks"),
            ExplorerNodeKind.Trailers => new CompanyDetailViewModel(company, "Trailers"),
            ExplorerNodeKind.Jobs => new CompanyDetailViewModel(company, "Jobs"),
            ExplorerNodeKind.Cities => new CompanyDetailViewModel(company, "Cities"),
            ExplorerNodeKind.Garage => company.Garages.FirstOrDefault(item => Same(item.Id, node.EntityId)) is { } garage ? new GarageDetailViewModel(company, garage) : currentDetail,
            ExplorerNodeKind.Driver => company.Drivers.FirstOrDefault(item => Same(item.Id, node.EntityId)) is { } driver ? new DriverDetailViewModel(company, driver) : currentDetail,
            ExplorerNodeKind.Truck => company.Trucks.FirstOrDefault(item => Same(item.Id, node.EntityId)) is { } truck ? new TruckDetailViewModel(company, truck) : currentDetail,
            ExplorerNodeKind.Trailer => (company.Trailers ?? []).FirstOrDefault(item => Same(item.LicensePlate, node.EntityId) || Same(item.Id, node.EntityId)) is { } trailer ? new TrailerDetailViewModel(company, trailer) : currentDetail,
            ExplorerNodeKind.Job => company.Missions.FirstOrDefault(item => Same(item.Id, node.EntityId)) is { } job ? new JobDetailViewModel(company, job) : currentDetail,
            ExplorerNodeKind.City => (company.Cities ?? []).FirstOrDefault(item => Same(item.Id, node.EntityId)) is { } city ? new CityDetailViewModel(company, city) : currentDetail,
            _ => currentDetail
        };

        if (detail is null)
            return null;

        return new(
            detail,
            node,
            node.Kind == ExplorerNodeKind.SaveLocationCompany ? $"Company selected: {company.DisplayName}" : null);
    }

    public void ExpandExplorerToNode(RowNavigationTargetViewModel target) =>
        ExpandExplorerToNode(new ExplorerNodeViewModel(
            string.Empty,
            target.Kind,
            target.CompanyId,
            target.EntityId));

    public void ExpandExplorerToNode(ExplorerNodeViewModel target)
    {
        var matching = Explorer.Roots
            .SelectMany(root => FindExplorerMatches(root, target, []))
            .FirstOrDefault();
        if (matching is null)
            return;

        ExpandAncestorPath(matching.Ancestors);
        if (ShouldExpandMatchedNode(target.Kind))
            matching.Node.IsExpanded = true;
    }

    private static ExplorerNodeViewModel BuildCompanyNode(CompanyDto company)
    {
        var companyNode = new ExplorerNodeViewModel(company.DisplayName, ExplorerNodeKind.Company, company.Id);
        AddCompanyCollections(companyNode, company);
        return companyNode;
    }

    private static ExplorerNodeViewModel BuildSaveLocationCompanyNode(
        string displayName,
        IEnumerable<CompanyDto> companies,
        string saveRootPath)
    {
        var company = companies.First();
        var companyNode = new ExplorerNodeViewModel(
            displayName,
            ExplorerNodeKind.SaveLocationCompany,
            company.Id,
            saveRootPath);
        AddCompanyCollections(companyNode, company);
        return companyNode;
    }

    private static void ExpandAncestorPath(IEnumerable<ExplorerNodeViewModel> ancestors)
    {
        foreach (var ancestor in ancestors)
            ancestor.IsExpanded = true;
    }

    private void CollapseCompanyDetailNodes()
    {
        foreach (var root in Explorer.Roots)
            CollapseCompanyDetailNodes(root);
    }

    private static void CollapseCompanyDetailNodes(ExplorerNodeViewModel node)
    {
        if (IsCompanyDetailNode(node.Kind))
            node.IsExpanded = false;

        foreach (var child in node.Children)
            CollapseCompanyDetailNodes(child);
    }

    private static IEnumerable<ExplorerMatch> FindExplorerMatches(
        ExplorerNodeViewModel node,
        ExplorerNodeViewModel target,
        IReadOnlyList<ExplorerNodeViewModel> ancestors)
    {
        if (MatchesExplorerNode(node, target))
            yield return new ExplorerMatch(node, ancestors);

        var nextAncestors = ancestors.Append(node).ToArray();
        foreach (var child in node.Children)
        {
            foreach (var match in FindExplorerMatches(child, target, nextAncestors))
                yield return match;
        }
    }

    private static bool MatchesExplorerNode(ExplorerNodeViewModel node, ExplorerNodeViewModel target) =>
        target.Kind switch
        {
            ExplorerNodeKind.Companies =>
                node.Kind == ExplorerNodeKind.Companies &&
                (string.IsNullOrWhiteSpace(target.EntityId) || Same(node.EntityId, target.EntityId)),
            ExplorerNodeKind.Company =>
                (node.Kind == ExplorerNodeKind.Company || node.Kind == ExplorerNodeKind.SaveLocationCompany) &&
                Same(node.CompanyId, target.CompanyId),
            ExplorerNodeKind.SaveLocationCompany =>
                node.Kind == ExplorerNodeKind.SaveLocationCompany &&
                Same(node.CompanyId, target.CompanyId) &&
                (string.IsNullOrWhiteSpace(target.EntityId) || Same(node.EntityId, target.EntityId)),
            ExplorerNodeKind.Garages or ExplorerNodeKind.Drivers or ExplorerNodeKind.Trucks or
                ExplorerNodeKind.Trailers or ExplorerNodeKind.Jobs or ExplorerNodeKind.Cities =>
                node.Kind == target.Kind && Same(node.CompanyId, target.CompanyId),
            ExplorerNodeKind.Garage or ExplorerNodeKind.Driver or ExplorerNodeKind.Truck or
                ExplorerNodeKind.Trailer or ExplorerNodeKind.Job or ExplorerNodeKind.City =>
                node.Kind == target.Kind && Same(node.CompanyId, target.CompanyId) && Same(node.EntityId, target.EntityId),
            _ => false
        };

    private static bool ShouldExpandMatchedNode(ExplorerNodeKind kind) =>
        kind is ExplorerNodeKind.Company or ExplorerNodeKind.SaveLocationCompany or
            ExplorerNodeKind.Garages or ExplorerNodeKind.Drivers or ExplorerNodeKind.Trucks or
            ExplorerNodeKind.Trailers or ExplorerNodeKind.Jobs or ExplorerNodeKind.Cities;

    private static bool IsCompanyDetailNode(ExplorerNodeKind kind) =>
        kind is ExplorerNodeKind.Company or ExplorerNodeKind.SaveLocationCompany or
            ExplorerNodeKind.Garages or ExplorerNodeKind.Drivers or ExplorerNodeKind.Trucks or
            ExplorerNodeKind.Trailers or ExplorerNodeKind.Jobs or ExplorerNodeKind.Cities;

    private static void AddCompanyCollections(ExplorerNodeViewModel companyNode, CompanyDto company)
    {
        AddCollection(companyNode, "Garages", ExplorerNodeKind.Garages, company.Id, company.Garages.Select(item => new ExplorerNodeViewModel(item.DisplayName, ExplorerNodeKind.Garage, company.Id, item.Id)));
        AddCollection(companyNode, "Drivers", ExplorerNodeKind.Drivers, company.Id, company.Drivers.Select(item => new ExplorerNodeViewModel(item.DisplayName, ExplorerNodeKind.Driver, company.Id, item.Id)));
        AddCollection(companyNode, "Trucks", ExplorerNodeKind.Trucks, company.Id, company.Trucks.Select(item => new ExplorerNodeViewModel(item.DisplayName, ExplorerNodeKind.Truck, company.Id, item.Id)));
        AddCollection(companyNode, "Trailers", ExplorerNodeKind.Trailers, company.Id, (company.Trailers ?? []).Select(item => new ExplorerNodeViewModel(item.LicensePlate ?? item.Id, ExplorerNodeKind.Trailer, company.Id, item.LicensePlate ?? item.Id)));
        AddCollection(companyNode, "Jobs", ExplorerNodeKind.Jobs, company.Id, company.Missions.Take(250).Select(item => new ExplorerNodeViewModel(string.IsNullOrWhiteSpace(item.Cargo) ? item.Id : item.Cargo!, ExplorerNodeKind.Job, company.Id, item.Id)));
        AddCollection(companyNode, "Cities", ExplorerNodeKind.Cities, company.Id, (company.Cities ?? []).Select(item => new ExplorerNodeViewModel(item.DisplayName, ExplorerNodeKind.City, company.Id, item.Id)));
    }

    private static void AddCollection(
        ExplorerNodeViewModel companyNode,
        string title,
        ExplorerNodeKind kind,
        string companyId,
        IEnumerable<ExplorerNodeViewModel> children)
    {
        var collection = new ExplorerNodeViewModel(title, kind, companyId);
        foreach (var child in children.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
            collection.Children.Add(child);
        companyNode.Children.Add(collection);
    }

    private static IReadOnlyList<CompanyDto> GetCompaniesForSaveLocation(
        string saveRootPath,
        IReadOnlyList<CompanyDto> companies,
        IReadOnlyList<GameSaveRowViewModel> gameSaves)
    {
        var sourcePrefixes = gameSaves
            .Where(save => string.Equals(save.SaveRootPath, saveRootPath, StringComparison.OrdinalIgnoreCase))
            .Select(save => $"{NormalizeSourceKey(save.SourceKey)}:")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return companies
            .Where(company => sourcePrefixes.Any(prefix => company.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(company => company.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string NormalizeSourceKey(string sourceKey)
    {
        var normalized = new string(sourceKey
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        normalized = string.Join('-', normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length == 0 ? "default" : normalized;
    }
}
