# CLAUDE.md

Hoard is an MIT-licensed, self-hosted pull-through package cache (.NET 10 /
ASP.NET Core modular monolith + statically exported Next.js console). NuGet
first. See [README.md](README.md) for the doc map.

## Sources of truth

- Product intent: `docs/PRODUCT_CHARTER.md`, `docs/PRODUCT_VISION.md`, `docs/PRODUCT_SPEC.md`.
- Architecture rules: `docs/ARCHITECTURE.md`, `docs/adr/`.
- What to build next, in order: `docs/HOARD_SPECKIT_MASTER_BACKLOG.md`.
- Principles: `.specify/memory/constitution.md` (once ratified, it wins over everything else).

An earlier attempt at this project by another agent was discarded. Do not
recreate or reference its artifacts (`hoard-speckit`/`hoard-release` skills,
`speckit_flow.py`, F-01–F-07 implementation logs, provider author guide).

## How work happens

- Drive feature work with `/drive-speckit`: one Spec Kit step per run, one
  backlog spec at a time, in order, tracked in `docs/spec-progress.md`.
- One `feature/NNN-<slug>` branch and one PR per spec. Never commit to `main`.
  The maintainer merges; never merge a PR yourself. After the merge,
  `/drive-speckit` tags the merge commit `v0.0.N-alpha` and verifies the release
  (`docs/RELEASING.md`).
- Before specifying, planning or implementing, ground with `/hoard-ground`.
  Record decisions with `/hoard-adr` so every doc stays consistent.
- Reviewer agents in `.claude/agents/` (grounding, architecture boundaries,
  security, cache integrity, NuGet protocol, console UX) gate plans and reviews;
  `drive-speckit` picks which ones apply.
- Stay inside a spec's **Out of scope** line. YAGNI: no abstraction, contract
  or option without a current consumer.
- Core cache engine and protocol work is test-first. Protocol specs need
  real-client (`dotnet`) end-to-end tests. Console work needs component and
  Playwright tests.
- Update README and affected docs in the same PR; describe shipped behavior,
  not plans.

## Architecture guardrails

- `Hoard.Core` never references concrete providers or EF Core; EF Core lives
  only in relational persistence providers. Architecture tests enforce this.
- Endpoints are vertical slices (`Features/<Area>/<UseCase>/`). Cache keys,
  freshness, SSRF policy, verified atomic storage, single-flight, streaming and
  eviction safety live once in shared core services, never per slice.
- Built-in DI and Minimal APIs; no MediatR.
- Loopback by default; network exposure requires the admin token for
  management. Never log secrets.
