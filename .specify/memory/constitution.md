# Hoard Constitution

Hoard is an MIT-licensed, self-hosted pull-through package cache. Native package managers use it
through their normal configuration; it fetches from upstream once, verifies, and serves locally.
NuGet first. This document holds durable principles only. Scope and platforms live in
`docs/PRODUCT_CHARTER.md`; release mechanics live in `docs/RELEASING.md`.

## Core Principles

### I. Cache, not registry

Hoard MUST provide transparent pull-through caching for native package-manager clients. Hoard MUST
NOT offer package publishing, custom client tooling, or paid tiers.

Rationale: users adopt Hoard by changing a feed URL, not their tools or workflows.

### II. Trustworthy bytes (NON-NEGOTIABLE)

- Content MUST be staged, checksum-verified and atomically committed before it becomes visible.
- Partial, corrupt or unverified content MUST NEVER be served as a cache hit.
- A cache entry MUST NEVER reference an uncommitted blob.

Rationale: a cache that can serve wrong bytes is worse than no cache.

### III. Secure by default (NON-NEGOTIABLE)

- Hoard MUST bind to loopback by default; network exposure happens only by explicit choice.
- Once management exists, network-exposed management MUST always require authentication.
- Reads of private content MUST always require authorization.
- Upstream access MUST be limited to approved hosts, with SSRF, redirect and DNS checks.
- Secrets MUST NEVER be logged or stored in plaintext.
- Security baselines MUST ship with the first spec that needs them, never later.

Rationale: a network-capable proxy is an attractive pivot point; safe defaults cannot be retrofitted.

### IV. Native protocol fidelity

- Protocol behavior MUST be proven with real package-manager clients.
- Only supported resources MAY be advertised.
- Every advertised URL MUST route through Hoard.

Rationale: clients trust what the server advertises; any leak or gap breaks restores or bypasses
the cache.

### V. Simplicity and YAGNI

- Build the smallest thing that passes the acceptance criteria.
- No abstraction, contract, extension point, schema or configuration option without a current
  consumer; a capability arrives in the spec that first uses it.
- Single process, single node until demand proves otherwise.

Rationale: speculative machinery costs review time and constrains later decisions.

### VI. Enforced modular boundaries

- Hoard is an ASP.NET Core composable modular monolith.
- `Hoard.Core` MUST NEVER reference concrete providers or EF Core; EF Core lives only in relational
  persistence providers.
- Management and protocol endpoints are vertical slices, each delivered with the feature that uses
  it.
- Shared invariants (cache keys and variant partitioning, freshness, SSRF policy, verified atomic
  storage, streaming, single-flight, eviction safety) MUST live once in shared core services, never
  per slice.
- Architecture tests MUST enforce dependency rules.
- Use built-in DI and Minimal APIs; no MediatR.

Rationale: invariants implemented once can be verified once; boundaries keep providers swappable.

### VII. Test-first where correctness lives

- Cache engine and protocol work MUST start from a failing test.
- Protocol specs MUST include real-client end-to-end tests.
- Console work MUST include component and browser tests.
- No spec merges without its tests.

Rationale: correctness of bytes and protocol is the product; tests are the proof.

### VIII. Small specs, continuous delivery

- Each spec MUST be deliverable in one reviewable PR; split anything larger.
- CI MUST gate every PR (build, tests, architecture checks).
- CI/CD exists from the first spec so value is visible early: every merged spec produces a
  published alpha release.

Rationale: small, released increments keep review honest and progress visible.

### IX. Truthful documentation

README and affected docs MUST be updated in the same PR and MUST describe shipped behavior,
distinguishing what works from what is planned.

Rationale: users and contributors act on the docs; docs that overstate erode trust.

### X. A polished, accessible console

- The web console MUST be statically exported and served by the Hoard host with no separate Node
  runtime.
- Target WCAG 2.2 AA, with dark and light themes and reduced-motion support.

Rationale: operators need a console that is easy to deploy and usable by everyone.

## Technology Stack

- Backend: .NET 10, ASP.NET Core.
- Storage: SQLite for metadata; filesystem for artifact content.
- Observability and API description: OpenTelemetry, OpenAPI.
- Console: Next.js App Router with TypeScript, Tailwind CSS and shadcn/ui, as a static export.
- CI/CD: GitHub Actions.

## Development Workflow

- One branch and one PR per spec; the maintainer merges.
- Releases follow `docs/RELEASING.md`.
- Schema migrations MUST be forward-only, with an automatic pre-migration backup.

## Governance

- This constitution supersedes all other practice. Where another document conflicts with it, this
  constitution wins and the other document is corrected.
- Amendments are made by PR and carry a semantic version bump: MAJOR for removing or redefining a
  principle, MINOR for adding a principle or materially expanding guidance, PATCH for clarifications
  and wording.
- Plan reviews MUST check compliance with these principles. Deviations from principles marked
  NON-NEGOTIABLE are rejected; any other deviation MUST be justified in the plan's complexity
  tracking or rejected.

**Version**: 1.0.0 | **Ratified**: 2026-10-31 | **Last Amended**: 2026-10-31
