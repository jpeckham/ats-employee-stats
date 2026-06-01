# Clean Architecture Maui Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Refactor ATS Employee Stats toward explicit Clean Architecture use cases and add a .NET MAUI desktop UI shell that reuses the existing local SQLite/statistics pipeline.

**Architecture:** Keep Domain pure, keep Application responsible for use-case orchestration and gateway interfaces, keep Infrastructure as SQLite/file gateway implementations, and keep Api/Maui as delivery mechanisms. The first increment preserves the existing medallion pipeline and web behavior while moving dashboard/query selection logic out of Minimal API handlers.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, Blazor WebAssembly client, xUnit, Microsoft.Data.Sqlite, .NET MAUI Windows.

---

### Task 1: Add Dashboard Query Use Cases

**Files:**
- Create: `src/AtsEmployeeStats.Application/Statistics/Queries/DashboardQueryOptions.cs`
- Create: `src/AtsEmployeeStats.Application/Statistics/Queries/IStatisticsDashboardUseCases.cs`
- Create: `src/AtsEmployeeStats.Application/Statistics/Queries/StatisticsDashboardUseCases.cs`
- Test: `tests/AtsEmployeeStats.Tests/StatisticsDashboardUseCasesTests.cs`

**Step 1: Write failing tests**

Add tests proving the use case can load a dashboard, list companies, find a company case-insensitively, find nested entities, find routes by origin/destination, and return null for missing items.

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter StatisticsDashboardUseCasesTests`

Expected: FAIL because the use-case types do not exist.

**Step 3: Implement minimal use cases**

Create an application service that depends on `StatisticsService`, maps through `StatisticsDashboardMapper`, and exposes methods for the query operations currently embedded in `AtsEmployeeStats.Api.Program`.

**Step 4: Run focused tests**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter StatisticsDashboardUseCasesTests`

Expected: PASS.

### Task 2: Thin Minimal API Handlers

**Files:**
- Modify: `src/AtsEmployeeStats.Api/Program.cs`
- Test: `tests/AtsEmployeeStats.Tests/StatisticsApiTests.cs`

**Step 1: Add/adjust endpoint tests**

Ensure existing API endpoint tests still cover company and nested entity lookups.

**Step 2: Refactor handlers**

Register `IStatisticsDashboardUseCases` and replace direct dashboard filtering in endpoint lambdas with use-case calls. Keep SignalR progress creation in the API layer.

**Step 3: Run API tests**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter StatisticsApiTests`

Expected: PASS.

### Task 3: Add MAUI Windows Shell

**Files:**
- Create: `src/AtsEmployeeStats.Maui/AtsEmployeeStats.Maui.csproj`
- Create: `src/AtsEmployeeStats.Maui/MauiProgram.cs`
- Create: `src/AtsEmployeeStats.Maui/App.xaml`
- Create: `src/AtsEmployeeStats.Maui/App.xaml.cs`
- Create: `src/AtsEmployeeStats.Maui/MainPage.xaml`
- Create: `src/AtsEmployeeStats.Maui/MainPage.xaml.cs`
- Modify: `AtsEmployeeStats.sln`

**Step 1: Add project structure**

Create a MAUI project targeting `net10.0-windows10.0.19041.0` and reference Application, Infrastructure, Contracts, and Domain.

**Step 2: Wire local services**

Register `SqliteMedallionSaveSnapshotSource`, `ISaveSnapshotSource`, `IStatisticsIngestor`, `StatisticsService`, and `IStatisticsDashboardUseCases`. Use `FileSystem.AppDataDirectory` for the default SQLite path.

**Step 3: Build a usable first screen**

Add a MAUI page with a refresh command, status text, and a companies list showing name, profit, driver count, garage count, and truck count.

**Step 4: Build MAUI project**

Run: `dotnet build src/AtsEmployeeStats.Maui/AtsEmployeeStats.Maui.csproj -f net10.0-windows10.0.19041.0`

Expected: PASS on Windows with the MAUI Windows workload installed.

### Task 4: Architecture Guard Tests

**Files:**
- Create: `tests/AtsEmployeeStats.Tests/ArchitectureDependencyTests.cs`

**Step 1: Write tests**

Add assembly reference tests proving Domain does not reference Application/Infrastructure/Api/Maui, Application does not reference Infrastructure/Api/Maui, and Infrastructure references Application only inward.

**Step 2: Run tests**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter ArchitectureDependencyTests`

Expected: PASS.

### Task 5: Full Verification

**Files:**
- No production edits unless verification reveals a defect.

**Step 1: Run full tests**

Run: `dotnet test AtsEmployeeStats.sln`

Expected: PASS.

**Step 2: Build solution**

Run: `dotnet build AtsEmployeeStats.sln`

Expected: PASS.

**Step 3: Inspect git diff**

Run: `git status --short` and `git diff --stat`.

Expected: Only intentional plan, application, API, test, and MAUI files changed.
