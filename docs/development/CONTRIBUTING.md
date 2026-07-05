# Contributing

Acroball aims to be a high-quality open-source PDF desktop application. Changes
should preserve the architecture, keep the user experience polished and leave
the repository understandable for the next engineer.

## Before You Start

Read:

- [DEVELOPMENT.md](DEVELOPMENT.md)
- [PROJECT_STATUS.md](PROJECT_STATUS.md)
- [ROADMAP.md](ROADMAP.md)
- [../ARCHITECTURE.md](../ARCHITECTURE.md)
- [../adr/](../adr/)

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

- Do not replace existing documents just because a different style is
  preferred.
- Extend or amend existing documents when history matters.
- Record durable changes in `IMPLEMENTATION_HISTORY.md`.
- Record major architectural decisions in `DECISION_LOG.md` and, when needed,
  a formal ADR.
- Use `DEVELOPMENT_SESSION.md` only for active temporary work.

## Pull Request Expectations

A complete change should include:

- Summary of behavior changed.
- Files changed.
- Tests run and results.
- Documentation updated.
- Known limitations or follow-up work.
- Developer handover notes for substantial milestones.
