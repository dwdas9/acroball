# ADR-0001: Clean layered architecture with a single composition root

**Status:** Accepted (M1 baseline)

## Context

Acroball will grow tools, backends and a plugin system over six milestones. We
need dependency rules that hold up under that growth and keep core logic
testable off any particular machine.

## Decision

Five assemblies with strictly inward-pointing dependencies:

- **Acroball.Domain** â€” value types and rules (page ranges, rotation, metadata,
  permissions). Zero dependencies.
- **Acroball.Application** â€” use-case contracts (`IPdfEngine`,
  `IPdfRenderService`, settings/recent/theme/update services) and request
  records. Depends only on Domain.
- **Acroball.Infrastructure** â€” persistence, logging, updates and (from M2) PDF
  backends. Depends on Application.
- **Acroball.UI** â€” Avalonia views, view models, theme. Depends on Application;
  never on Infrastructure.
- **Acroball.Desktop** â€” entry point and the *only* composition root; the only
  assembly that references both UI and Infrastructure.

**Acroball.Sdk** sits outside the stack: a frozen contract assembly for plugins.

## Consequences

Domain and Application build and test anywhere (including environments with
no Avalonia or native PDF libraries). Swapping a PDF backend touches only
Infrastructure and one registration line. The cost is some ceremony (request
records, DI extensions per layer), accepted deliberately.

