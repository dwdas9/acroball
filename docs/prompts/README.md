# How to Use This Protocol

These three files are the entire session-continuity workflow. Nothing else in
`docs/` needs to be read to start or end a session — that's the point.

## In Claude Code (automated)

The root `CLAUDE.md` is auto-loaded by Claude Code at the start of every
session in this repo and wires this in for you:

- **Session start:** happens automatically — Claude reads
  `01_System_Master.md` and `02_Initialize.md` and follows them before doing
  anything else. You don't need to type anything.
- **Session end:** run `/checkpoint`. It executes `03_Checkpoint.md` and
  overwrites `docs/development/CURRENT_STATE.md`.
- **Mid-session re-anchor:** run `/initialize` if context has drifted (e.g.
  after a long session) and you want Claude to re-read `CURRENT_STATE.md`.

Skim the diff on `CURRENT_STATE.md` after `/checkpoint`, before closing —
it's the only thing carrying context into the next session.

## In any other LLM tool (manual)

1. Open a new chat.
2. Give the LLM `01_System_Master.md`, then `02_Initialize.md`.
3. It reads `docs/development/CURRENT_STATE.md` and only the files listed in
   that document's Context Dependency Index, then reports back: current
   milestone, next task, files ingested, and any contradiction found.
4. Confirm that's correct, then proceed with the next task.
5. Before you stop, give the LLM `03_Checkpoint.md`. It overwrites
   `CURRENT_STATE.md` and touches nothing else. Skim the diff before closing.

## Order of operations

```
01_System_Master.md  →  02_Initialize.md  →  ... work ...  →  03_Checkpoint.md
      (persona)            (session start)                     (session end)
```

If a session ends abnormally (crash, ran out of time) without step "Ending a
session" happening, the next session's `CURRENT_STATE.md` will simply be
stale — `02_Initialize.md`'s contradiction check is what catches that, not a
recovery file.
