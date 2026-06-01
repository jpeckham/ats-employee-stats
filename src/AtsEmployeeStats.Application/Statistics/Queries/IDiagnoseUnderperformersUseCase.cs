using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public interface IDiagnoseUnderperformersUseCase
{
    Task ExecuteAsync(
        IOutputBoundaryAdapter<IReadOnlyList<UnderperformerDiagnosisDto>> output,
        DiagnoseUnderperformersInputData input,
        IProgressOutputBoundaryAdapter? progress,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UnderperformerDiagnosisDto>> DiagnoseAsync(
        string companyId,
        DashboardQueryOptions options,
        CancellationToken cancellationToken,
        IProgress<SaveLoadProgress>? progress = null,
        int count = 5);
}
