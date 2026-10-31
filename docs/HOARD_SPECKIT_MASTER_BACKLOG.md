# Hoard — Spec Kit Backlog

**Status:** v0.3 (2026-10-31) · **Product:** Hoard · **License:** MIT · **Source baseline:** [Charter](PRODUCT_CHARTER.md), [Vision](PRODUCT_VISION.md), [Product spec](PRODUCT_SPEC.md), [Architecture](ARCHITECTURE.md)

> **Purpose.** The ordered list of feature specifications for Hoard, with a ready-to-paste Spec Kit prompt for each. None of these specifications exist yet. Order is **walking skeleton first**: the pipeline ships on day one, a real cached `dotnet restore` arrives by spec 007, and provider contracts are qualified only when a second implementation exists.

## How to use this document

1. Run the [constitution prompt](#constitution-prompt) once with `/speckit-constitution`.
2. Take MVP specs **in order**. Paste one prompt into `/speckit-specify`, then run `/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`, `/speckit-analyze` and `/speckit-implement`.
3. Each spec is one branch (Spec Kit's `NNN-short-name`) and one PR. MVP spec numbers below are the intended spec directory numbers; if Spec Kit would assign a different number, pass `--number`. Post-MVP items carry `X-` IDs and take the next free number when started.
4. A spec is done when its acceptance focus passes in CI and the maintainer merges the PR. The merge commit is then tagged `v0.0.N-alpha` ([Releasing](RELEASING.md)).
5. Respect each spec's **Out of scope** line. Work that belongs to a later spec stays there.

## Shared guardrails (apply to every specification)

- Hoard is a **self-hosted pull-through cache**, not a publishing registry. No paywalls, no required SaaS.
- **One product, two profiles**: personal (loopback, SQLite + filesystem, nothing else to run) and team (network-exposed, built-in admin token). A personal install stays lightweight.
- **MVP is NuGet only, public upstreams only, one upstream per registry.** Private feeds, feed merging and other ecosystems are post-MVP.
- **Security and integrity are never deferred**: no partial blobs as hits, verify content, SSRF-safe upstream access, protected management whenever network-exposed, no secrets in logs, from the first network-capable spec.
- **No speculative abstraction.** Module boundaries are enforced by architecture tests from spec 001; contracts grow only as a real implementation needs them.
- **Honest protocol support**: never advertise unsupported resources; preserve status codes, headers, local URLs and checksum behavior.
- **Docs reflect shipped behavior** at every merge.

## Constitution prompt

```text
/speckit-constitution Create the Hoard constitution. Hoard is an MIT-licensed, self-hosted pull-through package cache: native package managers use it through their normal configuration; it fetches from upstream once, verifies, and serves locally. NuGet first.

Principles:
I. Cache, not registry. Transparent pull-through caching for native clients; no package publishing, no custom client tooling, no paid tiers.
II. Trustworthy bytes (non-negotiable). Content is staged, checksum-verified and atomically committed before it becomes visible; partial, corrupt or unverified content is never served as a hit; a cache entry never references an uncommitted blob.
III. Secure by default (non-negotiable). Loopback binding by default; network exposure requires the built-in admin token for management and explicit opt-in for anonymous package reads; upstream access only to approved hosts with SSRF, redirect and DNS checks; secrets never logged or stored in plaintext. Security baselines ship with the first spec that needs them, never later.
IV. Native protocol fidelity. Protocol behavior is proven with real package-manager clients (starting with dotnet restore); only supported resources are advertised; all advertised URLs route through Hoard.
V. Simplicity and YAGNI. Build the smallest thing that passes the acceptance criteria. No abstraction, contract, extension point or configuration option without a current consumer. Single process, single node until demand proves otherwise.
VI. Enforced modular boundaries. .NET 10 ASP.NET Core composable modular monolith. Hoard.Core never references concrete providers or EF Core; EF Core lives only in relational persistence providers. Management and protocol endpoints are vertical slices; shared invariants (cache keys and auth partitioning, freshness, SSRF policy, verified atomic storage, streaming, single-flight, eviction safety) live once in shared core services. Architecture tests enforce dependency rules. Built-in DI and Minimal APIs; no MediatR.
VII. Test-first where correctness lives. Core cache engine and protocol work start from a failing test; protocol specs include real-client end-to-end tests; console work includes component and Playwright tests. No spec merges without its tests.
VIII. Small specs, continuous delivery. Each spec is deliverable in one PR. GitHub Actions CI gates every PR (build, tests, architecture checks). CI/CD exists from the first spec so value is visible early: every merged spec produces an alpha release with a GHCR container image, self-contained binaries (linux-x64, linux-arm64, osx-arm64, win-x64) and GitHub Packages NuGet packages.
IX. Truthful documentation. README and affected docs are updated in the same PR and describe shipped behavior, distinguishing working from planned.
X. A polished, accessible console. Next.js (latest stable) App Router, TypeScript, Tailwind CSS, shadcn/ui; statically exported and served by the .NET host with no Node runtime; WCAG 2.2 AA target, dark and light themes, reduced-motion support.

Also record: technology stack (.NET 10, ASP.NET Core, SQLite, filesystem storage, OpenTelemetry, OpenAPI, Next.js static export, GitHub Actions, GHCR, GitHub Packages); MVP platforms (Linux container amd64/arm64, Linux, macOS arm64, Windows x64 executables); development workflow (one branch and PR per spec, the maintainer merges, the merge commit is tagged v0.0.N-alpha; forward-only schema migrations with automatic pre-migration backup; independently versioned packages bumped in the PR that changes them); governance (amendments by PR with a version bump; the constitution supersedes other practice; plan reviews check compliance).
```

## MVP inventory

| Spec | Title | Depends on | Folds in (v0.2 IDs) |
|---|---|---|---|
| 001 | [Pipeline and solution skeleton](#spec-001) | — | F-01, 35 (part) |
| 002 | [Host composition, configuration and deployment profiles](#spec-002) | 001 | F-02, F-03, 01 |
| 003 | [SQLite metadata store](#spec-003) | 002 | F-05, 03 |
| 004 | [Filesystem artifact store](#spec-004) | 002 | F-06, 04 |
| 005 | [Registries and safe upstream access](#spec-005) | 003 | 02, 18 (baseline) |
| 006 | [Cache engine: freshness, single-flight and streaming](#spec-006) | 003, 004, 005 | 05, 06 |
| 007 | [NuGet restore through Hoard + real-client harness](#spec-007) | 006 | 07, 09 |
| 008 | [NuGet compatibility coverage](#spec-008) | 007 | 08 |
| 009 | [Management API and admin authentication](#spec-009) | 003, 007 | 10, F-04, 18/24 (part) |
| 010 | [Registry management](#spec-010) | 005, 009 | 02 (management) |
| 011 | [Request activity, statistics and telemetry](#spec-011) | 006, 009 | 12 (data), 23 (part) |
| 012 | [Disk quota, LRU eviction and garbage collection](#spec-012) | 004, 006, 009, 011 | 19 (core) |
| 013 | [Console shell and design system](#spec-013) | 009 | 11 |
| 014 | [Overview, packages and live activity](#spec-014) | 011, 013 | 12 |
| 015 | [Registries, storage, settings and setup assistant](#spec-015) | 010, 012, 013 | 13, 19 (UI) |
| 016 | [Distribution, first run and upgrades](#spec-016) | 012, 015 | 14 |

**MVP gate:** all of 001–016 merged → release `v0.1.0`.

## Post-MVP inventory

| ID | Priority | Title | Depends on | Folds in |
|---|---|---|---|---|
| X-01 | P1 | [Private upstream feeds and package tokens](#x-01) | MVP | 24 (tokens), 02 (auth refs) |
| X-02 | P1 | [Go GOPROXY adapter](#x-02) | MVP | 15 |
| X-03 | P1 | [PostgreSQL persistence and metadata conformance suite](#x-03) | MVP | 26, F-05, F-07 |
| X-04 | P1 | [Security hardening](#x-04) | MVP | 18 |
| X-05 | P1 | [Maven repository adapter](#x-05) | X-02 | 16 |
| X-06 | P1 | [npm registry adapter](#x-06) | X-02 | 17 |
| X-07 | P1 | [OIDC console login and identity providers](#x-07) | X-01 | F-08, 24 |
| X-08 | P1 | [Audit trail](#x-08) | 009 | 25 |
| X-09 | P1 | [Pinning and retention exceptions](#x-09) | 012 | 20 |
| X-10 | P1 | [Offline mode and stale-if-error](#x-10) | 006 | 21 |
| X-11 | P1 | [Lockfile import and prewarming](#x-11) | 007, X-09 | 22 |
| X-12 | P1 | [Metrics export and bandwidth analytics](#x-12) | 011 | 23 |
| X-13 | P1 | [Contributor experience and nuget.org publishing](#x-13) | MVP | 35, F-07 (templates) |
| X-14 | P2 | [Multi-upstream registries](#x-14) | X-01 | — |
| X-15 | P2 | [S3-compatible and Azure Blob storage + artifact conformance suite](#x-15) | 012 | 27, F-06, F-07 |
| X-16 | P2 | [PyPI Simple API adapter](#x-16) | X-02 | 28 |
| X-17 | P2 | [Cargo sparse registry adapter](#x-17) | X-02 | 29 |
| X-18 | P2 | [Supply-chain cooldown and quarantine](#x-18) | X-04 | 30 |
| X-19 | P2 | [Vulnerability visibility](#x-19) | 008, 014 | 31 |
| X-20 | P2 | [CI integrations and preflight checks](#x-20) | X-01, X-11 | 32 |
| X-21 | P2 | [Integrity audit, backup and disaster restore](#x-21) | 012 | 33 |
| X-22 | P3 | [Multi-instance coordination and HA](#x-22) | X-03, X-15 | 34 |

## MVP specifications

<a id="spec-001"></a>
### 001: Pipeline and solution skeleton

**Outcome:** From the first merge, every PR is gated by CI and every merged spec produces a downloadable alpha release, even though Hoard only answers a health check.
**Acceptance focus:** .NET 10 solution with the projects the first specs need (Host, Core, Hosting, Abstractions, test projects) and no speculative ones; architecture tests fail a forbidden reference (e.g. Core → a provider); a minimal Host serves `/health`; PR workflow runs build, tests and architecture checks; tag workflow publishes a multi-arch GHCR image, self-contained binaries for four RIDs with checksums, packages to GitHub Packages, and a GitHub prerelease; the binary and container both start and answer `/health` in CI smoke tests.
**Out of scope:** Configuration, storage, protocols, console, provider contracts beyond what the architecture test needs.

```text
/speckit-specify Set up Hoard's delivery pipeline and minimal solution skeleton so that from the first merge every pull request is gated by GitHub Actions (build, tests, architecture dependency checks) and every merged spec can be released as an alpha. A release tag v0.0.N-alpha on a main commit must publish a multi-arch (amd64, arm64) container image to GHCR, self-contained executables for linux-x64, linux-arm64, osx-arm64 and win-x64 with SHA-256 checksums, changed NuGet packages to GitHub Packages, and a GitHub prerelease with notes. The only runtime behavior is a minimal Hoard host answering a health endpoint; CI must smoke-test both the container and a native binary. Include architecture tests proving Hoard.Core cannot reference concrete providers or EF Core. Create only the projects this needs; no provider contracts, storage, configuration or UI. Cover failure cases: a tag not on main, a failing test, an already-published package version.
```

<a id="spec-002"></a>
### 002: Host composition, configuration and deployment profiles

**Outcome:** An operator starts Hoard as one process or container, with validated configuration, safe defaults and clear startup diagnostics.
**Acceptance focus:** Loopback by default; non-loopback binding is refused until admin authentication exists (spec 009 lifts this); config precedence command line > environment > file; unknown or missing modules and malformed options fail startup with actionable messages and no insecure fallback; enabled modules map routes, disabled ones map none; readiness reflects module initialization; persistent data directory; clean shutdown.
**Out of scope:** Management API, registries, any provider beyond what is needed to prove composition.

```text
/speckit-specify As a developer or operator, I want to start Hoard as a single process or container with a persistent data directory and get clear feedback about whether it started correctly. Hoard is composed at build time from modules; configuration (command line over environment over file) selects which installed modules are enabled. Specify safe defaults (loopback only; refuse non-loopback binding until admin authentication exists), startup validation (unknown or missing modules, malformed options, duplicate modules fail fast with actionable diagnostics and never fall back to insecure behavior), enabled versus disabled modules (disabled modules expose no routes or background work), readiness versus liveness, restart semantics and clean shutdown. Secrets in configuration are never logged. Include first-run, restart, invalid-config and attempted network-exposure scenarios. Keep module contracts minimal: only what this spec's acceptance tests need.
```

<a id="spec-003"></a>
### 003: SQLite metadata store

**Outcome:** Hoard remembers registries, packages, artifacts and cache entries across restarts and upgrades without any external database.
**Acceptance focus:** SQLite (WAL) metadata provider with EF Core private to the provider; restart retains entries; unique constraints reject collisions; forward-only migrations with an automatic pre-migration backup; interrupted migration recovery documented; Core consumes a minimal repository contract, never `DbContext`.
**Out of scope:** PostgreSQL, a shared conformance suite (X-03), request activity records (011).

```text
/speckit-specify Specify Hoard's persistent metadata for a personal, dependency-free installation using an embedded SQLite database: registries, canonical package identities, artifact references (by SHA-256 digest) and HTTP cache entries keyed by registry and canonical request. Define consistency rules (a cache entry never references an uncommitted artifact; uniqueness of identities), behavior across restarts, forward-only schema migrations with an automatic backup before migrating, recovery from an interrupted migration, concurrent-write behavior within one process, and data-directory expectations (SQLite must not live on a network filesystem). The cache engine must use a small storage-neutral contract and never depend on the database technology. Do not build a second database provider or a generic conformance framework.
```

<a id="spec-004"></a>
### 004: Filesystem artifact store

**Outcome:** Package files are stored once by content, written safely, and never served while incomplete.
**Acceptance focus:** Content-addressed SHA-256 storage; staged writes on the same volume with atomic visibility; identical bytes deduplicate; length and digest verified before commit; no path traversal; abandoned staging cleaned on restart; streaming read without full buffering; disk-full and interruption leave no visible partial blob; deletion that tolerates open readers (including Windows).
**Out of scope:** Object storage (X-15), eviction policy (012).

```text
/speckit-specify Specify reliable local artifact storage for Hoard where package files are streamed to a staging area, verified (length and SHA-256), atomically committed under their content digest, shared when identical, and never readable while incomplete. Define behavior on disk-full, interrupted writes, corrupted staged data, duplicate content, concurrent writers of the same digest, deletion while a reader is streaming (including Windows file-locking semantics), and restart cleanup of abandoned staging files. Large files must stream with bounded memory. Keep the storage contract minimal and shaped by this filesystem implementation; object stores are a later spec.
```

<a id="spec-005"></a>
### 005: Registries and safe upstream access

**Outcome:** Hoard knows which upstream registries it may contact, and can only ever contact those.
**Acceptance focus:** Registries persisted in the metadata DB, seeded from configuration on first run only; one public upstream per registry; permitted upstream hosts per registry; shared upstream HTTP client blocks unconfigured hosts, redirects to unapproved or private/link-local/metadata addresses and DNS-rebinding; timeouts, response-size limits and retry with backoff; no credentials or auth headers forwarded upstream; secret-free logging.
**Out of scope:** Registry CRUD API/UI (010), private feeds (X-01).

```text
/speckit-specify Specify how Hoard defines upstream registries and guarantees it only contacts approved upstream hosts. Registries (unique ID, ecosystem, one public upstream URL, permitted hosts, enabled flag) are stored in the metadata database and may be seeded from configuration on first run only; afterwards the database is the source of truth. All upstream traffic goes through one shared client that refuses unconfigured hosts, validates redirects and resolved addresses against private, link-local and cloud-metadata ranges (including DNS rebinding), enforces timeouts and response-size limits, retries transient failures with bounded backoff, and never forwards client credentials upstream. Cover invalid URLs, duplicate IDs, disabled registries, unreachable upstreams and adversarial redirect cases. Management of registries through an API or UI is a later spec.
```

<a id="spec-006"></a>
### 006: Cache engine: freshness, single-flight and streaming

**Outcome:** One shared engine gives every protocol correct hits, misses, revalidation and failure behavior, and fetches each cold artifact from upstream only once.
**Acceptance focus:** Fresh immutable hits make zero upstream calls; stale metadata revalidates conditionally (ETag/Last-Modified); negative caching of 404/410 with bounded TTL; 401/403 never shared-cached; response-varying dimensions in cache keys; per-key single-flight so 10 concurrent cold requests cause one upstream transfer; waiters survive one client's disconnect; failures release waiters and allow retry; bounded memory streaming; inbound rate limits; written test-first.
**Out of scope:** Offline mode and stale-if-error policies (X-10), multi-node coordination (X-22).

```text
/speckit-specify Specify Hoard's shared pull-through cache engine used by every protocol adapter: fresh hits served locally with no upstream call, first misses fetched once and stored through verified atomic storage, mutable metadata versus immutable artifacts with separate freshness policies, conditional revalidation, bounded negative caching of not-found results, no shared caching of 401/403, and cache keys that prevent one registry or request variant being served for another. When many clients request the same uncached artifact at once, exactly one upstream transfer occurs per instance; one client disconnecting must not abort the others; failures and timeouts release all waiters consistently and permit retry. Large content streams with bounded memory. Inbound per-client rate limits and upstream concurrency limits prevent floods of unknown names amplifying upstream. Include concurrency, cancellation, timeout, corruption and recovery acceptance tests.
```

<a id="spec-007"></a>
### 007: NuGet restore through Hoard + real-client harness

**Outcome:** A developer adds Hoard as a NuGet source and `dotnet restore` works, faster the second time. **This is the walking skeleton's first real value.**
**Acceptance focus:** NuGet protocol module: local service index advertising only supported resources; PackageBaseAddress version index and `.nupkg` download; package ID/version normalization; every advertised URL stays in Hoard; real-client harness with a controlled upstream asserts cold restore (misses), warm restore after clearing client caches (hits, no repeat upstream artifact fetch), transitive restore, 10 concurrent cold requests → one upstream fetch, interrupted upstream transfer leaves no entry, upstream outage behavior; runs in CI.
**Out of scope:** Registration metadata, audit, search (008); management API (009).

```text
/speckit-specify Specify a NuGet v3 pull-through cache that developers add as an ordinary source (NuGet.Config or dotnet nuget add source) so unmodified dotnet restore and dotnet add package fetch direct and transitive packages through Hoard. The local service index must advertise only resources Hoard supports, and every advertised URL must route through Hoard. Support package version listing and .nupkg downloads with correct package ID and version normalization and URL encoding. Include an automated harness that runs the real dotnet client against Hoard and a controlled test upstream, asserting returned packages and upstream request counts: cold restore, warm restore after clearing only client-side caches, transitive restore, concurrent cold requests producing one upstream fetch, interrupted upstream transfer, bad versions and upstream outage. Tests run in CI.
```

<a id="spec-008"></a>
### 008: NuGet compatibility coverage

**Outcome:** Everyday NuGet workflows beyond basic restore work through Hoard, and unsupported ones fail honestly.
**Acceptance focus:** Registration metadata (including gzip/SemVer 2.0 hives as needed) with local URLs; `VulnerabilityInfo` proxied so default NuGet Audit restores emit no audit-source warnings; prerelease/SemVer 2.0/casing edge cases; HEAD behavior where clients need it; decision and implementation (or explicit non-advertisement) for search/autocomplete; published compatibility matrix of tested `dotnet` SDK versions.
**Out of scope:** Private feeds (X-01), vulnerability UI (X-19).

```text
/speckit-specify Specify complete NuGet consumption through Hoard beyond basic package download: registration metadata required by modern dotnet tooling with all URLs pointing back to Hoard, the vulnerability information resource so restores with default NuGet Audit settings produce no missing-audit-source warnings (which fail warnings-as-errors builds), prerelease and SemVer 2.0 versions, casing and normalization edge cases, and HEAD requests where clients use them. Decide whether search and autocomplete are supported; if not, they must not be advertised. Publish a compatibility matrix of tested dotnet SDK versions and supported and unsupported NuGet v3 resources, all verified with the real-client harness.
```

<a id="spec-009"></a>
### 009: Management API and admin authentication

**Outcome:** Operators and the console have a stable, documented API, and a network-exposed Hoard cannot be administered anonymously.
**Acceptance focus:** Versioned `/api/v1` with OpenAPI and generated TypeScript types; stable error envelope; cursor pagination; health, overview, packages and module/capability endpoints (truthful: installed vs enabled vs healthy); bootstrap admin token generated on first run, shown once, stored hashed, rotatable via a documented recovery path; non-loopback binding allowed once configured and requiring the token for management; anonymous network package reads only by explicit opt-in; privileged actions rejected without the token.
**Out of scope:** OIDC (X-07), package tokens (X-01), audit trail (X-08).

```text
/speckit-specify Specify a stable, versioned management API for Hoard used by operators and the web console: health and readiness, an overview summary, a searchable paginated package and artifact catalog, and truthful module and capability information (installed versus enabled versus healthy, without secrets). Publish an OpenAPI description with generated TypeScript client types, a consistent error format and cursor-based pagination. Introduce built-in admin authentication: on first run Hoard generates an admin token, shows it once, stores only a hash, and supports rotation and documented recovery if it is lost. When bound beyond loopback, every management endpoint requires the token, and anonymous package reads are allowed only if explicitly enabled. Cover unauthenticated access attempts, token rotation, lost token recovery and backward-compatible versioning.
```

<a id="spec-010"></a>
### 010: Registry management

**Outcome:** Operators add, edit, test, disable and remove registries without editing files or restarting.
**Acceptance focus:** Admin-only create/update/disable/delete with validation (ID collisions, invalid URLs, unapproved hosts); connectivity test with safe error feedback; defined impact of changes on existing cache entries and on client URLs; delete requires confirmation; all changes apply without restart.
**Out of scope:** Credentials and private feeds (X-01), multiple upstreams per registry (X-14).

```text
/speckit-specify Specify how Hoard administrators manage upstream registries at runtime through the management API: create, edit, enable, disable and delete public registries, and test upstream connectivity with safe, informative feedback. Changes take effect without restarting. Define validation (duplicate IDs, invalid or unapproved URLs), what happens to cached entries and to client-facing URLs when a registry is changed, disabled or deleted, and confirmation for destructive changes. Only administrators may make changes. Credentials and private feeds are out of scope.
```

<a id="spec-011"></a>
### 011: Request activity, statistics and telemetry

**Outcome:** Hoard records what it serves so operators (and the console) can see hits, misses, bytes and latency, live and over time.
**Acceptance focus:** Per-request records (registry, result, status, bytes, durations) written asynchronously in batches with bounded retention; time-bucketed aggregates; last-access tracking that feeds LRU without per-hit DB writes; paginated activity API and a Server-Sent Events live stream; OpenTelemetry traces/metrics with low-cardinality labels; no credentials or sensitive URLs recorded; acceptable loss of buffered data on crash is defined.
**Out of scope:** Prometheus export and savings analytics (X-12).

```text
/speckit-specify Specify how Hoard records and exposes request activity: every served request produces a record (time, registry, ecosystem, hit or miss, status, bytes served and fetched, durations) written asynchronously in batches so the request path is not slowed, with bounded retention and time-bucketed aggregates for hit rate, bandwidth and latency. Track last access per artifact for eviction without a database write per hit. Expose a paginated activity API and a live stream of requests for the console, plus OpenTelemetry traces and low-cardinality metrics. Never record credentials or sensitive URLs. Define how much buffered data may be lost on a crash, and cover busy CI traffic.
```

<a id="spec-012"></a>
### 012: Disk quota, LRU eviction and garbage collection

**Outcome:** A cache on a laptop or team server never grows without bound, and reclaiming space never breaks an active download.
**Acceptance focus:** Default disk cap (configurable); least-recently-used eviction of cache entries once over the cap; blob reclamation separate from entry eviction and safe for active readers (deferred deletion on Windows); orphan reconciliation after crash; low-disk safeguards; admin-triggered eviction of a selected package/version with confirmation, followed by refetch; reclaimed-space metrics.
**Out of scope:** Pinning (X-09), per-registry quotas and policy rules beyond the cap.

```text
/speckit-specify Specify how Hoard keeps its storage within a disk budget. A fresh install has a sensible default cap that operators can change; when usage exceeds it, Hoard evicts least-recently-used cache entries and reclaims their unreferenced artifacts without ever deleting content that is being streamed (including on Windows, where open files cannot be deleted) or leaving metadata pointing at missing blobs. After a crash, orphaned blobs and dangling records are reconciled. Administrators can see usage, manually evict a package or version with confirmation, and confirm it is refetched on next request. Include low-disk behavior, concurrent eviction and download, and accurate reclaimed-space reporting.
```

<a id="spec-013"></a>
### 013: Console shell and design system

**Outcome:** A distinctive, accessible management console, served by Hoard itself, that later screens drop into.
**Acceptance focus:** Next.js latest stable, App Router, TypeScript, Tailwind, shadcn/ui with Hoard design tokens; static export served by the .NET host (no Node runtime) with routing that works for arbitrary package IDs (no build-time dynamic params) and behind a reverse proxy; admin-token sign-in; navigation Overview · Packages · Activity · Registries · Storage · Settings; dark/light; loading/empty/error states; keyboard and screen-reader support; reduced motion; component and Playwright tests in CI.
**Out of scope:** Screen content (014, 015).

```text
/speckit-specify Specify the visual and interaction foundation of Hoard's management console: a distinctive, modern developer-tool look with cohesive dark and light themes, responsive layouts from laptop to large monitor, accessible keyboard and screen-reader navigation, reduced-motion support, and reliable loading, empty and error states. The console is served by the Hoard server itself with no separate Node runtime, must handle arbitrary package names in its routes, and must work behind a reverse proxy. Users sign in with the admin token when Hoard requires it. Provide the navigation shell (Overview, Packages, Activity, Registries, Storage, Settings) with placeholder content; target WCAG 2.2 AA; include automated component and browser tests.
```

<a id="spec-014"></a>
### 014: Overview, packages and live activity

**Outcome:** At a glance users can tell whether the cache is working and what it is saving, find any cached package, and watch requests live.
**Acceptance focus:** Overview KPIs (hit rate, bandwidth saved, storage, upstream health) and time-series charts; package explorer with ecosystem/registry filters, search, version and artifact detail, digest, first/last fetched and used; live request stream with filters and per-request diagnostics; drilldowns; empty states that guide to setup; numbers reconcile with test traffic.
**Out of scope:** Registry and storage management screens (015).

```text
/speckit-specify Specify Hoard's operational screens. The overview immediately shows whether Hoard is working and why it matters: hit rate, bandwidth saved, stored bytes, upstream health and trends over selectable time ranges. The package explorer lets users search and filter cached packages by registry and version and inspect artifacts, digests and when they were fetched and last used. The activity view shows a live stream of requests with hit, miss, status and timing, filters, and per-request detail. Describe drilldowns, empty states that guide new users to setup, error states, and how displayed numbers are defined so they match real traffic.
```

<a id="spec-015"></a>
### 015: Registries, storage, settings and setup assistant

**Outcome:** Operators manage registries and storage from the console, and a developer points `dotnet` at Hoard in minutes.
**Acceptance focus:** Registries screen (add/edit/test/disable/delete with confirmation); storage screen (usage visualization, cap, eviction candidates, manual eviction); settings (deployment and security posture, admin token rotation, diagnostics); setup assistant with copyable `NuGet.Config` and `dotnet nuget add source` snippets using the real public base URL, OS-appropriate variants, and a verification step proving traffic goes through Hoard; first-run guidance.
**Out of scope:** Other ecosystems' snippets until their adapters exist.

```text
/speckit-specify Specify the remaining console screens. Registries: add, edit, test connectivity, disable and delete registries with clear validation and confirmation. Storage: visualize usage against the disk cap, show eviction candidates and evict selected packages with confirmation. Settings: show the deployment and security posture (bindings, anonymous-read setting), rotate the admin token and view diagnostics. Setup assistant: a guided first-run flow that gives copyable, OS-appropriate NuGet.Config and dotnet CLI snippets using Hoard's actual public URL, explains team-hosting security considerations, and verifies that a restore actually went through Hoard. Additional ecosystems appear only when their adapters exist.
```

<a id="spec-016"></a>
### 016: Distribution, first run and upgrades

**Outcome:** Anyone can install Hoard on any MVP platform in under five minutes and upgrade without losing their cache.
**Acceptance focus:** Documented install for the container (persistent volume contract) and native binaries on Linux, macOS arm64 and Windows x64; first run initializes data, shows the admin token and setup URL; upgrade between alpha versions preserves cache with pre-migration backup; signal handling and graceful shutdown on each platform; team single-node deployment guide (TLS via reverse proxy, anonymous-read opt-in, SQLite on local disk only); quickstart timed in CI or by checklist.
**Out of scope:** PostgreSQL, HA, nuget.org publishing.

```text
/speckit-specify Specify installation and lifecycle for individual developers and small teams on every MVP platform: run the Hoard container with a persistent volume, or download a self-contained executable for Linux, macOS (Apple silicon) or Windows. First run initializes storage, displays the admin token once and points to the console's setup assistant. Upgrading to a newer release preserves all cached data, backing up metadata before migrations, with documented rollback. Include graceful shutdown on each platform, a single-node team deployment guide (TLS via reverse proxy, anonymous-read opt-in, local-disk-only SQLite), common error recovery, and a quickstart that gets a first cached restore in under five minutes. No external database, cache service or Node runtime may be required.
```

## Post-MVP specifications

<a id="x-01"></a>
### X-01: Private upstream feeds and package tokens

Credentialed upstreams (Azure Artifacts, GitHub Packages, private NuGet servers); upstream credentials encrypted at rest with ASP.NET Core Data Protection and never exposed through the API; Hoard package tokens (scoped per registry, revocable, sent by clients as basic-auth password) required to read private registries; cache partitioning so private content is never served to unauthorized clients.

```text
/speckit-specify Specify support for private, credentialed upstream feeds in Hoard. Administrators add a registry with upstream credentials, which are encrypted at rest, never returned by the API or logged, and can be rotated. Content fetched from a private registry may only be served to clients presenting a Hoard package token scoped to that registry; tokens are issued, listed and revoked by administrators and work with standard NuGet credential configuration. Public registries keep their existing access rules. Cover leaked or revoked tokens, rotated upstream credentials, upstream 401/403, and guarantees that private content never reaches unauthorized clients.
```

<a id="x-02"></a>
### X-02: Go GOPROXY adapter

```text
/speckit-specify Specify a native Go module proxy so developers set GOPROXY to Hoard and use normal go build and go mod download. Support list, info, mod and zip endpoints, module path escaping, upstream outages and clear guidance on checksum database verification and private modules; caching must never substitute for checksum verification. Prove it with real go client tests in CI, and update the setup assistant.
```

<a id="x-03"></a>
### X-03: PostgreSQL persistence and metadata conformance suite

The second metadata implementation: qualifies the metadata contract and introduces the shared conformance suite run against both SQLite and PostgreSQL.

```text
/speckit-specify Specify optional PostgreSQL metadata persistence for team deployments while SQLite remains fully supported. Introduce a shared metadata conformance suite that both providers must pass (consistency, uniqueness, concurrency, migrations, recovery), version the metadata contract, and define migration of an existing SQLite installation to PostgreSQL, backup and restore, and operator visibility. PostgreSQL must never be required for ordinary installations.
```

<a id="x-04"></a>
### X-04: Security hardening

```text
/speckit-specify Specify hardening of Hoard beyond the MVP baseline: adversarial test suites for SSRF and redirect abuse, request and header limits, archive and path handling, trusted reverse-proxy header handling, dependency vulnerability and license scanning in CI, a responsible-disclosure policy, and a documented threat model for both deployment profiles.
```

<a id="x-05"></a>
### X-05: Maven repository adapter

```text
/speckit-specify Specify a Maven-compatible repository proxy that Maven users configure as a mirror in settings.xml to resolve dependencies, transitive POMs, parent POMs and plugins through Hoard. Distinguish immutable releases from mutable snapshots and maven-metadata.xml freshness, handle checksum files, missing dependencies and outages, with real Maven client tests and Gradle where feasible.
```

<a id="x-06"></a>
### X-06: npm registry adapter

```text
/speckit-specify Specify an npm-compatible pull-through registry for ordinary npm install and npm ci via .npmrc. Scoped and unscoped packages, full and abbreviated metadata, tarball URLs rewritten to Hoard, lockfile integrity preserved, metadata freshness and errors, with real npm client tests.
```

<a id="x-07"></a>
### X-07: OIDC console login and identity providers

```text
/speckit-specify Specify OIDC sign-in for the Hoard console (generic OIDC with Microsoft Entra convenience) alongside the built-in admin token, with roles (viewer, operator, admin) mapped from identity claims, normalized authorization shared by all schemes, fail-closed behavior if a provider is misconfigured, and a break-glass path. Package-client tokens remain separate from browser login.
```

<a id="x-08"></a>
### X-08: Audit trail

```text
/speckit-specify Specify an audit history of security-relevant changes in Hoard (registry changes, token issuance and revocation, evictions, settings changes) showing who changed what and when, without recording secrets, with retention, filtering and export.
```

<a id="x-09"></a>
### X-09: Pinning and retention exceptions

```text
/speckit-specify Specify cache pinning so teams retain critical package versions despite eviction: pin and unpin packages or versions, see pinned storage cost, define precedence against the disk cap and behavior when pinned content alone exceeds it.
```

<a id="x-10"></a>
### X-10: Offline mode and stale-if-error

```text
/speckit-specify Specify dependable offline and upstream-outage behavior: a manual offline switch, serving cached immutable artifacts and stale metadata under explicit per-ecosystem policy with visible warnings, clear failures for uncached content, last-successful-refresh visibility and predictable reconnect.
```

<a id="x-11"></a>
### X-11: Lockfile import and prewarming

```text
/speckit-specify Specify lockfile-driven cache warming, starting with NuGet packages.lock.json: users supply a lockfile, preview what will be fetched and its size, then queue, follow, cancel and retry a bounded prefetch. Never execute package scripts or trust URLs from the file; report unsupported files and partial failures. Other ecosystems add parsers as their adapters exist.
```

<a id="x-12"></a>
### X-12: Metrics export and bandwidth analytics

```text
/speckit-specify Specify advanced observability: a Prometheus endpoint restricted on shared networks, bandwidth and time-saved analytics that clearly separate measured from estimated values, per-ecosystem and per-registry breakdowns and trace correlation for troubleshooting.
```

<a id="x-13"></a>
### X-13: Contributor experience and nuget.org publishing

```text
/speckit-specify Specify a contributor-friendly open-source experience: CONTRIBUTING guide with a measurable new-contributor path, issue and PR templates, security policy, provider author templates backed by the conformance suites, a compatibility matrix, SBOM and license notices in releases, and publishing stable provider packages to nuget.org.
```

<a id="x-14"></a>
### X-14: Multi-upstream registries

```text
/speckit-specify Specify registries that combine several upstreams behind one Hoard URL with an explicit priority order, including protections against dependency confusion (package ID reservations or source mapping), clear provenance of which upstream served each package and behavior when upstreams disagree.
```

<a id="x-15"></a>
### X-15: S3-compatible and Azure Blob storage + artifact conformance suite

```text
/speckit-specify Specify optional object-storage-backed artifact storage (S3-compatible and Azure Blob) with integrity, deduplication, streaming and deletion equivalent to local disk despite the lack of atomic rename. Introduce a shared artifact conformance suite run against filesystem and object stores, and cover credential setup, misconfiguration, outages and safe cleanup.
```

<a id="x-16"></a>
### X-16: PyPI Simple API adapter

```text
/speckit-specify Specify a PyPI-compatible cache for unmodified pip using the Simple Repository API (HTML and JSON), normalized project names, wheel and sdist links rewritten to Hoard with integrity hashes, transitive dependencies, with real pip client tests.
```

<a id="x-17"></a>
### X-17: Cargo sparse registry adapter

```text
/speckit-specify Specify a Cargo sparse-registry pull-through cache: index entries and config.json download URL rewritten to Hoard, crate archives with checksum verification, no upstream bypass, git index explicitly unsupported, with real cargo client tests.
```

<a id="x-18"></a>
### X-18: Supply-chain cooldown and quarantine

```text
/speckit-specify Specify optional supply-chain controls: a cooldown that withholds newly published versions for a configured period, quarantine of artifacts failing integrity checks, informative client errors, audited administrator overrides and stated limits of upstream publish-time data.
```

<a id="x-19"></a>
### X-19: Vulnerability visibility

```text
/speckit-specify Specify optional vulnerability visibility for cached packages using advisory data (starting with NuGet's vulnerability resource), shown on package detail with severity, source, freshness and links, never blocking restores by default and disclosing stale or missing data.
```

<a id="x-20"></a>
### X-20: CI integrations and preflight checks

```text
/speckit-specify Specify CI onboarding for GitHub Actions and Azure Pipelines: example workflows consuming Hoard, secure token handling, optional lockfile warmup, a preflight check proving traffic routes through Hoard, and before/after restore measurements.
```

<a id="x-21"></a>
### X-21: Integrity audit, backup and disaster restore

```text
/speckit-specify Specify operator recovery tools: scan for missing, corrupt or orphaned artifacts and dangling references with dry-run reports, consistent backup of metadata plus artifacts, restore with verification, and audited repair actions.
```

<a id="x-22"></a>
### X-22: Multi-instance coordination and HA (P3)

```text
/speckit-specify Specify how organizations could run several Hoard instances against shared persistence and object storage without duplicate conflicting writes or corruption: distributed single-flight, background job ownership, rolling upgrades and failover, while preserving single-instance simplicity. Only pursue after single-node demand is proven.
```

## Cross-cutting acceptance matrix

| Quality area | Required evidence |
|---|---|
| Native clients | Real package-manager cold/warm and transitive tests for every advertised adapter |
| Cache correctness | Mutable/immutable rules, conditional validation, bounded negative caching, statuses/headers, variant isolation |
| Reliability | Concurrent same-key requests, timeouts, retries, client disconnects, crash restart, disk full, interrupted fetches |
| Security | Restricted upstreams incl. redirects/DNS, safe logging, admin token when exposed, no credential leakage |
| Data integrity | SHA-256 validation, no partial hits, no dangling references, corruption detection |
| UX | Dark/light, responsive, keyboard, screen-reader labels, empty/loading/error states, setup snippets that work |
| Operations | Startup diagnostics, telemetry, backups before migration, eviction, documented restore |
| Delivery | CI green on every PR; each merged spec released as an alpha with image, binaries and packages |
| Boundaries | Architecture tests: no forbidden references; disabled modules expose no routes; vertical slices for endpoints |

## Deliberate exclusions (unless separately approved)

Package publishing and private hosted registries; enterprise SCIM and artifact-governance suites; air-gap replication; runtime plugin loading or marketplace; transparent interception of traffic without package-manager configuration; multi-tenant isolation within one instance; security certification claims; multi-instance HA before X-22.

## Open decisions

1. Exact default disk cap and freshness defaults per NuGet endpoint (settle in specs 006 and 012).
2. NuGet search/autocomplete in MVP or later (settle in spec 008).
3. Hoard namespace, domain and trademark clearance.
