---
name: hoard-ground
description: Load the authoritative Hoard context for a piece of work — the constitution, the relevant charter/spec/architecture sections, ADRs and the spec's backlog entry — and summarize the constraints that apply. Use before specifying, planning or implementing a spec, or before any work that might assert a product or architecture decision.
argument-hint: "Spec number (e.g. 007) or a short topic (e.g. 'storage', 'admin token')"
user-invocable: true
---

# Ground in the Hoard docs

Goal: the documents that govern this work are actually in context, and the
constraints are stated before work starts. Read; don't skim from memory.

## 1. Always read

- `CLAUDE.md`
- `.specify/memory/constitution.md` (skip if it is still the template)
- Every file in `docs/adr/` (they are short)

## 2. Read for the target

If given a spec number `NNN`:
- Its full section in `docs/HOARD_SPECKIT_MASTER_BACKLOG.md` (outcome, acceptance
  focus, out of scope, prompt) and the inventory rows for its dependencies.
- If `specs/NNN-*/` exists: `spec.md`, `plan.md`, `tasks.md` as present.
- Completed dependency specs' `spec.md` and `plan.md` contracts when this spec
  consumes their interfaces.

Then read the sections that match the topic:

| Topic | PRODUCT_SPEC.md | ARCHITECTURE.md | Also |
|---|---|---|---|
| Solution layout, references, packages | §2A, §11 | §3, §4 | ADR-0001, RELEASING.md |
| Host, config, binding, profiles | §8, §10 | §5, §8 | charter §7 |
| Metadata store, migrations | §2, §3, §9 | §9 | |
| Artifact storage, GC | §2, §3, §9 | §9 | |
| Registries, upstream safety | §3, §8, §10 | §8, §10 | |
| Cache engine | §3, §4 | §6 | |
| NuGet protocol | §5 | §6 | NuGet v3 docs (Microsoft Learn MCP) |
| Management API, admin token | §6, §8 | §8, §10 | |
| Activity, telemetry | §3, §9 | | |
| Console | §7 | | PRODUCT_VISION.md design direction |
| Distribution, upgrades | §9, §12, §13 | | RELEASING.md |

## 3. Report

Give a compact brief (aim for under 300 words):

- **Governing decisions** — bullets with their source (e.g. "Spec §8: admin token
  required whenever network-exposed").
- **In scope / out of scope** for this spec, per the backlog (name the later spec
  or X- item that owns excluded items).
- **Open questions** the docs leave to this spec (e.g. backlog Open decisions).
- **Conflicts** — any disagreement between documents. Surface it; do not resolve it.
