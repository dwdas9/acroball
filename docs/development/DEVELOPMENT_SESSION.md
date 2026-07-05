# Development Session

Last updated: 2026-07-06

This document is temporary. It records current work in progress, active
decisions, blockers and immediate next steps. At the end of a milestone,
durable information should be folded into release notes, implementation
history, the decision log and project status, then this file should be
refreshed for the next milestone.

## Active Work

Documentation Foundation.

## Current Objective

Leave the repository in a state where another senior software engineer can
understand Acroball without external context.

## Active Decisions

- Preserve existing documentation and ADR history.
- Create missing `docs/development/`, `docs/releases/`, `docs/user/` and
  `docs/architecture/` structure.
- Treat M3 as the Merge Workflow because `src/M3_Prompt.md`, current code and
  tests all point to that scope.
- Record visual organizer/PDF rendering as planned work, not completed work.
- Do not modify production code during this milestone.

## Current Verification

`dotnet test --no-restore` passed 75 tests on 2026-07-06.

The first sandboxed attempt failed with access denied while writing generated
`obj/` files. An unsandboxed run completed successfully.

## Blockers

None for documentation foundation.

## Open Questions

- Should the next feature milestone complete Split, Extract and Rotate UI
  before starting the historical M4 Compress/Protect/Metadata package?
- Should old `Quire` names in source file names be corrected in a future
  cleanup milestone?
- Should the project remove tracked empty `error.txt` and `output.txt` files?

## Immediate Next Steps

- Keep roadmap and project status aligned with implementation.
- Clarify the next feature milestone before starting production code work.
- Add rendered UI smoke tests when the project introduces Avalonia Headless.
