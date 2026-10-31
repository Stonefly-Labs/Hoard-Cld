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
| — | Project constitution | Complete | constitution | — (merged in #3; no release tag) | chore/project-constitution | [#3](https://github.com/Stonefly-Labs/Hoard-Cld/pull/3) | — |
| 001 | Pipeline and solution skeleton | In progress | clarify | plan | feature/001-pipeline-skeleton | [#4](https://github.com/Stonefly-Labs/Hoard-Cld/pull/4) | — |

Rows for specs are added when they start. Status values: `Not started`,
`In progress`, `Blocked`, `Accepted` (awaiting merge), `Merged` (awaiting release),
`Complete` (released).

## Blocked / needs a human

| Date | Spec | Issue |
|---|---|---|

## Log

| Date | Spec | Step | Result |
|---|---|---|---|
| 2026-10-31 | — | constitution | v1.0.0 ratified from backlog prompt; docs-grounding-auditor GROUNDED WITH NOTES (MAJOR governance escape hatch fixed; invented stack-ADR rule removed; console/PRODUCT_SPEC conflict escalated) |
| 2026-10-31 | — | constitution | Console conflict resolved by maintainer: keep Principle X absolute; removed "optional separate Next.js hosting" from PRODUCT_SPEC §2 |
| 2026-10-31 | 001 | specify | spec.md from verbatim backlog prompt; docs-grounding-auditor GROUNDED WITH NOTES (MAJOR: container must stay loopback-only despite base-image default — fixed; minors fixed); 1 open question (FR-015 packable projects) for clarify. Constitution row corrected to Complete (#3 merged) |
| 2026-10-31 | 001 | clarify | 1 question: FR-015 → maintainer: publish Hoard.Core as an (essentially empty) package from the first release; no other critical ambiguities |
