# User Guide

This guide describes the application as currently implemented.

## Current Status

Acroball 0.1.0 includes the application shell, settings and the Merge workflow.
Other PDF tools are visible in the catalog but remain placeholders until their
milestones are implemented.

## Start Acroball

From the repository:

```bash
dotnet run --project src/Acroball.Desktop
```

## Navigation

Use the sidebar to open:

- Home
- Merge
- Split
- Organize
- Rotate
- Extract
- Compress
- Protect
- Metadata
- Settings

Keyboard shortcuts:

- `Ctrl+1`: Home
- `Ctrl+2`: Merge
- `Ctrl+3` through `Ctrl+9`: remaining catalog tools

Only Merge is currently implemented as a workflow. Other tool pages show
placeholder information.

## Merge PDFs

1. Open Merge.
2. Add PDF files with Add Files or by dragging files into the file list.
3. Reorder files with Move Up, Move Down or drag/drop.
4. Choose an output folder.
5. Edit the suggested output file name if needed.
6. Select Merge.
7. Watch progress or select Cancel to stop the operation.
8. When complete, use Open Output Folder or Merge Another.

The merged output is written as a PDF in the selected folder.

## Settings

Open Settings to choose the theme:

- System
- Light
- Dark

The setting is persisted locally. Window size and maximized state are also
persisted.

## Privacy

Acroball runs locally. The current implemented workflows do not upload files or
require accounts.

## Local Data

Acroball stores settings, recent files and logs in the platform data directory.
For development and portable scenarios, set `Acroball_DATA_DIR` to override
that location.
