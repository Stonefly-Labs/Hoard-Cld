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
| 001 | Pipeline and solution skeleton | In progress | implement | review (round 2) | feature/001-pipeline-skeleton | [#4](https://github.com/Stonefly-Labs/Hoard-Cld/pull/4) | — |

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
| 2026-10-31 | 001 | plan | plan/research/data-model/contracts/quickstart; docs-grounding-auditor GROUNDED WITH NOTES, architecture-boundary-reviewer PASS WITH NOTES (rule hardening, no Version on pack), security-reviewer CHANGES NEEDED → 6 MAJOR fixed in plan (Kestrel config endpoints/PreferHostingUrls, hostile-env smoke, socket-table check, tag injection, publish gating/permissions, guard SHA check). Open for maintainer: alpha download visibility (repo private) |
| 2026-10-31 | 001 | checklist | checklists/security.md (21) + release.md (21), unchecked for maintainer; agent evaluation: 33 satisfied by spec/plan, 6 minor gaps to fold into tasks, 3 maintainer decisions (alpha visibility, Hoard.Core initial version, unexecuted win/arm64 artifacts) |
| 2026-10-31 | 001 | tasks | tasks.md (44 tasks; test-first per story; 6 checklist gaps folded in). Maintainer decisions recorded in spec/plan/research: alphas public (manual visibility settings before v0.0.1-alpha), Hoard.Core starts at 0.0.1-alpha, unexecuted win-x64/linux-arm64/arm64-image accepted for alphas (revisit before MVP) |
| 2026-10-31 | 001 | tasks | Amended: maintainer chose native hosted runners for every platform (repo public → standard runners free); release smoke-tests all 4 executables + amd64/arm64 images; superseded the "accept unexecuted artifacts" answer. Repo must be public before implement pushes release.yml |
| 2026-10-31 | 001 | analyze | 0 CRITICAL, 0 HIGH, 4 MEDIUM, 4 LOW; 19/19 FRs covered by tasks, no constitution conflicts. No edits made. Open: repo still private (blocks T037) |
| 2026-10-31 | 001 | analyze | Maintainer asked to fix all findings: C1 (re-run proof assigned to `release` step; FR-015/SC-005/quickstart §7/T039/T042), U1 (publish job setup steps in T035), G1 (T036 checks publish needs every verify job), I1 (edge case reworded), I2 (sixth Core rule added to FR-005/SC-002 from ARCHITECTURE §3), U2 (verify on ubuntu-latest), U3 (Dependabot consumer stated). O1 (repo public) with maintainer |
| 2026-10-31 | 001 | analyze | Blocker cleared: maintainer made the repository public (verified `gh repo view` → PUBLIC) |
| 2026-10-31 | 001 | implement | 44/44 tasks; build 0 warnings, 34/34 tests; local smoke pass; PR #4 CI green (build-test, smoke, release.yml dry run on all 4 native runners + amd64/arm64 images, publish skipped); gate proven failing on 3faf8ad then reverted. Deviation: xUnit v3 4.x needs Microsoft.Testing.Platform, so `dotnet test --solution Hoard.slnx` and no VSTest packages |
| 2026-10-31 | 001 | review | Round 1: converge clean (19/19 FRs, 5/5 SCs); build 0 warnings, 34/34 tests; PR #4 CI green. docs-grounding-auditor GROUNDED WITH NOTES (MAJOR: release packs Hoard.Core by name, not every packable project), architecture-boundary-reviewer PASS WITH NOTES, security-reviewer PASS WITH NOTES (6 plan security fixes verified). 8 tasks appended (T045–T052); 2 notes deferred to accept |
| 2026-10-31 | 001 | implement | Review round 1 tasks T045–T052 done (52/52): release `verify` builds/tests/packs the solution in Release (packs only Hoard.Core today); skopeo from runner image, no apt-get; release.yml path filter widened; container smoke rejects `root:*`/`0:*` and adds hostile `--urls` run; DEVELOPMENT/RELEASING/plan corrected. Build 0 warnings, 34/34 tests (Debug and Release); binary smoke 4/4 and arm64 container smoke 3/3 locally |
