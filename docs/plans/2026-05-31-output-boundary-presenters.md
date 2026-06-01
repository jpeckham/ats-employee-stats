# Output Boundary Presenters Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Refactor the statistics dashboard and reload flows to strict Clean Architecture input and output boundaries so API and MAUI presentation logic live in outer-layer presenters.

**Architecture:** Application use cases will accept request models plus output boundary adapters and will not return view DTOs directly. API presenters will map use-case responses to HTTP results and SignalR messages. MAUI presenters will map the same use-case responses to observable desktop view state.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, SignalR, .NET MAUI, xUnit, SQLite medallion statistics source.

---

## Approved Design

Application owns:
- input boundary interfaces
- output boundary interfaces
- request models
- response models
- use-case orchestration

API owns:
- endpoint/controller code
- HTTP presenters
- SignalR progress/status presenters
- mapping application responses to API view models and `IResult`

Web owns:
- clean client controllers/services that call the API
- binding returned API view models to Blazor components
- client-side SignalR handlers only for pushed server messages

MAUI owns:
- local controller/view-model orchestration
- MAUI presenters that update observable state directly
- no HTTP dependency for local in-process use cases

The first implementation slice is dashboard/reload and progress/status output. Recommendation and detail use cases should stay on the existing return-value style until the pattern is proven.

## Task 1: Add Application Output Boundary Contracts

**Files:**
- Create: `src/AtsEmployeeStats.Application/Statistics/Output/IOutputBoundaryAdapter.cs`
- Create: `src/AtsEmployeeStats.Application/Statistics/Output/IProgressOutputBoundaryAdapter.cs`
- Create: `src/AtsEmployeeStats.Application/Statistics/Queries/DashboardQueryRequest.cs`
- Modify: `src/AtsEmployeeStats.Application/Statistics/Queries/IStatisticsDashboardUseCases.cs`
- Modify: `src/AtsEmployeeStats.Application/Statistics/Queries/IStatisticsReloadUseCase.cs`
- Test: `tests/AtsEmployeeStats.Tests/StatisticsDashboardOutputBoundaryTests.cs`

**Step 1: Write the failing test**

Create `StatisticsDashboardOutputBoundaryTests.cs` with a test presenter:

```csharp
private sealed class CapturingOutput<T> : IOutputBoundaryAdapter<T>
{
    public T? Response { get; private set; }

    public Task PresentAsync(T response, CancellationToken cancellationToken)
    {
        Response = response;
        return Task.CompletedTask;
    }
}
```

Write a test named `Dashboard_use_case_presents_dashboard_response_through_output_boundary` that creates the existing fake snapshot source/service setup, calls the new `ExecuteDashboardAsync(request, output, progress, ct)` method, and asserts `output.Response` is not null and contains the expected company id.

**Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test .\tests\AtsEmployeeStats.Tests\AtsEmployeeStats.Tests.csproj --filter StatisticsDashboardOutputBoundaryTests
```

Expected: compile failure because `IOutputBoundaryAdapter<T>` and `ExecuteDashboardAsync` do not exist.

**Step 3: Add minimal contracts**

Add:

```csharp
namespace AtsEmployeeStats.Application.Statistics.Output;

public interface IOutputBoundaryAdapter<in TResponse>
{
    Task PresentAsync(TResponse response, CancellationToken cancellationToken);
}
```

Add:

```csharp
using AtsEmployeeStats.Application.Saves;

namespace AtsEmployeeStats.Application.Statistics.Output;

public interface IProgressOutputBoundaryAdapter
{
    Task PresentProgressAsync(SaveLoadProgress progress, CancellationToken cancellationToken);
}
```

Add `DashboardQueryRequest` as the input request type. For this slice it can wrap the existing dashboard options:

```csharp
using AtsEmployeeStats.Contracts;

namespace AtsEmployeeStats.Application.Statistics.Queries;

public sealed record DashboardQueryRequest(
    int? FromDay = null,
    int? ToDay = null,
    CollectionSortDto? Sort = null)
{
    public DashboardQueryOptions ToOptions() => new(FromDay, ToDay, Sort);
}
```

**Step 4: Add output-boundary methods while preserving old methods temporarily**

Extend `IStatisticsDashboardUseCases` with:

```csharp
Task ExecuteDashboardAsync(
    DashboardQueryRequest request,
    IOutputBoundaryAdapter<DashboardStatisticsDto> output,
    IProgressOutputBoundaryAdapter? progress,
    CancellationToken cancellationToken);
```

Extend `IStatisticsReloadUseCase` with:

```csharp
Task ExecuteReloadAsync(
    DashboardQueryRequest request,
    IOutputBoundaryAdapter<DashboardStatisticsDto> output,
    IProgressOutputBoundaryAdapter? progress,
    CancellationToken cancellationToken);
```

Implement these in the concrete use cases by adapting `IProgressOutputBoundaryAdapter` to `IProgress<SaveLoadProgress>` and then calling the existing methods. This is an intentional bridge to keep the first slice small.

**Step 5: Run test to verify it passes**

Run:

```powershell
dotnet test .\tests\AtsEmployeeStats.Tests\AtsEmployeeStats.Tests.csproj --filter StatisticsDashboardOutputBoundaryTests
```

Expected: pass.

## Task 2: Add API HTTP and SignalR Presenters

**Files:**
- Create: `src/AtsEmployeeStats.Api/Presentation/HttpResultPresenter.cs`
- Create: `src/AtsEmployeeStats.Api/Presentation/SignalRProgressPresenter.cs`
- Create: `src/AtsEmployeeStats.Api/Presentation/SignalRReloadPresenter.cs`
- Modify: `src/AtsEmployeeStats.Api/Program.cs`
- Test: `tests/AtsEmployeeStats.Tests/StatisticsApiTests.cs`

**Step 1: Write failing API test**

Add or update an API test that calls `/api/statistics` and asserts the endpoint still returns a successful dashboard response after the refactor. If existing tests already cover this, add a focused test for `/api/statistics/reload` to verify the response remains HTTP 200 and includes companies.

Run:

```powershell
dotnet test .\tests\AtsEmployeeStats.Tests\AtsEmployeeStats.Tests.csproj --filter StatisticsApiTests
```

Expected before implementation: compile failure once `Program.cs` is changed to require presenters, or test failure if endpoint still bypasses presenters.

**Step 2: Implement `HttpResultPresenter<T>`**

Add:

```csharp
using AtsEmployeeStats.Application.Statistics.Output;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AtsEmployeeStats.Api.Presentation;

public sealed class HttpResultPresenter<T> : IOutputBoundaryAdapter<T>
{
    public IResult Result { get; private set; } = Results.NoContent();

    public Task PresentAsync(T response, CancellationToken cancellationToken)
    {
        Result = Results.Ok(response);
        return Task.CompletedTask;
    }
}
```

If a not-found response is needed in this slice, add a specialized nullable presenter rather than putting HTTP semantics into Application.

**Step 3: Implement SignalR progress presenter**

Add:

```csharp
using AtsEmployeeStats.Application.Saves;
using AtsEmployeeStats.Application.Statistics.Output;
using AtsEmployeeStats.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace AtsEmployeeStats.Api.Presentation;

public sealed class SignalRProgressPresenter(IHubContext<StatisticsHub> hub)
    : IProgressOutputBoundaryAdapter
{
    public Task PresentProgressAsync(SaveLoadProgress progress, CancellationToken cancellationToken) =>
        hub.Clients.All.SendAsync(
            "LoadingProgress",
            DashboardProgressMapper.ToDashboardProgressDto(progress),
            cancellationToken);
}
```

**Step 4: Implement reload presenter**

Add a presenter that implements `IOutputBoundaryAdapter<DashboardStatisticsDto>` and sends `StatisticsUpdated` through SignalR before setting an HTTP result.

**Step 5: Refactor `Program.cs` dashboard and reload endpoints**

For `/api/statistics`:

```csharp
var presenter = new HttpResultPresenter<DashboardStatisticsDto>();
var progress = new SignalRProgressPresenter(hub);
await useCases.ExecuteDashboardAsync(
    new DashboardQueryRequest(fromDay, toDay, sort),
    presenter,
    progress,
    cancellationToken);
return presenter.Result;
```

For `/api/statistics/reload`, use the reload presenter so SignalR update is emitted by the presenter rather than endpoint glue.

**Step 6: Run API tests**

Run:

```powershell
dotnet test .\tests\AtsEmployeeStats.Tests\AtsEmployeeStats.Tests.csproj --filter StatisticsApiTests
```

Expected: pass.

## Task 3: Add MAUI Dashboard Presenter

**Files:**
- Create: `src/AtsEmployeeStats.Maui/Presentation/MauiDashboardPresenter.cs`
- Create: `src/AtsEmployeeStats.Maui/Presentation/MauiProgressPresenter.cs`
- Modify: `src/AtsEmployeeStats.Maui/MainPage.xaml.cs`
- Test: build verification, plus any extracted non-MAUI presenter tests if the presenter can be kept framework-light.

**Step 1: Extract presentable state if needed**

If `DashboardPageModel` cannot be tested without MAUI runtime types, extract a small state target interface:

```csharp
public interface IDashboardPresentationTarget
{
    void ShowDashboard(DashboardStatisticsDto dashboard);
    void ShowProgress(SaveLoadProgress progress);
}
```

Keep it inside `AtsEmployeeStats.Maui` unless another outer layer needs it.

**Step 2: Implement `MauiDashboardPresenter`**

The presenter implements `IOutputBoundaryAdapter<DashboardStatisticsDto>` and calls the page model state update method. The page model remains the view model, but the presenter owns the mapping from application response to MAUI state.

**Step 3: Implement `MauiProgressPresenter`**

The presenter implements `IProgressOutputBoundaryAdapter` and updates the page model progress properties.

**Step 4: Refactor refresh flow**

Replace:

```csharp
await _ingestUseCase.IngestAsync(CancellationToken.None, progress, force: false);
var dashboard = await _dashboardUseCases.GetDashboardAsync(options, CancellationToken.None, progress);
ApplyDashboard(dashboard);
```

with:

```csharp
var progressPresenter = new MauiProgressPresenter(this);
await _ingestUseCase.IngestAsync(CancellationToken.None, progressPresenter.AsProgress(), force: false);
var dashboardPresenter = new MauiDashboardPresenter(this);
await _dashboardUseCases.ExecuteDashboardAsync(request, dashboardPresenter, progressPresenter, CancellationToken.None);
```

If `IStatisticsIngestUseCase` still only accepts `IProgress<SaveLoadProgress>`, use a temporary adapter and convert ingestion in a later slice.

**Step 5: Build MAUI**

Run:

```powershell
dotnet build .\src\AtsEmployeeStats.Maui\AtsEmployeeStats.Maui.csproj -f net10.0-windows10.0.19041.0
```

Expected: build succeeds with 0 errors.

## Task 4: Remove Endpoint Progress Glue for Converted Flows

**Files:**
- Modify: `src/AtsEmployeeStats.Api/Program.cs`
- Modify: `src/AtsEmployeeStats.Api/SaveIngestionService.cs` if it still manually maps progress
- Test: `tests/AtsEmployeeStats.Tests/StatisticsApiTests.cs`

**Step 1: Delete converted helper code**

Remove `BuildSignalRProgress` from `Program.cs` once all converted endpoints use presenters.

**Step 2: Keep non-converted endpoints stable**

Recommendation and detail endpoints may still use existing return style for this first slice. Do not convert them in this task unless the previous tasks already converted shared helper code cleanly.

**Step 3: Run focused tests**

Run:

```powershell
dotnet test .\tests\AtsEmployeeStats.Tests\AtsEmployeeStats.Tests.csproj --filter "StatisticsApiTests|StatisticsDashboardOutputBoundaryTests"
```

Expected: pass.

## Task 5: Full Verification

**Files:**
- No code edits unless verification exposes failures.

**Step 1: Run full test suite**

Run:

```powershell
dotnet test .\AtsEmployeeStats.sln
```

Expected: deterministic tests pass; opt-in real-save tests remain skipped unless `ATS_EMPLOYEE_STATS_REAL_SAVE_TESTS=1`.

**Step 2: Build MAUI**

Run:

```powershell
dotnet build .\src\AtsEmployeeStats.Maui\AtsEmployeeStats.Maui.csproj -f net10.0-windows10.0.19041.0
```

Expected: pass with 0 errors.

**Step 3: Manual smoke test**

Launch the MAUI app with the VS Code `ATS Employee Stats MAUI (Windows)` launch config. Confirm refresh shows progress, companies show `View`, and selecting a company updates the details pane.

## Follow-Up Slice

After dashboard/reload is stable, convert:
- detail lookup use cases
- recommendation use cases
- ingest use case progress output
- background ingestion service output

Do not convert all use cases in the first slice; the point is to establish the pattern and validate API + MAUI parity with limited blast radius.
