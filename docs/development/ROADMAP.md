# Roadmap

Last updated: 2026-07-06

This roadmap reflects the repository as implemented, not aspirational feature
text from earlier prompts.

## Completed

### M1 - Application Shell

- .NET 10 solution structure.
- Clean Architecture project layout.
- Avalonia 12 desktop shell.
- Home page and sidebar navigation.
- Tool catalog and placeholder pages.
- Light, Dark and System themes.
- Settings persistence.
- Rolling file logging.
- CI workflow.
- Baseline test projects.

### M2 - PDF Manipulation Engine

- `IPdfEngine` contract.
- PDFsharp-backed inspection and manipulation engine.
- PDF inspect and page geometry.
- Merge, split, extract, rotate and metadata update engine operations.
- Atomic output writes.
- Operation progress and cancellation.
- PDF engine integration tests.

### M3 - Merge Workflow

- Shared job execution framework.
- Merge job request and merge job orchestration.
- Merge UI page.
- File picking and drag/drop add.
- Reordering through buttons and drag/drop.
- Output folder and file-name handling.
- Progress, cancellation and result state.
- Merge ViewModel tests.

## In Progress

### Documentation Foundation

- Development documentation.
- Project status.
- Roadmap.
- Changelog.
- Implementation history.
- Decision log.
- Coding standards.
- Release documents.
- User guide and FAQ.
- Temporary development session handover.

## Planned

### Basic Tool UI Completion

- Split tool page.
- Extract tool page.
- Rotate tool page.
- Shared user-facing patterns for file selection, output naming, progress,
  cancellation and result state.

### Rendering and Organization

- PDFium/PDFtoImage implementation behind `IPdfRenderService`.
- Thumbnail pipeline.
- Visual Organize tool.
- Compose support in `IPdfEngine`.

### M4 Feature Package

- Compress workflow.
- Protect workflow for encrypt/decrypt and permissions.
- Metadata editor workflow.
- Command palette.
- Custom window chrome.

### M5 Extension and Accessibility

- Plugin loading through collectible `AssemblyLoadContext`.
- Plugin manager UI.
- SDK contribution points.
- Accessibility pass.

### M6 Packaging and Updates

- Velopack installers.
- Self-update integration.
- Platform release process.
- Code signing plan.

## Deferred

- NativeAOT is excluded by ADR-0005 because plugins require runtime assembly
  loading.
- Velopack dependency is deferred until M6.
- PDF rendering is deferred until the rendering/organize milestone.
- Avalonia Headless UI tests are not yet present.
