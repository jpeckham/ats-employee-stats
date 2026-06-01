using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public interface IRecommendDriverSkillsUseCase
{
    Task ExecuteAsync(
        IOutputBoundaryAdapter<IReadOnlyList<DriverSkillRecommendationDto>> output,
        RecommendDriverSkillsInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DriverSkillRecommendationDto>> RecommendAsync(
        string companyId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null,
        int count = 5);
}
