# Add Background Runner Abstraction

Replace direct `Task.Run` calls in WPF presenters with a small abstraction so presenter behavior can be unit-tested without relying on real thread scheduling.

Current context:

- WPF presenter code currently uses `Task.Run` to keep file discovery, source validation, save catalog loading, and dashboard reload work off the dispatcher.
- Direct `Task.Run` makes presenter tests harder because behavior is coupled to threading.

Goal:

Introduce an outer-ring abstraction such as:

```csharp
public interface IBackgroundRunner
{
    Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken = default);
    Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default);
}
```

or an equivalent shape that fits the codebase.

Use it in WPF presenters instead of direct `Task.Run`.

Constraints:

- Keep the abstraction in the WPF outer layer unless another delivery layer needs it.
- Do not put WPF threading concerns into Application.
- Preserve current non-blocking startup/reload behavior.
- Add a test-friendly implementation for unit tests.
- Keep production implementation simple and explicit.

Verification:

- Add tests proving presenter methods use the runner boundary rather than direct blocking calls where practical.
- Run `dotnet test tests/AtsEmployeeStats.Tests/AtsEmployeeStats.Tests.csproj --filter ArchitectureDependencyTests -c Debug`.
- Run WPF presenter tests.
- Run the full test project.

