# Checkpoint — Session End Protocol

Trigger this when the session is ending, regardless of whether the milestone
is complete. **Also trigger it periodically mid-session** — roughly every few
tool-call-heavy exchanges during any session with multiple non-trivial file
edits — per `CLAUDE.md`'s "During a long session" rule. This is a resilience
measure: if the session stops abruptly (crash, out of tokens, dropped
connection), the next session should never have to reconstruct more than a
few minutes of lost progress from git diffs alone.

---

A mid-session checkpoint follows the exact same rules and output below as an
end-of-session one. The only difference: at true session end, treat this as
the final action and stop; mid-session, write the checkpoint and then
continue the work in progress.

Development for this checkpoint is being captured now. **Do not implement any
further functionality until the checkpoint file is written.** Do not touch
any file other than the one this protocol names below.

Your only remaining job is to overwrite
`docs/development/CURRENT_STATE.md` in full, so the next session can resume
by reading that single file plus the files it lists — nothing else.

## Rules

- This is the **only** file you write during checkpoint. Do not update
  `PROJECT_STATUS.md`, a changelog, a decision log, or any other document —
  those categories are banned per `01_System_Master.md`. If durable
  architectural knowledge was produced this session (a real decision that
  outlives this task), that belongs in an ADR — a single new immutable file
  in `docs/adr/`, not a rewrite of history. Everything else about *what
  happened this session* is either in git or belongs in this file.
- Overwrite the file completely. This is not an append-only log; the
  previous content is disposable once superseded.
- Every file path in the Context Dependency Index must be a real file, not a
  directory, and must be the exact path the next session will read verbatim.
- Keep it lean. This file should be readable in under a minute — that's the
  whole point.

## Required output — overwrite `docs/development/CURRENT_STATE.md` with exactly this structure

```markdown
# Current State

Last updated: {{date}}

## Current Milestone
{{name of the active milestone}}

## Last Completed Action
{{the exact class, file, or component just modified or finished}}

## Current Blockers
{{compile errors, missing logic, failing tests — or "None"}}

## Next Immediate Task
{{the single, specific task the next session must execute — one task, not a list}}

## Context Dependency Index
{{strict bulleted list of exact file paths required to execute the Next
Immediate Task. Files only, never directories. If the task needs no
additional context beyond this document, write "None."}}

- path/to/exact/File1.cs
- path/to/exact/File2.axaml
- path/to/exact/File2.axaml.cs
```

## Before finishing

- Confirm the solution builds, or state plainly that it does not and why
  (that becomes part of **Current Blockers**).
- Re-read the Context Dependency Index once you've written it and verify
  every path actually exists in the repository right now.
- Don't narrate the checkpoint itself — no summary, changelog entry, or
  recap of it; the state file above is the entire deliverable for this step.
  At true session end, that also ends the turn. Mid-session, just resume the
  task afterward as normal.
