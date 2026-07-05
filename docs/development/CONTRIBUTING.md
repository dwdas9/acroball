# Contributing

Acroball aims to be a high-quality open-source PDF desktop application. Changes
should preserve the architecture, keep the user experience polished and leave
the repository understandable for the next engineer.

## Before You Start

Read, in order:

- [CURRENT_STATE.md](CURRENT_STATE.md) — the active milestone and the exact
  files it names.
- [../ARCHITECTURE.md](../ARCHITECTURE.md) and [../adr/](../adr/) only if
  `CURRENT_STATE.md` points you there.

See [../prompts/02_Initialize.md](../prompts/02_Initialize.md) for the full
session-start protocol. Do not scan the rest of the repository up front.

Check the current milestone before writing code. Do not begin future milestone
work until it is explicitly in scope.

## Local Validation

Run:

```bash
dotnet restore
dotnet build
dotnet test
```

For documentation-only changes, still run tests when the documentation states
build or test status.

## Engineering Rules

- Preserve the Clean Architecture dependency direction.
- Do not make UI reference Infrastructure.
- Do not put PDF processing directly in view code.
- Reuse existing services, abstractions and patterns before adding new ones.
- Keep public interfaces stable unless there is a clear architectural reason.
- Add focused tests for behavior changes.
- Keep documentation current with implementation.

## Documentation Rules

- Do not create status, roadmap, changelog, or history documents — git history
  is the record of what happened.
- Record a genuine architectural decision as a new ADR in `docs/adr/`.
- Overwrite [CURRENT_STATE.md](CURRENT_STATE.md) at session end per
  [../prompts/03_Checkpoint.md](../prompts/03_Checkpoint.md); it is disposable,
  not append-only.

## Pull Request Expectations

A complete change should include:

- Summary of behavior changed.
- Files changed.
- Tests run and results.
- Documentation updated.
- Known limitations or follow-up work.
- Developer handover notes for substantial milestones.
