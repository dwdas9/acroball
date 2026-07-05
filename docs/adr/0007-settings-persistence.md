# ADR-0007: JSON settings via System.Text.Json source generation, atomic writes, per-OS paths

**Status:** Accepted (M1 baseline)

## Context

Settings must survive crashes, work identically on three OSes, respect
platform conventions, and stay trim-safe (ADR-0005).

## Decision

- One `settings.json` (and `recent.json`) under a per-OS data directory:
  `%APPDATA%\Acroball`, `~/Library/Application Support/Acroball`, or
  `$XDG_CONFIG_HOME/Acroball`. The `Acroball_DATA_DIR` environment variable
  overrides everything (portable installs, hermetic tests).
- Serialization through a `JsonSerializerContext` (source-generated,
  reflection-free), enums as strings for hand-editability.
- **Atomic writes**: serialize to `*.tmp`, then `File.Move(overwrite: true)`.
  A crash mid-write can never corrupt the previous good file.
- Unreadable settings fall back to defaults with a logged warning; settings
  can never block startup.

## Consequences

No settings library dependency. Schema evolution is handled by STJ's
missing-member tolerance plus record defaults.

