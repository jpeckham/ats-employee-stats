using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public interface IRecommendNextGarageCityUseCase
{
    Task ExecuteAsync(
        IOutputBoundaryAdapter<GarageCityRecommendationDto?> output,
        RecommendNextGarageCityInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task<GarageCityRecommendationDto?> RecommendAsync(
        string companyId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null);
}
