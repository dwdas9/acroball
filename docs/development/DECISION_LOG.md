# Decision Log

This log summarizes major architectural decisions. Formal ADRs remain the
source of record for accepted decisions.

## Clean Layered Architecture

Decision: Use Domain, Application, Infrastructure, UI, Desktop and SDK
assemblies with Desktop as the single composition root.

Reason: Keep core PDF logic testable and prevent UI/backend coupling.

Alternatives Considered: A single desktop project, or direct UI references to
PDF backends.

Consequences: More project and DI structure, but clearer ownership and easier
testing.

Future Considerations: Review Application-layer filesystem validation before
copying that pattern into more jobs.

Reference: [../adr/0001-clean-layered-architecture.md](../adr/0001-clean-layered-architecture.md)

## Avalonia 12 and .NET 10

Decision: Build on Avalonia 12 and `net10.0`.

Reason: Use the current cross-platform desktop stack and avoid a mid-project
framework migration.

Alternatives Considered: Older Avalonia versions or platform-specific UI.

Consequences: The project accepts Avalonia 12 API changes and uses explicit
trim-safe view location.

Future Considerations: Revisit custom window chrome and accessibility during
the planned M4/M5 work.

Reference: [../adr/0003-avalonia-12.md](../adr/0003-avalonia-12.md)

## CommunityToolkit.Mvvm

Decision: Use CommunityToolkit.Mvvm for observable state, commands and weak
messaging.

Reason: Reduce boilerplate while keeping view models testable.

Alternatives Considered: Hand-written MVVM infrastructure or a larger MVVM
framework.

Consequences: UI suppresses CS1591 for generated public members; view models
stay in the UI assembly.

Future Considerations: Extract a presentation assembly only if enforcement of
view-model purity becomes necessary.

Reference: [../adr/0004-mvvm-toolkit.md](../adr/0004-mvvm-toolkit.md)

## PDF Backends

Decision: Use PDFsharp for manipulation and reserve PDFium/PDFtoImage for
rendering behind separate abstractions.

Reason: No single permissively licensed .NET library covers both manipulation
and rendering well.

Alternatives Considered: iText, Ghostscript and QuestPDF.

Consequences: Two backend stacks are planned. PDFsharp is currently shipped;
PDFium rendering is still pending.

Future Considerations: Keep rendering serialized because PDFium native access
is not thread-safe.

Reference: [../adr/0002-pdf-backends.md](../adr/0002-pdf-backends.md)

## JSON Persistence

Decision: Persist settings and recent files with System.Text.Json source
generation and atomic writes.

Reason: Keep persistence small, trim-safe and resilient to crashes.

Alternatives Considered: A settings library or ad hoc serialization.

Consequences: The project owns schema evolution and persistence error handling.

Future Considerations: Continue using missing-member tolerant records for
settings evolution.

Reference: [../adr/0007-settings-persistence.md](../adr/0007-settings-persistence.md)

## In-House File Logger

Decision: Use a small `ILoggerProvider` writing rolling local log files.

Reason: Current logging needs are plain diagnostic text with no remote sinks.

Alternatives Considered: Serilog.

Consequences: Fewer dependencies, but no structured logging features.

Future Considerations: Replace with Serilog if structured sinks become useful;
the rest of the code depends only on logging abstractions.

Reference: [../adr/0006-logging.md](../adr/0006-logging.md)

## Plugin Model and Packaging

Decision: Plan plugins through collectible `AssemblyLoadContext`; defer
Velopack packaging and self-update to M6.

Reason: Plugins require runtime assembly loading, and packaging should not
enter the codebase until application workflows are mature.

Alternatives Considered: NativeAOT and hand-built installers.

Consequences: NativeAOT is excluded; self-contained trimmed ReadyToRun
publishes remain the packaging direction.

Future Considerations: Keep SDK dependencies minimal and stable.

References:

- [../adr/0005-plugins-and-trimming.md](../adr/0005-plugins-and-trimming.md)
- [../adr/0008-updates-velopack.md](../adr/0008-updates-velopack.md)

## Shared Job Execution

Decision: Route the merge workflow through `IJobExecutor`, `JobRunner`,
`MergeJobRequest` and `MergeJob`.

Reason: Centralize validation, logging, progress, cancellation, timing and
error translation for long-running user workflows.

Alternatives Considered: No explicit alternative is recorded. The obvious
alternative is direct ViewModel-to-engine execution.

Consequences: Workflows get consistent behavior, but Application now includes
job orchestration and some filesystem validation.

Future Considerations: If more tools follow this model, define clear rules for
which validation belongs in Application versus Infrastructure.

## Temporary Session Documentation

Decision: Maintain `DEVELOPMENT_SESSION.md` separately from
`IMPLEMENTATION_HISTORY.md`.

Reason: Active work, blockers and immediate handover notes are temporary and
should not pollute the permanent engineering journal.

Alternatives Considered: Recording active session notes directly in
implementation history.

Consequences: Milestone closure must fold durable information into release
notes, implementation history and decision log, then refresh the session file.

Future Considerations: Keep the session document short and current.
