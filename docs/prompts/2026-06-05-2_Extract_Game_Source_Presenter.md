# Extract Game Source Presenter

Refactor WPF game source discovery and setup wizard behavior out of `MainWindowPresenter` into a dedicated WPF presenter/controller.

Current context:

- `MainWindowPresenter` currently loads game source configuration, discovers ATS/ETS2 candidates, manages the source wizard, saves validated source configuration, and loads save game rows.
- Bound source/wizard ViewModels in `AtsEmployeeStats.Wpf.ViewModels` should remain passive output state.
- Mapping between Application save models and WPF ViewModels should happen in the outer-ring presenter.

Goal:

Create a `GameSourcePresenter` or `GameSourceController` that owns:

- Loading configured game sources.
- Loading discovered save games.
- Starting and navigating the source wizard.
- Mapping `GameSourceConfiguration`, `GameSourceCandidates`, and `SaveGame` into primitive/display-only WPF ViewModels.
- Mapping selected wizard/source ViewModels back into Application input data.
- Persisting validated game source selections through the existing Application use case.

Constraints:

- Keep Application save types out of public WPF ViewModels.
- Keep source/wizard ViewModels as bound state only.
- Preserve existing wizard behavior and text bindings.
- Avoid introducing new infrastructure dependencies outside the WPF composition/presenter layer.
- Add tests that prove mapping and wizard state changes without requiring real file-system discovery.

Verification:

- Run `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter ArchitectureDependencyTests -c Debug`.
- Run WPF source-management tests.
- Run the full test project.

