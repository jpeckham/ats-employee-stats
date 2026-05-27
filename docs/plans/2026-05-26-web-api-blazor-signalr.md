# Web API Blazor SignalR Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the Terminal.Gui console app with a minimal ASP.NET Core API, hosted Blazor WebAssembly frontend, and SignalR live updates.

**Architecture:** Preserve `Domain`, `Application`, and `Infrastructure`; add `AtsEmployeeStats.Api`, `AtsEmployeeStats.Web`, and a small shared contracts project. Remove the console project from the solution and replace console tests with API, contract-mapping, and UI navigation tests.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, SignalR, Blazor WebAssembly, xUnit.

---

### Task 1: Shared Contracts

**Files:**
- Create: `src/AtsEmployeeStats.Contracts/AtsEmployeeStats.Contracts.csproj`
- Create: `src/AtsEmployeeStats.Contracts/StatisticsDtos.cs`
- Modify: `AtsEmployeeStats.sln`
- Test: `tests/AtsEmployeeStats.Tests/StatisticsDashboardDtoTests.cs`

**Steps:**
1. Write a failing test that expects dashboard DTOs for companies, garages, drivers, jobs, config, and live status messages.
2. Run `dotnet test --filter StatisticsDashboardDtoTests` and verify it fails because contracts do not exist.
3. Create the contracts project and DTO records.
4. Add the project to the solution.
5. Re-run the targeted test.

### Task 2: Application Dashboard Mapping

**Files:**
- Create: `src/AtsEmployeeStats.Application/Statistics/StatisticsDashboardMapper.cs`
- Modify: `src/AtsEmployeeStats.Application/AtsEmployeeStats.Application.csproj`
- Test: `tests/AtsEmployeeStats.Tests/StatisticsDashboardMapperTests.cs`

**Steps:**
1. Write a failing test that maps sample `AtsStatistics` into dashboard DTOs.
2. Run the targeted mapper test and verify it fails because the mapper does not exist.
3. Add a contracts project reference to Application.
4. Implement the mapper with no API or UI dependency.
5. Re-run the targeted test.

### Task 3: Minimal API And SignalR

**Files:**
- Create: `src/AtsEmployeeStats.Api/AtsEmployeeStats.Api.csproj`
- Create: `src/AtsEmployeeStats.Api/Program.cs`
- Create: `src/AtsEmployeeStats.Api/StatisticsHub.cs`
- Create: `src/AtsEmployeeStats.Api/StatisticsApiOptions.cs`
- Modify: `AtsEmployeeStats.sln`
- Test: `tests/AtsEmployeeStats.Tests/StatisticsApiTests.cs`

**Steps:**
1. Write failing endpoint tests for `GET /api/config`, `GET /api/statistics`, and `POST /api/statistics/reload`.
2. Run targeted API tests and verify they fail because the API project does not exist.
3. Add the API project with references to Application, Infrastructure, Domain, Contracts.
4. Implement minimal endpoints and `/hubs/statistics`.
5. Re-run API tests.

### Task 4: Blazor WebAssembly UI

**Files:**
- Create: `src/AtsEmployeeStats.Web/AtsEmployeeStats.Web.csproj`
- Create: `src/AtsEmployeeStats.Web/Program.cs`
- Create: `src/AtsEmployeeStats.Web/App.razor`
- Create: `src/AtsEmployeeStats.Web/Pages/Dashboard.razor`
- Create: `src/AtsEmployeeStats.Web/Services/StatisticsClient.cs`
- Create: `src/AtsEmployeeStats.Web/Services/DashboardNavigationState.cs`
- Create: `src/AtsEmployeeStats.Web/wwwroot/index.html`
- Create: `src/AtsEmployeeStats.Web/wwwroot/css/app.css`
- Modify: `AtsEmployeeStats.sln`
- Test: `tests/AtsEmployeeStats.Tests/DashboardNavigationStateTests.cs`

**Steps:**
1. Write failing navigation state tests for company -> garage -> driver -> back breadcrumbs.
2. Run the targeted tests and verify failure.
3. Add the Blazor WASM project and SignalR client package.
4. Implement client services, dashboard page, and restrained data-dense styling.
5. Re-run navigation tests and build.

### Task 5: Remove Console Presentation

**Files:**
- Delete: `src/AtsEmployeeStats.Console/`
- Delete: `tests/AtsEmployeeStats.Tests/TerminalDashboardAppTests.cs`
- Delete or rewrite: `tests/AtsEmployeeStats.Tests/CommandLineOptionsTests.cs`
- Modify: `AtsEmployeeStats.sln`
- Modify: `README.md`

**Steps:**
1. Remove console project and console-specific tests.
2. Remove console project from solution.
3. Update README run instructions to use the API/Web app.
4. Run `dotnet test`.
5. Run the API locally and verify the app starts.
