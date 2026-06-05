using System.Collections.ObjectModel;
using System.IO;
using AtsEmployeeStats.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AtsEmployeeStats.Wpf.ViewModels;

internal sealed record ExplorerMatch(
    ExplorerNodeViewModel Node,
    IReadOnlyList<ExplorerNodeViewModel> Ancestors);

public sealed partial class GameSourceRowViewModel : ObservableObject
{
    public GameSourceRowViewModel(
        string gameKey,
        string gameName,
        string sourcePrefix,
        bool enabled,
        string? installPath,
        string? profilePath,
        string? savePath,
        IReadOnlyList<string> savePaths)
    {
        GameKey = gameKey;
        GameName = gameName;
        SourcePrefix = sourcePrefix;
        SavePaths = savePaths;
        Enabled = enabled;
        InstallPath = installPath ?? string.Empty;
        ProfilePath = profilePath ?? string.Empty;
        SavePath = savePath ?? string.Empty;
        SaveLocationsText = savePaths.Count == 0
            ? "No save locations selected"
            : $"{savePaths.Count:N0} save location(s) selected";
        SourceStatusText = enabled ? "Included" : "Not included";
    }

    public string GameKey { get; }

    public string GameName { get; }

    public string SourcePrefix { get; }

    public IReadOnlyList<string> SavePaths { get; }

    public string SaveLocationsText { get; }

    public string SourceStatusText { get; }

    [ObservableProperty]
    private bool enabled;

    [ObservableProperty]
    private string installPath = string.Empty;

    [ObservableProperty]
    private string profilePath = string.Empty;

    [ObservableProperty]
    private string savePath = string.Empty;
}

public sealed partial class GameSourceWizardGameViewModel : ObservableObject
{
    public GameSourceWizardGameViewModel(
        string gameKey,
        string gameName,
        string fullGameName,
        IEnumerable<GameSourceWizardInstallCandidateViewModel> installCandidates,
        IEnumerable<GameSourceWizardSaveRootCandidateViewModel> saveRootCandidates,
        GameSourceRowViewModel? existing)
    {
        GameKey = gameKey;
        GameName = gameName;
        FullGameName = fullGameName;
        InstallCandidates = new ObservableCollection<GameSourceWizardInstallCandidateViewModel>(installCandidates);
        SaveRootCandidates = new ObservableCollection<GameSourceWizardSaveRootCandidateViewModel>(saveRootCandidates);

        var selectedInstall = InstallCandidates.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(existing?.InstallPath) &&
            string.Equals(candidate.Path, existing.InstallPath, StringComparison.OrdinalIgnoreCase)) ??
            InstallCandidates.FirstOrDefault(candidate => candidate.IsValid) ??
            InstallCandidates.FirstOrDefault();
        if (selectedInstall is not null)
            selectedInstall.IsSelected = true;

        foreach (var saveRoot in SaveRootCandidates)
        {
            saveRoot.IsSelected =
                (!string.IsNullOrWhiteSpace(existing?.SavePath) &&
                 string.Equals(saveRoot.Path, existing.SavePath, StringComparison.OrdinalIgnoreCase)) ||
                saveRoot.IsValid;
        }

        HasGame = existing?.Enabled ?? SaveRootCandidates.Any(candidate => candidate.IsValid);
    }

    public string GameKey { get; }

    public string GameName { get; }

    public string FullGameName { get; }

    [ObservableProperty]
    private bool hasGame;

    public ObservableCollection<GameSourceWizardInstallCandidateViewModel> InstallCandidates { get; }

    public ObservableCollection<GameSourceWizardSaveRootCandidateViewModel> SaveRootCandidates { get; }

    public string? DeriveProfilePath()
    {
        var savePath = SaveRootCandidates.FirstOrDefault(candidate => candidate.IsSelected)?.Path;
        if (string.IsNullOrWhiteSpace(savePath))
            return null;

        var name = Path.GetFileName(savePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.Equals(name, "profiles", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "steam_profiles", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(savePath);
        }

        return savePath;
    }
}

public sealed partial class GameSourceWizardInstallCandidateViewModel(
    string path,
    bool isValid,
    IReadOnlyList<string> proofs) : ObservableObject
{
    public string Path { get; } = path;

    public bool IsValid { get; } = isValid;

    public string ProofText { get; } = string.Join("; ", proofs);

    [ObservableProperty]
    private bool isSelected;
}

public sealed partial class GameSourceWizardSaveRootCandidateViewModel(
    string path,
    bool isValid,
    int saveFileCount,
    IReadOnlyList<string> proofs) : ObservableObject
{
    public string Path { get; } = path;

    public bool IsValid { get; } = isValid;

    public int SaveFileCount { get; } = saveFileCount;

    public string ProofText { get; } = string.Join("; ", proofs);

    [ObservableProperty]
    private bool isSelected;
}

public sealed class GameSaveRowViewModel
{
    public GameSaveRowViewModel(
        string gameKey,
        string profileName,
        string saveName,
        string saveDirectory,
        string sourceKey,
        string? saveRootPath)
    {
        GameKey = gameKey;
        ProfileName = profileName;
        SaveName = saveName;
        SaveDirectory = saveDirectory;
        SourceKey = sourceKey;
        SaveRootPath = saveRootPath ?? string.Empty;
    }

    public string GameKey { get; }

    public string ProfileName { get; }

    public string SaveName { get; }

    public string SaveDirectory { get; }

    public string SourceKey { get; }

    public string SaveRootPath { get; }
}

public sealed class CompaniesDetailViewModel : EntityDetailViewModel
{
    public CompaniesDetailViewModel(IReadOnlyList<CompanyDto> companies)
        : base("Companies", "All trucking companies", RowFormatting.Money(companies.Sum(company => company.Profit), companies.FirstOrDefault()?.CurrencySymbol ?? "$"))
    {
        Metrics.Add(new("Companies", RowFormatting.Count(companies.Count)));
        Metrics.Add(new("Profit", RowFormatting.Money(companies.Sum(company => company.Profit), companies.FirstOrDefault()?.CurrencySymbol ?? "$")));
        Metrics.Add(new("Drivers", RowFormatting.Count(companies.Sum(company => company.Drivers.Count))));
        Metrics.Add(new("Trucks", RowFormatting.Count(companies.Sum(company => company.Trucks.Count))));
        Tabs.Add(new("Companies", companies.Select(company => new GridRowViewModel(
            company.DisplayName,
            RowFormatting.Money(company.Profit, company.CurrencySymbol),
            $"{company.Garages.Count:N0} garages / {company.Drivers.Count:N0} drivers / {company.Trucks.Count:N0} trucks",
            $"{company.Missions.Count:N0} jobs",
            RowFormatting.Trend(company.ProfitTrend),
            company)
        {
            Target = new(ExplorerNodeKind.Company, company.Id),
            ProfitSort = company.Profit
        }), TableColumns.Companies));
    }
}
