# Changelog

All notable project changes are recorded here.

## Unreleased

### Added

- Documentation Foundation structure under `docs/development/`,
  `docs/releases/`, `docs/user/` and `docs/architecture/`.
- Current project status, roadmap, implementation history, decision log,
  coding standards, release checklist, user guide and FAQ.
- Temporary `DEVELOPMENT_SESSION.md` handover document.

### Changed

- Existing documentation is being aligned with the actual repository state.

### Fixed

- Documentation gaps around project status, release history and contributor
  handover.

## 0.1.0 - 2026-07-06

### Added

- .NET 10 solution with Domain, Application, Infrastructure, UI, Desktop and
  SDK projects.
- Avalonia 12 desktop shell with home, settings, sidebar navigation and theme
  resources.
- Clean Architecture dependency layout and composition root.
- CommunityToolkit.Mvvm view models and command generation.
- JSON settings persistence with source-generated serialization.
- Recent files service.
- Rolling file logger.
- Null update service.
- PDFsharp manipulation engine.
- PDF inspect, page geometry, merge, split, extract, rotate and metadata
  update engine operations.
- Shared job execution framework.
- Merge workflow UI and ViewModel.
- xUnit v3 test suites for Domain, Application, Infrastructure and UI.
- Architecture overview and ADRs 0001 through 0008.

### Changed

- `README.md` was simplified after the initial commit, leaving status and
  layout placeholders for the Documentation Foundation to restore.

### Fixed

- No fixes are separately recorded in git history.

### Removed

- No removals are separately recorded in git history.

### Deprecated

- No deprecations are recorded.

### Breaking Changes

- No breaking changes are recorded.

### Security

- No security changes are recorded.
