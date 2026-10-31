---
name: drive-speckit
description: Advance Hoard's Spec Kit workflow by exactly one step — work out where things stand from docs/spec-progress.md, the specs/ folder, git and GitHub, run the right speckit step for the right spec (using the backlog's verbatim specify prompt), apply proportional review gates, update the progress file, commit and push; after the human merges a spec, cut and verify its alpha release. Use to start or continue spec-driven work, or to ask what the next step is.
argument-hint: "Optional: --status | --spec NNN | --step <name> | --dry-run"
user-invocable: true
---

# Drive Spec Kit

You advance Hoard's Spec Kit workflow **one step per invocation** and keep
`docs/spec-progress.md` truthful. The rules come from `CLAUDE.md`,
`docs/DEVELOPMENT.md`, `docs/RELEASING.md` and the constitution; this skill
applies them.

## Non-negotiables

1. **One step per invocation.** Never chain steps; the human reviews between steps.
2. **Never mark a step done that you did not verify** (artifact exists with real
   content; tests ran and you report the actual result).
3. **The specify prompt is verbatim** from the spec's section in
   `docs/HOARD_SPECKIT_MASTER_BACKLOG.md`. Do not paraphrase or trim it.
4. **Never run a step on `main`**, never merge, never push to `main`. Pushing a
   release tag in the `release` step is the one exception, and only on a merge
   commit the human merged.
5. **Respect the spec's Out of scope line.** Anything it excludes stays out, even
   if it looks convenient. No abstraction, table, endpoint or option without a
   consumer in this spec.
6. **Stop on blockers** — a document conflict, an unanswered product question, or a
   failing gate — and record them. Don't paper over them.
7. **Stay proportional.** No evidence ledgers, artifact hashes or repeated review
   loops. Findings become tasks or explicit human deferrals.

## Steps

`constitution` (once) → per spec: `specify` → `clarify` → `plan` → `tasks` →
`analyze` → `implement` → `review` → `accept` → *(human merges)* → `release`.
`checklist` is optional (offer it after `plan` when the spec has notable risk
areas: security, integrity, data migration, accessibility).

## 1. Read state

```bash
cat docs/spec-progress.md
ls -d specs/*/ 2>/dev/null
cat .specify/feature.json 2>/dev/null
grep -c '\[PROJECT_NAME\]' .specify/memory/constitution.md
git branch --show-current
git status --short
gh pr list --state all --limit 20 --json number,title,headRefName,state,isDraft,url,mergeCommit
git ls-remote --tags origin 'v0.0.*-alpha'
```

Reconcile `spec-progress.md` with reality. Evidence:

| Step | Evidence |
|---|---|
| specify | `specs/NNN-*/spec.md` with non-template content |
| clarify | `spec.md` has a Clarifications section, or the log records "no questions" |
| plan | `specs/NNN-*/plan.md` exists |
| tasks | `specs/NNN-*/tasks.md` exists |
| analyze | log entry (no artifact) |
| implement | all tasks in `tasks.md` checked and the code exists |
| review | log entry with gate verdicts |
| accept | log entry quoting the human's acceptance; PR marked ready |
| merged | the spec's PR is merged → status `Merged`, next step `release` |
| release | tag `v0.0.N-alpha` on the PR's merge commit and a published GitHub prerelease → status `Complete` |

If the file and reality disagree, **trust reality**, fix the file and say what you
corrected.

## 2. Arguments

| Argument | Behavior |
|---|---|
| *(none)* | Determine and run the next step |
| `--status` | Report state and the next step; run nothing |
| `--spec NNN` | Target that spec. If its dependencies (backlog table) are not Complete, say so and ask before proceeding |
| `--step <name>` | Force a step; warn and ask if out of order |
| `--dry-run` | Show the exact command, prompt and branch you would use; run nothing |

## 3. Preflight

**Constitution gate.** If the constitution still contains `[PROJECT_NAME]`, the
only allowed step is `constitution`:
- From up-to-date `main`, create `chore/project-constitution`.
- Run `speckit-constitution` with the fenced prompt from the backlog's
  "Constitution prompt" section, verbatim.
- Run `docs-grounding-auditor` on the result. Fix BLOCKER findings before
  committing; surface any conflicts it names to the human.
- Update the progress file, commit, push, open a PR and give the link. Stop.
  Spec 001 waits until that PR is merged. The constitution gets no release tag.

**Branch gate.**
- `specify` for a new spec starts from `main` (you create the branch).
- Every later step up to `accept` must run on that spec's recorded
  `feature/NNN-<slug>` branch. If `HEAD` is elsewhere, stop and offer to switch
  (never with uncommitted changes).
- `release` runs from an up-to-date `main`.
- Only one numbered spec is in progress at a time unless the human explicitly
  started another.

**Grounding.** Before `specify`, `plan` and `implement`, invoke the `hoard-ground`
skill for the target spec.

## 4. Select the next step

Target the first spec in backlog order (MVP table, then post-MVP only when the
human names one) that is not `Complete`, or the one in progress. Within it, the
next step is the first not yet done. A spec in `Accepted` status is waiting for
the human to merge — report that and stop.

## 5. Run the step

**Every** `speckit-*` invocation for a spec (specify, clarify, plan, checklist,
tasks, analyze, implement, converge) passes
`SPECIFY_FEATURE_DIRECTORY=specs/NNN-<slug>`, taken from the spec's recorded
folder in the progress file. Spec Kit otherwise falls back to the per-machine
`.specify/feature.json`, which is wrong after a fresh clone or when two specs are
in progress.

### Reviewer selection

Run a reviewer at `plan` and `review` when the spec is listed **or** the
plan/diff touches its area. All are read-only agents in `.claude/agents/`.

| Agent | Default specs | Also when the work touches |
|---|---|---|
| `docs-grounding-auditor` | all (also at `specify`) | — |
| `architecture-boundary-reviewer` | 001–007, 010, 012 | projects, references, packages, modules, persistence, migrations, endpoint structure |
| `security-reviewer` | 002, 005, 006, 009, 010, 011, 012, 013, 014, 017, 019 | binding/exposure, auth, tokens, secrets, outbound HTTP, logging, input parsing, file paths |
| `cache-integrity-reviewer` | 003, 004, 006, 007, 009, 013, 019 | staging/commit, digests, concurrency, streaming, eviction/GC, migrations, crash recovery |
| `nuget-protocol-reviewer` | 007, 008, 009, 018 | NuGet routes, service index, URL rewriting, client setup snippets |
| `console-ux-reviewer` | 014–018 | anything under `apps/web` |

### specify
1. Sync `main` (`git checkout main && git pull --ff-only`). Confirm all of the
   spec's dependencies are Complete; if not, stop and ask. If the human chooses to
   start early and the spec needs a dependency's unmerged interfaces, branch from
   that dependency's feature branch, note it in the PR description, and rebase
   onto `main` once the dependency merges.
2. Pick `<slug>`: 2–4 kebab-case words from the spec title. Create
   `feature/NNN-<slug>` where `NNN` is the backlog spec number.
3. Invoke `speckit-specify` with `SPECIFY_FEATURE_DIRECTORY=specs/NNN-<slug>`
   (so numbering matches the backlog 1:1 and the release tag maps correctly) and
   the verbatim prompt, plus:

   > **Hoard override:** Where the docs decide something, state it as a constraint.
   > Where a *product* decision is missing, mark `[NEEDS CLARIFICATION]` rather
   > than guessing. Leave technical choices to the plan. Honor the spec's
   > "Out of scope" sentence exactly. Keep the spec proportional to the feature:
   > no capability, entity or endpoint that this spec's acceptance does not need.

4. Gate: `docs-grounding-auditor` on `spec.md`.
5. Commit, push, and open a **draft PR** titled `NNN — <title>`; record branch and
   PR link in the progress file.

### clarify
Run `speckit-clarify` and let the human answer. Questions that are really
document-level product decisions get escalated, not answered in the spec alone —
if the answer changes a canonical doc, use `hoard-adr` (in a separate step if
large).

### plan
Run `speckit-plan`. Gates: reviewers per the selection table. If the plan makes a
decision worth recording beyond this spec, propose an ADR via `hoard-adr` and get
the human's approval.

### checklist (optional)
Run `speckit-checklist` focused on the spec's real risk areas.

### tasks
Run `speckit-tasks`. Check the tasks follow the constitution's test-first rule:
cache-engine and protocol work starts from failing tests; protocol specs include
real-`dotnet`-client tests through the harness (from 007); console specs include
component and Playwright tests; failure paths (interruption, corruption,
concurrency, upstream outage, unauthenticated access) are covered, not just the
happy path. Tests only for what this spec changes.

### analyze
Run `speckit-analyze`. Report findings; fix only CRITICAL/HIGH ones in spec, plan
or tasks with the human's agreement. Do not loop.

### implement
Run `speckit-implement`. Run the build and tests listed in `CLAUDE.md` (the
commands section is added by spec 001) and report actual results. Commit in
sensible increments; push. After pushing, check the PR's CI with
`gh pr checks` and report real status.

### review
1. Run `speckit-converge` once to find unbuilt work.
2. Run the reviewers from the selection table against the branch diff
   (`git diff main...HEAD`).
3. Run the full test suite and confirm CI is green on the PR.
4. Outcome:
   - Clean → next step is `accept`.
   - Findings → append concrete tasks to `tasks.md`; next step is `implement`.
   - After **two** review rounds with findings, stop and ask the human whether to
     fix, defer or accept with known limitations. No open-ended loops.

### accept
No Spec Kit command. Summarize for the human: what was delivered against the
spec's acceptance focus, test and CI results, reviewer verdicts, known
limitations, and docs/ADR changes (README and affected docs must describe what
now ships). **Ask for acceptance.** Only after the human explicitly accepts:
record their words in the log, set status `Accepted`, mark the PR ready
(`gh pr ready`), and give the PR link as a bare URL. The human merges.

### release
Runs once the spec's PR is merged. Per `docs/RELEASING.md`:
1. Sync `main`. Read the PR's merge commit
   (`gh pr view <N> --json mergeCommit -q .mergeCommit.oid`) and confirm it is on
   `origin/main`.
2. Tag: `v0.0.<N>-alpha` with `N` = the spec number without leading zeroes.
   Check local and remote tags and existing releases first. If a correct tag
   already exists, reuse it; never move or recreate a tag. A tag pointing
   elsewhere is a blocker.
3. `git tag -a v0.0.N-alpha <merge-sha> -m "Hoard v0.0.N-alpha — NNN <title>"` and
   push only that tag (`git push origin refs/tags/v0.0.N-alpha`).
4. Watch the release workflow (`gh run list --workflow release.yml --limit 5`,
   then `gh run watch <id>`). Verify `gh release view v0.0.N-alpha`: prerelease,
   notes, binaries and checksums attached, image pushed to GHCR.
5. On success set status `Complete` with the release URL. On failure, record the
   failing job as a blocker; don't retag. Rerunning the failed run is fine.
6. Don't commit to `main`. The progress-file update for the release rides in the
   next step's commit (normally the next spec's `specify` branch); reality
   (tag + release) is the evidence until then.

If the release workflow doesn't exist yet (before spec 001 has merged), skip the
tag and say so.

## 6. Update the progress file

Always, even when blocked:
- Update the spec's row (status, last step, next step, branch, PR link, release).
  Add a row when a spec starts.
- Append one log line: `| YYYY-MM-DD | NNN | <step> | <one-line result + gate verdicts> |`
  (use `date +%F`).
- Blockers go in the Blocked table.

## 7. Commit and push

Commit only verified steps, with the progress update in the same commit:

```text
<step>(NNN): <summary>

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

Prefixes: `spec` · `clarify` · `plan` · `checklist` · `tasks` · `analyze` · `impl` ·
`review` · `accept` · `release`. Push the branch after every commit. A blocked step
is not committed; leave the working tree and explain.

## 8. Report and stop

```
Spec NNN — <title>
Branch:    feature/NNN-<slug>
PR:        #N — <bare url>
Step:      <step>   <PASS | BLOCKED | NEEDS HUMAN>
Artifacts: <paths>
Gates:     <verdicts>
CI:        <green | failing: job | pending>
Commit:    <sha> <message>
Next:      <step> — run /drive-speckit again
```

If anything needs a human decision, say so plainly and stop.
