# Hoard — Spec Progress

Maintained by the `drive-speckit` skill. One row per spec; a one-line log entry per
step. Trust the filesystem, git tags and GitHub over this file — if they disagree,
fix this file.

Steps: `constitution` (once) → per spec: `specify` → `clarify` → `plan` → `tasks` →
`analyze` → `implement` → `review` → `accept` → *(maintainer merges)* → `release`.
(`checklist` is optional, run when a spec has notable risk areas.)

## Status

| Spec | Title | Status | Last step | Next step | Branch | PR | Release |
|---|---|---|---|---|---|---|---|
| — | Project constitution | In progress | constitution | merge PR, then 001 specify | chore/project-constitution | (pending) | — |

Rows for specs are added when they start. Status values: `Not started`,
`In progress`, `Blocked`, `Accepted` (awaiting merge), `Merged` (awaiting release),
`Complete` (released).

## Blocked / needs a human

| Date | Spec | Issue |
|---|---|---|
| 2026-10-31 | — | Constitution X (console MUST be static export, no Node runtime) conflicts with PRODUCT_SPEC §2 "Optional separate Next.js hosting may be introduced later". Maintainer to choose: qualify X as "bundled console" or drop the PRODUCT_SPEC sentence. |

## Log

| Date | Spec | Step | Result |
|---|---|---|---|
| 2026-10-31 | — | constitution | v1.0.0 ratified from backlog prompt; docs-grounding-auditor GROUNDED WITH NOTES (MAJOR governance escape hatch fixed; invented stack-ADR rule removed; console/PRODUCT_SPEC conflict escalated) |
