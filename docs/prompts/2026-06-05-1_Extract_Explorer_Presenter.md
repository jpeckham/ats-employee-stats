# Extract Explorer Presenter

Refactor the WPF explorer/navigation behavior out of `MainWindowPresenter` into a dedicated presenter or mapper so it is independently unit-testable and aligned with Clean Architecture.

Current context:

- `src/AtsEmployeeStats.Wpf/Controllers/MainWindowPresenter.cs` currently owns explorer tree construction, node expansion, selection handling, save-location grouping, and detail ViewModel creation.
- `src/AtsEmployeeStats.Wpf/ViewModels` should remain passive bound output state.
- Existing ArchUnitNET rules enforce that public ViewModels do not depend on Application, Infrastructure, or Domain.

Goal:

Create an `ExplorerPresenter`, `ExplorerTreeBuilder`, or similarly named WPF outer-ring adapter that owns:

- Building the `CompanyExplorerViewModel` tree.
- Grouping companies under game/save-location explorer nodes.
- Matching and expanding explorer paths.
- Selecting companies/entities by `ExplorerNodeViewModel`.
- Creating the appropriate `EntityDetailViewModel` for a selected node.

Constraints:

- Keep behavior unchanged.
- Keep Application, Infrastructure, and Domain free of WPF dependencies.
- Do not make ViewModels call use cases.
- Do not move WPF-specific types into Application.
- Add focused tests for tree construction and node selection before moving the logic.

Verification:

- Run `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter ArchitectureDependencyTests -c Debug`.
- Run WPF presenter/navigation-focused tests.
- Run the full test project after the refactor.

