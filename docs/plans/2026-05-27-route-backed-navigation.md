# Route-Backed Navigation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a route-backed Blazor list/detail navigation flow for trucking companies, garages, drivers, trucks, and jobs.

**Architecture:** Keep the existing `/api/statistics` payload and move selection state into URLs. Extract small frontend helper logic for finding entities, filtering scoped lists, building driver truck history, and rendering shared dashboard shell pieces.

**Tech Stack:** C#/.NET 10, Blazor WebAssembly, xUnit.

---

### Task 1: Route View Model Helpers

**Files:**
- Create: `src/AtsEmployeeStats.Web/Services/DashboardViewModel.cs`
- Create or modify: `tests/AtsEmployeeStats.Tests/DashboardViewModelTests.cs`

**Step 1: Write failing tests**

Add tests that create a `CompanyDto` with one garage, two drivers, three trucks, and missions. Assert:

- `GetGarageDrivers(company, garageId)` returns only drivers assigned to the garage.
- `GetGarageTrucks(company, garageId)` returns only trucks assigned to the garage.
- `GetDriverJobs(company, driverId)` returns only missions assigned to the driver.
- `GetDriverTrucks(company, driverId)` returns trucks from current truck assignment, truck driver assignment, and historical mission truck ids without duplicates.

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter DashboardViewModelTests`

Expected: FAIL because `DashboardViewModel` does not exist.

**Step 3: Write minimal implementation**

Create `DashboardViewModel` as a static class with methods:

- `FindCompany(DashboardStatisticsDto? statistics, string companyId)`
- `FindGarage(CompanyDto company, string garageId)`
- `FindDriver(CompanyDto company, string driverId)`
- `GetGarageDrivers(CompanyDto company, string garageId)`
- `GetGarageTrucks(CompanyDto company, string garageId)`
- `GetDriverJobs(CompanyDto company, string driverId)`
- `GetDriverTrucks(CompanyDto company, string driverId)`

Use `StringComparer.OrdinalIgnoreCase` for id comparisons.

**Step 4: Run test to verify it passes**

Run: `dotnet test --filter DashboardViewModelTests`

Expected: PASS.

### Task 2: Shared Dashboard Loading Shell

**Files:**
- Modify: `src/AtsEmployeeStats.Web/Pages/Dashboard.razor`
- Create: `src/AtsEmployeeStats.Web/Pages/DashboardBase.cs`

**Step 1: Extract shared behavior without changing routes**

Move statistics loading, realtime event handling, range changes, progress formatting, and `Money(long)` into `DashboardBase`.

**Step 2: Run existing tests**

Run: `dotnet test --filter DashboardNavigationStateTests`

Expected: PASS if the old navigation state tests still compile, or remove those tests in Task 3 after routes replace the service.

**Step 3: Build**

Run: `dotnet build`

Expected: PASS.

### Task 3: Company List Route

**Files:**
- Modify: `src/AtsEmployeeStats.Web/Pages/Dashboard.razor`
- Modify: `src/AtsEmployeeStats.Web/wwwroot/css/app.css`

**Step 1: Implement `/` as company list only**

Render only trucking companies with columns `Company`, `Profit`, `Garages`, `Drivers`, `Trucks`, and a `View` button linking to `/companies/{company.Id}`.

**Step 2: Remove obsolete navigation state service**

Stop injecting `DashboardNavigationState` into the dashboard and remove service registration from `src/AtsEmployeeStats.Web/Program.cs` if it is no longer used.

**Step 3: Build**

Run: `dotnet build`

Expected: PASS.

### Task 4: Company Detail Route

**Files:**
- Create: `src/AtsEmployeeStats.Web/Pages/CompanyDetail.razor`
- Modify: `src/AtsEmployeeStats.Web/wwwroot/css/app.css`

**Step 1: Add route**

Create `@page "/companies/{CompanyId}"`.

**Step 2: Render company detail**

Use `DashboardViewModel.FindCompany`. If missing, show an empty state with a link to `/`.

Add breadcrumb links and tab buttons controlled by a query parameter or local component state. Default to `Garages`.

Tabs:

- Garages list with `View` buttons to `/companies/{company.Id}/garages/{garage.Id}`
- Drivers list with `View` buttons to `/companies/{company.Id}/drivers/{driver.Id}`
- Trucks list without deeper truck detail for now

**Step 3: Build**

Run: `dotnet build`

Expected: PASS.

### Task 5: Garage Detail Route

**Files:**
- Create: `src/AtsEmployeeStats.Web/Pages/GarageDetail.razor`
- Modify: `src/AtsEmployeeStats.Web/wwwroot/css/app.css`

**Step 1: Add route**

Create `@page "/companies/{CompanyId}/garages/{GarageId}"`.

**Step 2: Render garage detail**

Find company and garage. If missing, show a scoped empty state.

Add breadcrumb links. Add `Drivers` and `Trucks` tabs.

Drivers list uses `DashboardViewModel.GetGarageDrivers` and links to `/companies/{company.Id}/garages/{garage.Id}/drivers/{driver.Id}`.

Trucks list uses `DashboardViewModel.GetGarageTrucks`.

**Step 3: Build**

Run: `dotnet build`

Expected: PASS.

### Task 6: Driver Detail Routes

**Files:**
- Create: `src/AtsEmployeeStats.Web/Pages/DriverDetail.razor`
- Modify: `src/AtsEmployeeStats.Web/wwwroot/css/app.css`

**Step 1: Add routes**

Create both routes on the same component:

- `@page "/companies/{CompanyId}/drivers/{DriverId}"`
- `@page "/companies/{CompanyId}/garages/{GarageId}/drivers/{DriverId}"`

**Step 2: Render driver detail**

Find company, optional garage, and driver. If missing, show a scoped empty state.

Add breadcrumb links that include the garage when `GarageId` is present.

Add `Jobs` and `Trucks` tabs.

Jobs uses `DashboardViewModel.GetDriverJobs`. Trucks uses `DashboardViewModel.GetDriverTrucks`.

**Step 3: Build**

Run: `dotnet build`

Expected: PASS.

### Task 7: Cleanup And Verification

**Files:**
- Delete if unused: `src/AtsEmployeeStats.Web/Services/DashboardNavigationState.cs`
- Delete or rewrite: `tests/AtsEmployeeStats.Tests/DashboardNavigationStateTests.cs`
- Modify: `src/AtsEmployeeStats.Web/Program.cs`

**Step 1: Remove obsolete state**

Delete the old navigation state service and tests if no longer used.

**Step 2: Run full tests**

Run: `dotnet test`

Expected: PASS.

**Step 3: Run the app**

Run: `dotnet run --project src/AtsEmployeeStats.Api --urls http://localhost:5000`

Expected: app starts and serves the frontend.

**Step 4: Manual smoke test**

Open `http://localhost:5000`, verify:

- Home lists only trucking companies.
- Company `View` opens company detail.
- Company tabs switch among garages, drivers, and trucks.
- Garage `View` opens garage detail.
- Garage driver `View` opens scoped driver detail.
- Browser Back returns to the prior route.
