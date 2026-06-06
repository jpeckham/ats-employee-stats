# Streaming Bronze Memory Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Reduce rebuild memory by reading only projection-relevant bronze units and avoiding a full all-units snapshot graph.

**Architecture:** Keep the existing silver/gold schema and dashboard query contract. Add a filtered bronze reader used by rebuild, so `ReadingBronze` hydrates only unit types consumed by `StatisticsProjection`. Preserve chronological/company grouping behavior and existing projection semantics for relevant units.

**Tech Stack:** C#/.NET 10, Microsoft.Data.Sqlite, xUnit.

---

### Task 1: Add Regression Coverage For Filtered Bronze Rebuild

**Files:**
- Modify: `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs`
- Modify: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs`

**Steps:**
1. Add a test that ingests a save containing a relevant unit and an irrelevant high-volume unit type.
2. Force rebuild from bronze.
3. Assert the irrelevant unit type is not read during rebuild using an internal diagnostic hook.
4. Run the targeted test and confirm it fails before implementation.
5. Implement the filtered bronze reader and diagnostic hook.
6. Run the targeted test and full test project.

### Task 2: Stream Projection Groups

**Files:**
- Modify: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs`
- Modify: `src/AtsEmployeeStats.Application/Statistics/StatisticsProjection.cs` only if needed

**Steps:**
1. Replace `ReadAllBronzeSnapshotsAsync` in rebuild with an async stream/grouped projection path.
2. Read bronze metadata ordered by company/source key and last-write time.
3. Hydrate filtered documents one save at a time.
4. Build company statistics per group, then compose the final `AtsStatistics`.
5. Preserve existing `ReadAllAsync` behavior for callers that explicitly request full parsed snapshots.
6. Run tests and build.

### Task 3: Verify Runtime Memory Behavior

**Files:**
- No production file unless verification exposes a gap.

**Steps:**
1. Run the focused medallion tests.
2. Run the full test project.
3. Build the solution.
4. If feasible, run a local rebuild and compare process memory during `ReadingBronze`.
