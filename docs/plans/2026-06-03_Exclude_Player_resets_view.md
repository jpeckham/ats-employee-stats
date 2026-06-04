# Fix: "Exclude player character" resets explorer to root

## Problem

Checking or unchecking "Exclude player character" resets the detail panel to the top-level
companies view and loses the current explorer position.

**Trigger path:**
`ExcludePlayerDriver` checkbox → `OnExcludePlayerDriverChanged` → `RefreshAsync()`

**Root cause — two sub-problems in `RefreshAsync`:**

1. `SelectedDetail` is unconditionally reset:
   ```csharp
   SelectedDetail = new CompaniesDetailViewModel(_dashboard.Companies);
   ```
   There is no attempt to remember or restore what was selected before the refresh.

2. `BuildExplorer` tears down and rebuilds the entire node tree:
   ```csharp
   Explorer.Roots.Clear();
   Explorer.Roots.Add(root);
   ```
   All `ExplorerNodeViewModel` instances are new objects, so the WPF `TreeView`'s selected
   item and scroll position are lost even if we tried to preserve them at the view layer.

## Fix

### 1. Track the selected node

Add a field to `MainWindowViewModel` that records the identity of the last selected node:

```csharp
private ExplorerNodeViewModel? _selectedNode;
```

Set it at the end of `SelectExplorerNode` (after the `SelectedDetail` assignment), capturing
`Kind`, `CompanyId`, and `EntityId` — the three coordinates used by `MatchesExplorerNode`.

### 2. Restore selection after rebuild

In `RefreshAsync`, replace the hardcoded reset:

```csharp
// Before
BuildExplorer(_dashboard.Companies);
SelectedDetail = new CompaniesDetailViewModel(_dashboard.Companies);

// After
BuildExplorer(_dashboard.Companies);
if (_selectedNode is not null)
    SelectExplorerNode(_selectedNode);
else
    SelectedDetail = new CompaniesDetailViewModel(_dashboard.Companies);
```

`SelectExplorerNode` already contains all the null-guards and fallback logic needed for the
case where the previously selected entity no longer exists in the refreshed data (e.g., the
player driver node disappears when exclusion is toggled on). In that case `SelectedDetail`
stays unchanged (the `_ => SelectedDetail` default branch), which is acceptable.

`ReloadAsync` has the same pattern and should get the same treatment.

## Limitations / known remaining behaviour

- The TreeView scroll position will still reset to wherever the re-selected node is after a
  rebuild, because WPF `TreeView` does not preserve scroll state across an `ItemsSource`
  change. Fixing that would require virtualising the tree or deferring the `BringIntoView`
  call — out of scope for this fix.
- If the selected entity is genuinely removed by the filter (e.g., a driver tab selected and
  the player driver is then excluded), `SelectedDetail` is left showing stale data from the
  previous query. A follow-up could detect this case and fall back to the parent entity view.

## Files to change

- `src/AtsEmployeeStats.Wpf/ViewModels/MainWindowViewModel.cs`
  - Add `_selectedNode` field
  - Set it in `SelectExplorerNode`
  - Use it in `RefreshAsync` and `ReloadAsync`
