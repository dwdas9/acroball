# Acroball — Project Instructions

This repository uses a Continuous State Management protocol instead of ad-hoc
onboarding or narrative documentation. Full rationale: `docs/prompts/README.md`.

## At the start of every session, before anything else

1. Read `docs/prompts/01_System_Master.md` — persona, technical constraints,
   banned documentation categories, repository-as-source-of-truth philosophy.
2. Read `docs/prompts/02_Initialize.md` and follow it exactly: read ONLY
   `docs/development/CURRENT_STATE.md` and the files listed in its Context
   Dependency Index. Do not scan, grep, or walk the rest of the repository.
3. Report back the current milestone, next task, and files ingested, per
   `02_Initialize.md`, then proceed.

## During a long session

Don't wait for the end of the session to checkpoint. Re-run the
`docs/prompts/03_Checkpoint.md` protocol periodically during any session
involving multiple non-trivial file edits — roughly every few tool-call-heavy
exchanges, or after finishing a meaningful chunk of a multi-step task — so
that an abrupt stop (crash, running out of tokens, connection drop) never
loses more than a few minutes of progress. A mid-session checkpoint follows
the exact same rules and output structure as an end-of-session one; only the
trigger differs. Do not ask permission first — just do it and continue.

## At the end of a session

Run `/checkpoint` (or, if asked to end/wrap up the session without it being
invoked explicitly, follow `docs/prompts/03_Checkpoint.md` directly). This
overwrites `docs/development/CURRENT_STATE.md` and touches nothing else.

## Standing rule

Never create status trackers, development logs, implementation histories, or
narrative changelogs. See `docs/prompts/01_System_Master.md` for the full
banned list and reasoning. A genuine architectural decision becomes a new ADR
in `docs/adr/`; everything else about "what happened" belongs in git history
or in `docs/development/CURRENT_STATE.md`.
