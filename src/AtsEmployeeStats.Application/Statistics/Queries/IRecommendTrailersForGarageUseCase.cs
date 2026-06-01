using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public interface IRecommendTrailersForGarageUseCase
{
    Task ExecuteAsync(
        IOutputBoundaryAdapter<IReadOnlyList<TrailerRecommendationDto>> output,
        RecommendTrailersForGarageInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TrailerRecommendationDto>> RecommendAsync(
        string companyId,
        string garageId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null,
        int count = 3);
}
