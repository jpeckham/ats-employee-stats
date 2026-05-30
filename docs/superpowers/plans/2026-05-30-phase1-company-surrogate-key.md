# Phase 1 — Company Surrogate Key Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an integer surrogate primary key (`id`) to `silver_companies` so that Phase 2 can use it as a stable single-column FK in `silver_trailers`.

**Architecture:** `silver_companies` currently uses `company_id TEXT PRIMARY KEY` (e.g. `tgcitw-parnell`). We demote that to a `UNIQUE NOT NULL` candidate key and introduce `id INTEGER PRIMARY KEY` (SQLite ROWID alias — auto-assigned). Because the silver/gold tables are fully deleted and rebuilt on every ingestion, there is no data migration concern — only the DDL needs to change. However, `CREATE TABLE IF NOT EXISTS` will not alter an existing table, so the migration must explicitly drop and recreate `silver_companies` when the `id` column is absent.

**Tech Stack:** C# / .NET 10, SQLite via `Microsoft.Data.Sqlite`, xUnit

---

## Files

| File | Change |
|------|--------|
| `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs` | Update `silver_companies` DDL; add `MigrateCompanySurrogateKeyAsync` migration call |
| `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs` | Add test verifying surrogate key is populated after ingestion |

---

### Task 1: Write the failing test

**Files:**
- Modify: `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs`

- [ ] **Step 1: Add the failing test**

Open `tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs` and add this test after the last existing `[Fact]` before the helper methods (look for a region boundary or the last test that calls `WriteAnalyticSaveAsync`):

```csharp
[Fact]
public async Task StatisticsService_assigns_integer_surrogate_key_to_silver_companies()
{
    await WriteAnalyticSaveAsync();
    var source = new SqliteMedallionSaveSnapshotSource(_root, _dbPath);
    var service = new StatisticsService(source);

    await service.IngestAsync(CancellationToken.None);

    using var connection = OpenTestConnection();
    await connection.OpenAsync();

    var (id, companyId) = await QuerySingleAsync<(long, string)>(
        connection,
        "select id, company_id from silver_companies",
        reader => (reader.GetInt64(0), reader.GetString(1)));

    Assert.True(id > 0);
    Assert.False(string.IsNullOrWhiteSpace(companyId));
}
```

- [ ] **Step 2: Run the test to confirm it fails**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "StatisticsService_assigns_integer_surrogate_key_to_silver_companies" --no-build
```

Expected: **FAIL** — `SqliteException: no such column: id` (or similar schema error).

---

### Task 2: Update the silver_companies DDL

**Files:**
- Modify: `src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs`

The `EnsureSchemaAsync` method (around line 320) contains the `CREATE TABLE IF NOT EXISTS silver_companies` statement. We need to:

1. Change the DDL to the new schema.
2. Add a migration that drops + recreates the table when the `id` column is absent (handles existing databases).

- [ ] **Step 1: Update the `silver_companies` CREATE TABLE statement**

Find this block in `EnsureSchemaAsync`:

```csharp
            create table if not exists silver_companies (
                company_id text primary key,
                display_name text not null,
                last_updated_utc text not null
            );
```

Replace it with:

```csharp
            create table if not exists silver_companies (
                id integer primary key,
                company_id text not null unique,
                display_name text not null,
                last_updated_utc text not null
            );
```

- [ ] **Step 2: Add `MigrateCompanySurrogateKeyAsync` helper**

Find the `EnsureColumnAsync` private static method (search for `private static async Task EnsureColumnAsync`). Add the following new method directly above or below it:

```csharp
private static async Task MigrateCompanySurrogateKeyAsync(
    SqliteConnection connection,
    CancellationToken cancellationToken)
{
    await using var check = connection.CreateCommand();
    check.CommandText = "pragma table_info(silver_companies)";
    await using var reader = await check.ExecuteReaderAsync(cancellationToken);
    var hasIdColumn = false;
    while (await reader.ReadAsync(cancellationToken))
    {
        if (string.Equals(reader.GetString(1), "id", StringComparison.OrdinalIgnoreCase))
        {
            hasIdColumn = true;
            break;
        }
    }

    if (hasIdColumn)
    {
        return;
    }

    await ExecuteAsync(connection, "drop table if exists silver_companies", cancellationToken);
    await ExecuteAsync(
        connection,
        """
        create table silver_companies (
            id integer primary key,
            company_id text not null unique,
            display_name text not null,
            last_updated_utc text not null
        )
        """,
        cancellationToken);
}
```

- [ ] **Step 3: Call the migration from `EnsureSchemaAsync`**

At the end of `EnsureSchemaAsync`, after the existing `EnsureColumnAsync` calls (around line 643–654), add:

```csharp
await MigrateCompanySurrogateKeyAsync(connection, cancellationToken);
```

- [ ] **Step 4: Run the failing test — it should now pass**

```
dotnet test tests/AtsEmployeeStats.Tests --filter "StatisticsService_assigns_integer_surrogate_key_to_silver_companies" --no-build
```

Expected: **PASS**

- [ ] **Step 5: Run the full test suite to confirm no regressions**

```
dotnet test tests/AtsEmployeeStats.Tests --no-build
```

Expected: all tests **PASS**

- [ ] **Step 6: Commit**

```
git add src/AtsEmployeeStats.Infrastructure/Saves/SqliteMedallionSaveSnapshotSource.cs
git add tests/AtsEmployeeStats.Tests/SqliteMedallionSaveSnapshotSourceTests.cs
git commit -m "feat: add integer surrogate PK to silver_companies (phase 1 of trailer natural key)"
```

---

## Self-Review Notes

- The `INSERT INTO silver_companies` statement does **not** need to change — SQLite auto-assigns the ROWID when `id` is omitted from the column list.
- The `SELECT` on `silver_companies` does not exist in the current read path (`ReadGoldStatisticsAsync` reads from `gold_company_summary` instead) — so no read queries need updating.
- The migration guard (`pragma table_info`) means the first `EnsureSchemaAsync` call on an existing DB drops and recreates the table; subsequent calls find `id` already present and skip. Since silver data is always wiped and rebuilt on every `IngestAsync`, the drop is safe.
- Phase 2 will add `company_pk INTEGER REFERENCES silver_companies(id)` to `silver_trailers` and will look up the surrogate via `SELECT id FROM silver_companies WHERE company_id = $company_id` inside the insert loop.
