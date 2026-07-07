# ADR-0004: CommunityToolkit.Mvvm with partial properties; view models live in Acroball.UI

**Status:** Accepted (M1 baseline)

## Context

We want observable state and commands without boilerplate, and a testing
story for view models.

## Decision

- **CommunityToolkit.Mvvm 8.4** source generators: `[ObservableProperty]` on
  C# 13 partial properties, `[RelayCommand]` for commands,
  `WeakReferenceMessenger` for cross-page navigation messages.
- View models live in **Acroball.UI** (not a separate assembly). They depend
  only on Application abstractions, so they remain unit-testable; the
  UI-assembly placement is a pragmatic tradeoff to avoid a sixth project.
- Headless Avalonia UI tests (Avalonia.Headless.XUnit) join in **M2**, once
  the first real tool page exists to justify the harness.

## Consequences

`NoWarn CS1591` in Acroball.UI only (generated members carry no XML docs). If
view-model purity ever needs enforcement, extraction to a Acroball.Presentation
assembly is mechanical.

