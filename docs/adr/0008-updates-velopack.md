# ADR-0008: Velopack for installers and self-update, deferred to M6

**Status:** Accepted direction (implementation M6)

## Context

Cross-platform installers plus delta self-updates are a solved problem;
building them by hand is not a good use of this project.

## Decision

Velopack (MIT) will provide Windows/macOS/Linux packages and self-update in
Milestone 6. Until then `IUpdateService` exists in Application with a
`NullUpdateService` registered, so UI written earlier ("Check for updates")
needs no rework, and builds that cannot self-update (distro packages) simply
report `IsSupported = false`.

## Consequences

The Velopack dependency and its startup hook enter the codebase only in M6,
confined to Acroball.Desktop and one Infrastructure service.

