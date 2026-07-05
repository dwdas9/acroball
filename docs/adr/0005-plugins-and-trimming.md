# ADR-0005: Plugins via collectible AssemblyLoadContext; therefore no NativeAOT

**Status:** Accepted (M1 baseline; implementation M5)

## Context

Extensibility is a headline feature. Plugin loading reAcroballs runtime assembly
loading, which NativeAOT forbids.

## Decision

- Plugins implement `IAcroballPlugin` from **Acroball.Sdk**, a tiny contract
  assembly whose dependency list stays frozen.
- Discovery from a plugins directory; each plugin loads into an isolated,
  collectible `AssemblyLoadContext`.
- Publishing therefore targets **self-contained + IL trimming + ReadyToRun**,
  explicitly *not* NativeAOT.
- Trim-safety is designed in from M1: source-generated JSON, compiled
  bindings, explicit ViewLocator, no reflection-based service discovery.

## Consequences

Startup is R2R-fast rather than AOT-instant; binary size is moderate. In
exchange the plugin model is the standard, well-documented .NET approach.

