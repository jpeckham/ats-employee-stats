using System.Collections.ObjectModel;
using System.IO;
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Wpf.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AtsEmployeeStats.Wpf.Controllers;

public sealed partial class GameSourcePresenter(
    GameSourceManagementUseCase gameSourceManagement,
    GameSaveCatalogUseCase gameSaveCatalog) : ObservableObject
{
    [ObservableProperty]
    private bool isSourceWizardVisible;

    [ObservableProperty]
    private int currentWizardIndex;

    [ObservableProperty]
    private GameSourceWizardGameViewModel? currentWizardGame;

    public ObservableCollection<GameSourceRowViewModel> GameSources { get; } = [];

    public ObservableCollection<GameSaveRowViewModel> GameSaves { get; } = [];

    public ObservableCollection<GameSourceWizardGameViewModel> SourceWizardGames { get; } = [];

    public bool CanReloadSaves =>
        GameSources.Any(source => source.Enabled && source.SavePath.Length > 0);

    public string SourceWizardStepText =>
        SourceWizardGames.Count == 0 ? string.Empty : $"Step {CurrentWizardIndex + 1:N0} of {SourceWizardGames.Count:N0}";

    public Task<bool> RequiresWizardAsync() =>
        Task.Run(() => gameSourceManagement.RequiresWizardAsync(CancellationToken.None));

    public async Task LoadGameSourcesAsync()
    {
        var sources = await Task.Run(() => gameSourceManagement.DiscoverAsync(CancellationToken.None));
        GameSources.Clear();
        foreach (var source in sources)
            GameSources.Add(ToViewModel(source));
        await LoadGameSavesAsync();
    }

    public async Task<(bool Succeeded, string StatusText)> StartSourceWizardAsync()
    {
        try
        {
            var wizardGames = await Task.Run(async () =>
            {
                var rows = GameSources.ToList();
                var games = new List<GameSourceWizardGameViewModel>();
                foreach (var game in new[] { GameType.Ats, GameType.Ets2 })
                {
                    var candidates = await gameSourceManagement.DiscoverCandidatesAsync(game, CancellationToken.None);
                    var existing = rows.FirstOrDefault(source => string.Equals(source.GameKey, game.ToString(), StringComparison.OrdinalIgnoreCase));
                    games.Add(new GameSourceWizardGameViewModel(
                        game.ToString(),
                        DisplayName(game),
                        FullDisplayName(game),
                        candidates.InstallCandidates.Select(candidate => new GameSourceWizardInstallCandidateViewModel(
                            candidate.Path,
                            candidate.IsValid,
                            candidate.Proofs)),
                        candidates.SaveRootCandidates.Select(candidate => new GameSourceWizardSaveRootCandidateViewModel(
                            candidate.Path,
                            candidate.IsValid,
                            candidate.SaveFileCount,
                            candidate.Proofs)),
                        existing));
                }

                return games;
            });

            SourceWizardGames.Clear();
            foreach (var wizardGame in wizardGames)
                SourceWizardGames.Add(wizardGame);

            CurrentWizardIndex = 0;
            CurrentWizardGame = SourceWizardGames.FirstOrDefault();
            IsSourceWizardVisible = true;
            OnPropertyChanged(nameof(SourceWizardStepText));
            return (true, "Review game sources before importing saves.");
        }
        catch (Exception ex)
        {
            return (false, $"Unable to discover game sources: {ex.Message}");
        }
    }

    public void PreviousSourceWizardStep()
    {
        if (CurrentWizardIndex <= 0)
            return;

        CurrentWizardIndex--;
        CurrentWizardGame = SourceWizardGames[CurrentWizardIndex];
        OnPropertyChanged(nameof(SourceWizardStepText));
    }

    public void NextSourceWizardStep()
    {
        if (CurrentWizardIndex >= SourceWizardGames.Count - 1)
            return;

        CurrentWizardIndex++;
        CurrentWizardGame = SourceWizardGames[CurrentWizardIndex];
        OnPropertyChanged(nameof(SourceWizardStepText));
    }

    public async Task<(bool Succeeded, string StatusText)> FinishSourceWizardAsync()
    {
        try
        {
            var configurations = SourceWizardGames.Select(ToConfiguration).ToList();
            var result = await Task.Run(() => gameSourceManagement.SaveValidatedAsync(
                configurations,
                CancellationToken.None));
            if (!result.Saved)
                return (false, string.Join(" ", result.Errors));

            IsSourceWizardVisible = false;
            await LoadGameSourcesAsync();
            return (true, "Source setup saved. Reload saves to import enabled sources.");
        }
        catch (Exception ex)
        {
            return (false, $"Unable to save source setup: {ex.Message}");
        }
    }

    public async Task LoadGameSavesAsync()
    {
        var configurations = GameSources.Select(ToConfiguration).ToList();
        var saves = await Task.Run(() => gameSaveCatalog.FindSaveGamesAsync(
            configurations,
            CancellationToken.None));
        GameSaves.Clear();
        foreach (var save in saves)
            GameSaves.Add(ToViewModel(save));
    }

    private static GameSourceRowViewModel ToViewModel(GameSourceConfiguration source) =>
        new(
            source.Game.ToString(),
            DisplayName(source.Game),
            source.Game == GameType.Ats ? "ats-" : "ets2-",
            source.Enabled,
            source.InstallPath,
            source.ProfilePath,
            source.SavePath,
            source.EffectiveSavePaths);

    private static GameSaveRowViewModel ToViewModel(SaveGame save) =>
        new(
            save.Game.ToString(),
            save.ProfileName,
            save.SaveName,
            save.SaveDirectory,
            save.SourceKey,
            save.SaveRootPath);

    private static GameSourceConfiguration ToConfiguration(GameSourceRowViewModel source) =>
        new(
            ParseGameType(source.GameKey),
            source.Enabled,
            string.IsNullOrWhiteSpace(source.InstallPath) ? null : source.InstallPath,
            string.IsNullOrWhiteSpace(source.ProfilePath) ? null : source.ProfilePath,
            string.IsNullOrWhiteSpace(source.SavePath) ? null : source.SavePath,
            source.SavePaths);

    private static GameSourceConfiguration ToConfiguration(GameSourceWizardGameViewModel game)
    {
        var savePaths = game.SaveRootCandidates
            .Where(candidate => candidate.IsSelected)
            .Select(candidate => candidate.Path)
            .ToList();
        return new(
            ParseGameType(game.GameKey),
            game.HasGame,
            game.InstallCandidates.FirstOrDefault(candidate => candidate.IsSelected)?.Path,
            DeriveProfilePath(savePaths.FirstOrDefault()),
            savePaths.FirstOrDefault(),
            savePaths);
    }

    private static string? DeriveProfilePath(string? savePath)
    {
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

    private static GameType ParseGameType(string gameKey) =>
        Enum.Parse<GameType>(gameKey, ignoreCase: true);

    private static string DisplayName(GameType game) =>
        game == GameType.Ats ? "ATS" : "ETS2";

    private static string FullDisplayName(GameType game) =>
        game == GameType.Ats ? "American Truck Simulator" : "Euro Truck Simulator 2";
}
