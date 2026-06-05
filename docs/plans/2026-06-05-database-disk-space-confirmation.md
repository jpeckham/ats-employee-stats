# Database Disk Space Confirmation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a Finish confirmation that estimates Employee Database disk usage from selected ATS/ETS2 saves, shows free space, and blocks saving when there is not enough disk space.

**Architecture:** Add a small WPF-facing disk estimate service plus confirmation boundary. `GameSourcePresenter` computes the estimate from selected wizard roots before calling `SaveValidatedAsync`; application validation and persistence remain unchanged.

**Tech Stack:** C#/.NET, WPF, xUnit, CommunityToolkit.Mvvm.

---

### Task 1: Add Database Size Estimator

**Files:**
- Create: `src/AtsEmployeeStats.Wpf/Services/IDatabaseDiskSpaceService.cs`
- Create: `src/AtsEmployeeStats.Wpf/Services/LocalDatabaseDiskSpaceService.cs`
- Test: `tests/AtsEmployeeStats.Tests/GameSourcePresenterTests.cs`

**Step 1: Write failing tests**

Add tests that use a fake disk service to verify:

- selected save roots are passed to the disk service
- required bytes use the service result
- insufficient free space blocks saving

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter GameSourcePresenterTests`

Expected: FAIL because the presenter does not accept or call a disk service.

**Step 3: Implement minimal service**

Add:

```csharp
public sealed record DatabaseDiskSpaceEstimate(
    long SelectedSaveBytes,
    long RequiredDatabaseBytes,
    long FreeBytes,
    bool HasEnoughSpace);

public interface IDatabaseDiskSpaceService
{
    DatabaseDiskSpaceEstimate Estimate(IReadOnlyList<string> saveRoots);
}
```

Local implementation:

- enumerate `game.sii` recursively under roots
- skip paths with a segment ending in `.bak`
- sum file lengths
- calculate `Ceiling(saveBytes * 2.1)`
- read free space from the database directory drive

**Step 4: Run tests to verify pass**

Run the same filtered test command.

### Task 2: Add Confirmation Boundary

**Files:**
- Create: `src/AtsEmployeeStats.Wpf/Services/ISourceWizardConfirmation.cs`
- Create: `src/AtsEmployeeStats.Wpf/Services/SourceWizardConfirmation.cs`
- Modify: `src/AtsEmployeeStats.Wpf/Controllers/GameSourcePresenter.cs`
- Modify: `src/AtsEmployeeStats.Wpf/App.xaml.cs`
- Test: `tests/AtsEmployeeStats.Tests/GameSourcePresenterTests.cs`

**Step 1: Write failing tests**

Add tests that:

- return false from the fake confirmation and assert no settings were saved
- return true and assert settings were saved
- assert no confirmation is shown when space is insufficient

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter GameSourcePresenterTests`

Expected: FAIL because no confirmation boundary exists.

**Step 3: Implement minimal confirmation**

Add:

```csharp
public interface ISourceWizardConfirmation
{
    bool ConfirmDatabaseBuild(DatabaseDiskSpaceEstimate estimate);
}
```

WPF implementation uses `MessageBox.Show` with estimated required space and free
space in human-readable units.

**Step 4: Wire dependencies**

Register the local disk service and confirmation in WPF startup where presenters
are created.

**Step 5: Run tests to verify pass**

Run the filtered tests.

### Task 3: Verify Full Test Suite

**Files:**
- No additional files expected.

**Step 1: Run all tests**

Run: `dotnet test AtsEmployeeStats.sln`

Expected: PASS.

**Step 2: Inspect git diff**

Run: `git diff --stat` and `git diff --check`.

Expected: scoped changes and no whitespace errors.
