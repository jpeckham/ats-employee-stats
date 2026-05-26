namespace AtsEmployeeStats.Application.Saves;

public enum SaveLoadStage
{
    DiscoveringFiles,
    FilesDiscovered,
    LoadingFiles,
    FileLoaded,
    Completed
}

public sealed record SaveLoadProgress(
    SaveLoadStage Stage,
    int CompletedFiles,
    int TotalFiles,
    int CompletedUnits,
    int TotalUnits,
    string Message,
    string? CurrentFile = null,
    int CurrentFileCompletedUnits = 0,
    int CurrentFileTotalUnits = 0);
