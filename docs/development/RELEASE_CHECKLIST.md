# Release Checklist

Use this checklist before declaring a milestone or release complete.

## Scope

- Milestone objective is written down.
- Implemented work matches the stated milestone.
- Future milestone work has not been mixed in.
- Public interface changes are intentional and documented.

## Build and Tests

- `dotnet restore` succeeds.
- `dotnet build --configuration Release --no-restore` succeeds.
- `dotnet test --configuration Release --no-build` succeeds.
- New behavior has appropriate tests.
- Manual verification is documented when automated coverage is not practical.

## Architecture

- Dependency direction remains valid.
- UI does not depend on Infrastructure.
- PDF processing goes through Application abstractions.
- DI registration remains centralized by layer.
- New persistence uses atomic writes when applicable.
- Logging uses `ILogger` abstractions.

## User Experience

- Workflow text is clear and implemented.
- Progress and cancellation exist for long operations.
- Errors are user-presentable.
- Keyboard navigation and focus behavior have been reviewed.
- Theme behavior works in Light, Dark and System modes.

## Documentation

- `ARCHITECTURE.md` milestones table reflects what actually shipped.
- New architectural decisions are recorded as ADRs in `docs/adr/`.
- A one-paragraph entry is added to `docs/releases/RELEASES.md`.
- `CURRENT_STATE.md` is overwritten per
  [../prompts/03_Checkpoint.md](../prompts/03_Checkpoint.md).
