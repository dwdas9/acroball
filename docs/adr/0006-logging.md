# ADR-0006: In-house ~150-line file logger instead of Serilog

**Status:** Accepted (M1 baseline)

## Context

Acroball needs plain diagnostic text logs on disk: one file per day, a couple of
weeks of retention. Nothing structured, nothing shipped anywhere.

## Decision

`FileLoggerProvider` (Acroball.Infrastructure.Logging): an
`ILoggerProvider` writing `Acroball-yyyyMMdd.log` through an unbounded channel
with a single background consumer, 14-day retention enforced at startup.
Level filtering stays in `ILoggerFactory` configuration. All code logs
against `Microsoft.Extensions.Logging.Abstractions` only.

## Consequences

One fewer third-party dependency, aligned with the project's self-contained
ethos. If needs ever outgrow this (structured logs, sinks), swapping in
Serilog is a one-line provider change because nothing depends on the
concrete logger.

