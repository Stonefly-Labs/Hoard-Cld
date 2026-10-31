---
name: hoard-adr
description: Record a Hoard architecture or product decision as an ADR in docs/adr/ and propagate it to every document it affects (charter, product spec, architecture, backlog prompts, RELEASING.md, CLAUDE.md), then sweep for contradictions. Use when the human makes a decision, when a plan or implementation forces one, or when a document conflict is resolved.
argument-hint: "Short description of the decision"
user-invocable: true
---

# Record a decision (ADR)

Decisions drift into contradictions when they're written in one place only. This
skill writes the ADR **and** updates everything it touches.

## 1. Confirm the decision

- State the decision back in 2–4 bullets, including what is explicitly *not*
  decided (open questions).
- If the human hasn't actually made the decision, present a recommendation and
  ask. Never record an invented decision as Accepted.

## 2. Write the ADR

- File: `docs/adr/NNNN-<kebab-title>.md`, next number after the highest existing.
- Format (match ADR-0001):

```markdown
# ADR-NNNN: <Title>

- **Status:** Accepted | Proposed | Superseded by ADR-NNNN
- **Date:** YYYY-MM-DD
- **Specs:** <number — title>[; ...] (if tied to specs)

## Context
## Decision
## Open questions (if any)
## Alternatives considered
## Consequences
```

- Keep it short and concrete. Reference specs by number **and** check the number
  against the backlog's MVP and post-MVP tables.
- Superseding an ADR: mark the old one `Superseded by ADR-NNNN` and say so in the
  new one.

## 3. Propagate

Search all docs for the topic and update every affected statement:

```bash
grep -rn -i '<keywords>' CLAUDE.md README.md docs/
```

Usual places:
- `docs/PRODUCT_CHARTER.md` §6 (scope), §7 (profiles), §12 (open decisions)
- `docs/PRODUCT_SPEC.md` (the relevant section and §14 open questions)
- `docs/ARCHITECTURE.md` (the relevant section and §13 unresolved decisions)
- `docs/HOARD_SPECKIT_MASTER_BACKLOG.md` — guardrails, the constitution prompt,
  affected specs' outcome, acceptance focus, out-of-scope line **and** paste-ready
  prompt (all must agree), dependencies, open decisions. If the constitution
  already exists, propose an amendment via `speckit-constitution` instead of
  editing it directly.
- `docs/RELEASING.md` for versioning or distribution decisions
- `CLAUDE.md` if it's a working rule or guardrail
- Existing `specs/NNN-*/` artifacts for in-progress specs (flag to the human
  rather than rewriting accepted specs)

## 4. Sweep

Run the `docs-grounding-auditor` agent with the instruction to check the whole
`docs/` set for contradictions with the new ADR, wrong spec-number references and
broken links. Fix what it finds.

## 5. Report

List the ADR file, every file changed (one line each on what changed), anything
left for the human, and the open questions.
