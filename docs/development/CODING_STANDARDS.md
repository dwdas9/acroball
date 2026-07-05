# Coding Standards

These standards are inferred from the current repository and
[../../.editorconfig](../../.editorconfig).

## Formatting

- UTF-8.
- LF line endings.
- Final newline required.
- Trim trailing whitespace.
- 4-space indentation by default.
- 2-space indentation for `.axaml`, XML, project files, props, targets, JSON
  and YAML.

## C# Style

- Use file-scoped namespaces.
- Use explicit accessibility modifiers.
- Use braces for control flow.
- Sort `System` directives first.
- Use `var` when the type is apparent.
- Do not use primary constructors by default.
- Prefer immutable records for data contracts.
- Add XML documentation on public APIs in `src/` projects.
- Keep comments short and useful.

## Naming

- Public types use `PascalCase`.
- Interfaces use the `I` prefix.
- Private fields use `_camelCase`.
- Async methods end with `Async`.
- Request and result records should describe their use case clearly, such as
  `MergeRequest` and `JobExecutionResult`.

## Folder Structure

- Domain: value types, enums, records and domain exceptions.
- Application: abstractions, operation requests, models and job orchestration.
- Infrastructure: concrete persistence, logging, updates and PDF backends.
- UI: Avalonia views, view models, services, converters, themes and navigation.
- Desktop: entry point and composition root.
- SDK: stable plugin contract surface.

## MVVM

- View models derive from `ViewModelBase` or `PageViewModel`.
- Use `[ObservableProperty]` and `[RelayCommand]` for view-model state and
  commands.
- Use `WeakReferenceMessenger` for cross-page navigation messages.
- Keep backend dependencies behind Application abstractions.
- Keep views responsible for Avalonia-only interaction details such as drag
  events and storage provider integration.

## Dependency Injection

- Register infrastructure services in `AddAcroballInfrastructure`.
- Register UI services and view models in `AddAcroballUi`.
- Compose only in `DesktopComposition`.
- Build the provider with validation enabled.
- Avoid reflection-based service discovery.

## Async, Cancellation and Progress

- Long-running PDF work returns `Task`.
- Accept `CancellationToken` on operations that can block or perform I/O.
- Use `IProgress<OperationProgress>` or `IProgress<JobProgress>` for user
  visible work.
- Keep PDF work off the UI thread.
- Use atomic writes for generated output where a partial file would be unsafe.

## Exception Handling

- Surface expected PDF failures through `PdfOperationException` or subtypes.
- Preserve useful user-facing messages.
- Log backend detail through `ILogger`.
- Do not let settings or recent-file persistence failures block startup.

## Logging

- Depend on `Microsoft.Extensions.Logging` abstractions.
- Use structured log message templates.
- Keep the concrete file logger isolated in Infrastructure.
- Treat logging failures as non-fatal.

## Testing

- Use xUnit v3.
- Keep test projects executable.
- Prefer real file tests for persistence and PDF engine behavior.
- Use generated PDF fixtures where possible.
- Use mocks or stubs for UI workflow dependencies.
- Add tests with every feature milestone.

## XAML and Theme

- Use compiled bindings with `x:DataType`.
- Use `DynamicResource` for theme-dependent brushes.
- Keep raw colors in `Theme/Palette.axaml`.
- Keep reusable control styling in `Theme/Controls.axaml`.
- Register view mappings explicitly in `ViewLocator`.
- Keep icon geometry resources in `Theme/Icons.axaml`.

## Documentation

- Update status, roadmap, changelog, implementation history, decision log and
  release documents as part of each milestone.
- Preserve existing documentation history.
- Prefer incremental updates over rewrites.
- Clearly distinguish implemented, planned and deferred work.
