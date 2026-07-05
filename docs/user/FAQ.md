# FAQ

## Is Acroball ready for daily use?

Not yet. Version 0.1.0 has the application shell, settings and Merge workflow.
Several tools are still placeholders.

## Which PDF workflow is implemented?

Merge is implemented in the UI. The PDF engine also supports split, extract,
rotate and metadata update, but those workflows do not yet have dedicated UI
pages.

## Does Acroball upload my PDFs?

No implemented workflow uploads files. Current PDF work runs locally.

## Does Acroball support password-protected PDFs?

The engine can open encrypted PDFs when given a password, but the Merge UI does
not currently expose password entry.

## Where are settings and logs stored?

They are stored in the platform data directory resolved by `AppPaths`.
Set `Acroball_DATA_DIR` to override the location.

## Why are some tools visible but unavailable?

The tool catalog is visible early so navigation and design can be built around
the full product direction. Placeholder pages remain until each workflow ships.

## What should contributors read first?

Start with:

- [../development/DEVELOPMENT.md](../development/DEVELOPMENT.md)
- [../development/CURRENT_STATE.md](../development/CURRENT_STATE.md)
- [../ARCHITECTURE.md](../ARCHITECTURE.md)
