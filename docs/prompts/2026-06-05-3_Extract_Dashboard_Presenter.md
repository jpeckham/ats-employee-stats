# Extract Dashboard Presenter

Refactor dashboard loading, refresh/reload behavior, progress updates, and status state out of `MainWindowPresenter` into a dedicated WPF dashboard presenter.

Current context:

- `MainWindowPresenter` currently calls dashboard/reload use cases, manages busy flags, progress bars, status text, dashboard query options, and selected detail restoration.
- The WPF ViewModels should remain passive bound state.
- Commands should behave like Clean Architecture controllers: collect input state, call Application input boundaries, and pass output into presenter-owned state updates.

Goal:

Create a `DashboardPresenter` or `DashboardController` that owns:

- Startup sync.
- Refresh dashboard.
- Reload saves.
- Progress mapping from `SaveLoadProgress` into WPF progress state.
- Status text and busy-state transitions.
- Dashboard query input such as `ExcludePlayerDriver`.
- Updating selected detail state after reload/refresh.

Constraints:

- Do not let WPF ViewModels depend on Application use cases or Application progress models.
- Keep use-case calls inside WPF controller/presenter types.
- Preserve existing UI behavior.
- Add tests for progress mapping, busy-state transitions, refresh/reload status updates, and selected-tab restoration.

Verification:

- Run `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter ArchitectureDependencyTests -c Debug`.
- Run focused dashboard presenter tests.
- Run the full test project.

