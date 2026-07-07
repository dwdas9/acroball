# Initialize — Session Start Protocol

Run this at the start of every session, after `01_System_Master.md` has been
loaded.

---

You are resuming work on Acroball. This is a continuation, not a fresh
audit. Do not attempt to rebuild context by exploring the codebase.

## Ingest — and only this

1. Read `docs/development/CURRENT_STATE.md` in full.
2. Read every file listed under that document's **Context Dependency Index**
   section — the exact file paths, nothing implied around them.

That is the complete set of files you may read before proposing a plan for
the next task. Do not read anything else yet.

## Hard restrictions

- **Do not** scan, list, or walk the repository tree.
- **Do not** open any file not explicitly named in the Context Dependency
  Index, even if it seems related, adjacent, or "probably relevant."
- **Do not** grep or search across the solution to "get a feel" for the
  codebase.
- **Do not** read other documentation in `docs/` (architecture notes, ADRs,
  coding standards) unless `CURRENT_STATE.md` names it explicitly in its
  index for this task.
- **Do not** treat this as an opportunity to perform a repository audit,
  health check, or documentation-consistency pass. That is out of scope for
  session start.

If a file listed in the Context Dependency Index does not exist, or its
content contradicts what `CURRENT_STATE.md` claims about it — **stop and
report this to the user.** Do not fall back to scanning the repository to
"figure it out yourself." A broken index is a checkpoint bug to fix, not a
license to widen scope.

If the **Next Immediate Task** in `CURRENT_STATE.md` requires a file that
isn't in the index, say so and ask before reading it. Expanding scope beyond
the index is a decision, not a default.

## After ingesting

Confirm back to the user, briefly:

- The current milestone and next immediate task, as read from `CURRENT_STATE.md`.
- The exact list of files you ingested (should match the index 1:1).
- Any contradiction or missing file you found (or "none").

Then proceed directly to the next immediate task. Do not re-plan the whole
milestone — the plan already lives in `CURRENT_STATE.md`.
