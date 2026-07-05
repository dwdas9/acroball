# Development Guide

Starting point for engineers working on Acroball.

## Prerequisites

- .NET SDK 10.0.301 or a compatible later feature band (pinned in
  [../../global.json](../../global.json)).
- A platform supported by Avalonia Desktop: Windows, macOS or Linux.
- Git.

Package versions are centrally managed in
[../../Directory.Packages.props](../../Directory.Packages.props).

## Common Commands

```bash
dotnet restore
dotnet build
dotnet run --project src/Acroball.Desktop
dotnet test
```

## Where things live

- Architecture, layering, and current milestone shape: [../ARCHITECTURE.md](../ARCHITECTURE.md).
- Why a decision was made: [../adr/](../adr/) (one immutable file per decision).
- Code style and naming: [CODING_STANDARDS.md](CODING_STANDARDS.md).
- What to do right now, and which files that requires: [CURRENT_STATE.md](CURRENT_STATE.md).

Do not duplicate any of the above here. If this guide and `ARCHITECTURE.md`
ever disagree on a fact, `ARCHITECTURE.md` wins.

## Session continuity

This project uses a stateful LLM protocol instead of narrative dev logs — see
[../prompts/01_System_Master.md](../prompts/01_System_Master.md). Do not create
status, roadmap, changelog, or history documents; update
[CURRENT_STATE.md](CURRENT_STATE.md) at the end of a session instead.
