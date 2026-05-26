using AtsEmployeeStats.Domain.Saves;

namespace AtsEmployeeStats.Application.Saves;

public interface ISaveSnapshotSource
{
    Task<IReadOnlyList<SaveSnapshot>> ReadAllAsync(
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);
}
