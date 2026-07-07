# ADR-0003: Avalonia 12 on .NET 10, Fluent base theme, explicit ViewLocator

**Status:** Accepted (M1 baseline)

## Context

Avalonia 12.0 went stable in April 2026 with .NET 10 as the recommended
runtime. v12 removed or changed several v11 APIs we would otherwise have
used.

## Decision

- Pin **Avalonia 12.0.x** and **net10.0** across the solution.
- **FluentTheme** as the base, fully reskinned by our own palette/typography/
  control styles (Theme/*.axaml). All colors resolve through semantic
  `Q.Brush.*` resources with Light/Dark ThemeDictionaries.
- **Compiled bindings** (v12 default) everywhere: every view declares
  `x:DataType`.
- **Explicit ViewLocator**: a hand-written type→view table instead of
  name-based reflection, so assembly trimming can never break navigation.
- **No Avalonia.Diagnostics reference** (DevTools became a separate
  commercial product in v12).
- Window chrome: standard decorations on Windows/Linux;
  `ExtendClientAreaToDecorationsHint` on macOS only (the removed
  `ExtendClientAreaChromeHints` API is not used). Fancy custom chrome is
  deferred to M4.

## Consequences

We take v12's breaking changes now, once, rather than migrating mid-project.
Anything targeting removed APIs (DataAnnotations validation plugins, XAML
`CubicBezierEasing`, `TextBox.Watermark`) is avoided from the start.
