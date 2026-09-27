# ADR-0015: 1.0 scope and release criteria

**Status:** Accepted (supersedes the "1.0" line of the original milestone
plan; defers parts of ADR-0005 and ADR-0008)

## Context

The original plan (`ARCHITECTURE.md`) put 1.0 at the end of a milestone
that bundled packaging, self-update, code signing and plugins. Later work
reused milestone labels for different features (Viewer, Annotations, Fill
Form), so no document said what "finished" meant and every completed
milestone ended with "ask what's next". This ADR is that missing finish line.

## Decision

**Acroball 1.0 is the offline, cross-platform PDF toolkit as it now stands.**

In scope, and required for 1.0:

- The ten built-in tools: Viewer (with bookmarks, annotations), Merge,
  Split, Organize, Rotate, Extract, Compress, Protect, Metadata, Fill Form.
- The shell: home screen, sidebar navigation, `Ctrl+1`…`9`, Light/Dark/System
  theme, persisted window state, settings, recent files, file logging.
- Self-contained executables for `win-x64`, `osx-x64`, `osx-arm64`,
  `linux-x64`, `linux-arm64`, produced by the `App Release` workflow.
  Windows ships as a folder (launcher plus DLLs), not a single-file bundle:
  the unsigned single-file `Acroball.exe` was deleted on write by an
  antivirus product as a false positive, while the folder build was not.
  Archives exclude `.pdb`/`.xml` files and include the license notices.
- No network calls of any kind.

Explicitly deferred past 1.0 (not blockers):

- **Self-update / installers via Velopack** (ADR-0008). `NullUpdateService`
  stays registered; releases are downloaded manually.
- **Plugin loading** (ADR-0005). `Acroball.Sdk` stays as the contract; the
  no-NativeAOT publishing constraint still applies.
- **Code signing.** Needs purchased certificates, not code. Unsigned builds
  trigger SmartScreen/Gatekeeper warnings on first run.
- **Command palette, custom window chrome, formal accessibility audit.**

Release criteria — 1.0 ships when all hold:

1. `dotnet build -c Release` has 0 warnings and 0 errors; `dotnet test`
   passes in full on CI (Windows, macOS, Linux).
2. Every tool in `ToolCatalog` maps to a real page (none falls through to
   `ToolPlaceholderViewModel`).
3. A self-contained `win-x64` publish launches and shows its main window.
4. `Directory.Build.props` `<Version>` is `1.0.0` and the `app-v1.0.0` tag
   produces a GitHub release with all five platform archives.

## Consequences

Work after 1.0 is new scope and is chosen deliberately as 1.x, starting from
the deferred list above. Sessions can measure "done" against this ADR
instead of asking.
