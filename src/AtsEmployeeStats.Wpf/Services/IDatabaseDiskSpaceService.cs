namespace AtsEmployeeStats.Wpf.Services;

public sealed record DatabaseDiskSpaceEstimate(
    long SelectedSaveBytes,
    long ProjectedDatabaseBytes,
    long ExistingDatabaseBytes,
    long RequiredAdditionalBytes,
    long FreeBytes,
    bool HasEnoughSpace);

public interface IDatabaseDiskSpaceService
{
    DatabaseDiskSpaceEstimate Estimate(IReadOnlyList<string> saveRoots);
}
