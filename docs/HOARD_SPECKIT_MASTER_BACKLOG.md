# Hoard — Spec Kit Backlog

**Status:** v0.3 (2026-10-31) · **Product:** Hoard · **License:** MIT · **Source baseline:** [Charter](PRODUCT_CHARTER.md), [Vision](PRODUCT_VISION.md), [Product spec](PRODUCT_SPEC.md), [Architecture](ARCHITECTURE.md)

> **Purpose.** The ordered list of feature specifications for Hoard, with a ready-to-paste Spec Kit prompt for each. None of these specifications exist yet. Order is **walking skeleton first**: the pipeline ships on day one, a real cached `dotnet restore` arrives by spec 007, every capability arrives in the spec that first uses it, and provider contracts are qualified only when a second implementation exists.

## How to use this document

Run `/drive-speckit`: it advances one step per invocation, using the prompts below verbatim, and records progress in [spec-progress.md](spec-progress.md). The manual equivalent:

1. Run the [constitution prompt](#constitution-prompt) once with `/speckit-constitution`.
2. Take MVP specs **in order**. Paste one prompt into `/speckit-specify`, then run `/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`, `/speckit-analyze` and `/speckit-implement`.
3. Each spec is one `feature/NNN-<slug>` branch and one PR. MVP spec numbers below are the spec directory numbers (`specs/NNN-<slug>`, set via `SPECIFY_FEATURE_DIRECTORY`). Post-MVP items carry `X-` IDs and take the next free number when started.
4. A spec is done when its acceptance focus passes in CI and the maintainer merges the PR. The merge commit is then tagged `v0.0.N-alpha` ([Releasing](RELEASING.md)).
5. Respect each spec's **Out of scope** line. Work that belongs to a later spec stays there.

## Shared guardrails (apply to every specification)

- Hoard is a **self-hosted pull-through cache**, not a publishing registry. No paywalls, no required SaaS.
- **One product, two profiles**: personal (loopback, SQLite + filesystem, nothing else to run) and team (network-exposed with an explicit TLS posture, built-in admin token for management). Package reads from public registries are anonymous in the MVP. A personal install stays lightweight.
- **MVP is NuGet only, public upstreams only, one upstream per registry.** Private feeds, feed merging and other ecosystems are post-MVP.
- **Security and integrity are never deferred**: no partial blobs as hits, verify content, SSRF-safe upstream access, protected management whenever network-exposed, no secrets in logs, from the first network-capable spec.
- **No speculative abstraction.** Module boundaries are enforced by architecture tests from spec 001; contracts grow only as a real implementation needs them.
- **Honest protocol support**: never advertise unsupported resources; preserve status codes, headers, local URLs and checksum behavior.
- **Docs reflect shipped behavior** at every merge.

## Constitution prompt

```text
/speckit-constitution Create the Hoard constitution. Hoard is an MIT-licensed, self-hosted pull-through package cache: native package managers use it through their normal configuration; it fetches from upstream once, verifies, and serves locally. NuGet first. Keep the constitution to durable principles; scope, platforms and release mechanics live in docs/PRODUCT_CHARTER.md and docs/RELEASING.md and must not be duplicated here.

Principles:
I. Cache, not registry. Transparent pull-through caching for native clients; no package publishing, no custom client tooling, no paid tiers.
II. Trustworthy bytes (non-negotiable). Content is staged, checksum-verified and atomically committed before it becomes visible; partial, corrupt or unverified content is never served as a hit; a cache entry never references an uncommitted blob.
III. Secure by default (non-negotiable). Loopback binding by default and network exposure only by explicit choice; once management exists, network-exposed management always requires authentication; reads of private content always require authorization; upstream access only to approved hosts with SSRF, redirect and DNS checks; secrets never logged or stored in plaintext. Security baselines ship with the first spec that needs them, never later.
IV. Native protocol fidelity. Protocol behavior is proven with real package-manager clients; only supported resources are advertised; every advertised URL routes through Hoard.
V. Simplicity and YAGNI. Build the smallest thing that passes the acceptance criteria. No abstraction, contract, extension point, schema or configuration option without a current consumer; a capability arrives in the spec that first uses it. Single process, single node until demand proves otherwise.
VI. Enforced modular boundaries. ASP.NET Core composable modular monolith. Hoard.Core never references concrete providers or EF Core; EF Core lives only in relational persistence providers. Management and protocol endpoints are vertical slices, each endpoint delivered with the feature that uses it; shared invariants (cache keys and variant partitioning, freshness, SSRF policy, verified atomic storage, streaming, single-flight, eviction safety) live once in shared core services. Architecture tests enforce dependency rules. Built-in DI and Minimal APIs; no MediatR.
VII. Test-first where correctness lives. Cache engine and protocol work start from a failing test; protocol specs include real-client end-to-end tests; console work includes component and browser tests. No spec merges without its tests.
VIII. Small specs, continuous delivery. Each spec is deliverable in one reviewable PR; split anything larger. CI gates every PR (build, tests, architecture checks). CI/CD exists from the first spec so value is visible early: every merged spec produces a published alpha release.
IX. Truthful documentation. README and affected docs are updated in the same PR and describe shipped behavior, distinguishing working from planned.
X. A polished, accessible console. Statically exported web console served by the Hoard host with no separate Node runtime; WCAG 2.2 AA target, dark and light themes, reduced-motion support.

Also record: technology stack (.NET 10, ASP.NET Core, SQLite, filesystem storage, OpenTelemetry, OpenAPI, Next.js App Router with TypeScript, Tailwind CSS and shadcn/ui as static export, GitHub Actions); development workflow (one branch and PR per spec, the maintainer merges, releases per docs/RELEASING.md; forward-only schema migrations with automatic pre-migration backup); governance (amendments by PR with a semantic version bump; the constitution supersedes other practice; plan reviews check compliance).
```

## MVP inventory

| Spec | Title | Depends on | Folds in (v0.2 IDs) |
|---|---|---|---|
| 001 | [Pipeline and solution skeleton](#spec-001) | — | F-01, 35 (part) |
| 002 | [Host, configuration and deployment profiles](#spec-002) | 001 | F-03, 01 |
| 003 | [Metadata store foundation](#spec-003) | 002 | F-05, 03 |
| 004 | [Filesystem artifact store](#spec-004) | 002 | F-06, 04 |
| 005 | [Registries and safe upstream access](#spec-005) | 003 | 02, 18 (baseline) |
| 006 | [Cache core: hits, misses, single-flight and streaming](#spec-006) | 004, 005 | 05 (part), 06 |
| 007 | [NuGet restore through Hoard + real-client harness](#spec-007) | 006 | 07, 09, F-02 |
| 008 | [NuGet compatibility coverage](#spec-008) | 007 | 08 |
| 009 | [Revalidation, negative caching and rate limits](#spec-009) | 007 | 05 (part), 18 (part) |
| 010 | [Admin authentication and management API foundation](#spec-010) | 007 | 10, 18/24 (part) |
| 011 | [Registry management API](#spec-011) | 005, 010 | 02 (management) |
| 012 | [Request activity, statistics and telemetry](#spec-012) | 006, 010 | 12 (data), 23 (part) |
| 013 | [Disk quota, LRU eviction and garbage collection](#spec-013) | 004, 010, 012 | 19 (core) |
| 014 | [Console shell and design system](#spec-014) | 010 | 11 |
| 015 | [Overview and package explorer](#spec-015) | 012, 014 | 12 |
| 016 | [Live activity view](#spec-016) | 012, 014 | 12 |
| 017 | [Registries, storage and settings screens](#spec-017) | 011, 013, 014 | 19 (UI) |
| 018 | [Setup assistant](#spec-018) | 014 | 13 |
| 019 | [Distribution, first run and upgrades](#spec-019) | 013, 018 | 14 |

**MVP gate:** all of 001–019 merged → release `v0.1.0`.

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
| X-08 | P1 | [Audit trail](#x-08) | 010 | 25 |
| X-09 | P1 | [Pinning and retention exceptions](#x-09) | 013 | 20 |
| X-10 | P1 | [Offline mode and stale-if-error](#x-10) | 009 | 21 |
| X-11 | P1 | [Lockfile import and prewarming](#x-11) | 007, X-09 | 22 |
| X-12 | P1 | [Metrics export and bandwidth analytics](#x-12) | 012 | 23 |
| X-13 | P1 | [Contributor experience and nuget.org publishing](#x-13) | MVP | 35, F-07 (templates) |
| X-14 | P2 | [Multi-upstream registries](#x-14) | X-01 | — |
| X-15 | P2 | [S3-compatible and Azure Blob storage + artifact conformance suite](#x-15) | 013 | 27, F-06, F-07 |
| X-16 | P2 | [PyPI Simple API adapter](#x-16) | X-02 | 28 |
| X-17 | P2 | [Cargo sparse registry adapter](#x-17) | X-02 | 29 |
| X-18 | P2 | [Supply-chain cooldown and quarantine](#x-18) | X-04 | 30 |
| X-19 | P2 | [Vulnerability visibility](#x-19) | 008, 015 | 31 |
| X-20 | P2 | [CI integrations and preflight checks](#x-20) | X-01, X-11 | 32 |
| X-21 | P2 | [Integrity audit, backup and disaster restore](#x-21) | 013 | 33 |
| X-22 | P3 | [Multi-instance coordination and HA](#x-22) | X-03, X-15 | 34 |
| X-23 | P1 | [Module capability discovery](#x-23) | X-02 | F-04 |

## MVP specifications

<a id="spec-001"></a>
### 001: Pipeline and solution skeleton

**Outcome:** From the first merge, every PR is gated by CI and every merged spec produces a downloadable alpha release, even though Hoard only answers a health check.
**Acceptance focus:** .NET 10 solution with only `Hoard.Host`, `Hoard.Core` and test projects; architecture tests that encode the dependency rules by naming pattern (Core must not reference `Hoard.Persistence.*`, `Hoard.Storage.*`, `Hoard.Protocols.*`, `Hoard.Identity.*` or EF Core) and fail on a deliberate violation; minimal Host serving `/health`; PR workflow runs build, tests and architecture checks; tag workflow publishes a multi-arch GHCR image, self-contained binaries for the MVP platforms with checksums, any packable packages to GitHub Packages, and a GitHub prerelease; CI smoke-tests both the container and a native binary.
**Out of scope:** Configuration, storage, protocols, console, other projects (each is created by the spec that first needs it).

```text
/speckit-specify Set up Hoard's delivery pipeline and minimal solution skeleton so that from the first merge every pull request is gated by GitHub Actions (build, tests, architecture dependency checks) and every merged spec can be released as an alpha per docs/RELEASING.md. A release tag on a main commit must publish a multi-arch (amd64, arm64) container image to GHCR, self-contained executables for linux-x64, linux-arm64, osx-arm64 and win-x64 with SHA-256 checksums, any packable NuGet packages to GitHub Packages, and a GitHub prerelease with notes. The only runtime behavior is a minimal Hoard host answering a health endpoint; CI must smoke-test both the container and a native binary. Create only the host, core and test projects. Architecture tests must encode the dependency rules by project naming pattern (core may never reference persistence, storage, protocol or identity providers, or EF Core) and prove they fail on a violation. Cover failure cases: a tag not on main, a failing test, an already-published package version. Out of scope for this spec: configuration, storage, protocols, console, and any project not needed here.
```

<a id="spec-002"></a>
### 002: Host, configuration and deployment profiles

**Outcome:** An operator starts Hoard as one process or container with validated configuration, safe network defaults and clear startup diagnostics.
**Acceptance focus:** Loopback by default; binding beyond loopback only by explicit configuration, with a startup warning that package reads are anonymous; a non-loopback listener requires an explicit TLS posture — native HTTPS from a configured certificate or a declared TLS-terminating reverse proxy — or startup fails ([ADR-0002](adr/0002-explicit-tls-posture.md)); the container image explicitly binds all interfaces inside the container with the reverse-proxy posture declared (host exposure controlled by Docker port publishing, documented as `-p 127.0.0.1:8484:8484` for personal use); config precedence command line > environment > file; malformed or unknown options fail startup with actionable messages and no insecure fallback; persistent data directory; liveness vs readiness; secrets never logged; clean shutdown.
**Out of scope:** Module registration and enable/disable (arrives with the first real module in 007), storage, management API.

```text
/speckit-specify As a developer or operator, I want to start Hoard as a single process or container with a persistent data directory and get clear feedback about whether it started correctly. Configuration comes from command line over environment over file. Specify safe defaults: loopback only unless binding to other addresses is explicitly configured, with a clear warning that package reads are then anonymous. Binding beyond loopback also requires an explicit TLS posture: Hoard serves HTTPS from a configured certificate, or the operator declares that a TLS-terminating reverse proxy is in front; otherwise startup fails, and an invalid certificate never falls back to plain HTTP. The official container image explicitly binds all interfaces inside the container, declaring the reverse-proxy posture, so Docker port publishing works, and its documentation publishes the port to the host's loopback by default. Malformed or unknown options fail fast with actionable diagnostics and never fall back to insecure behavior. Distinguish liveness from readiness, define restart semantics and clean shutdown, and never log secrets. Include first-run, restart, invalid-config, explicit network-exposure and container scenarios. Out of scope for this spec: module registration or enable/disable machinery (no modules exist yet; it arrives with the first real module), storage and the management API.
```

<a id="spec-003"></a>
### 003: Metadata store foundation

**Outcome:** Hoard has a durable embedded database it can safely upgrade, ready for later specs to add their own data.
**Acceptance focus:** SQLite (WAL) provider with EF Core private to it; data survives restart; forward-only migrations with automatic pre-migration backup; documented recovery from an interrupted migration; refuses to start on a network filesystem where detectable and documents the rule; Core sees a minimal storage-neutral seam, never `DbContext`; the only schema is what the store itself needs (e.g. schema version), proven with a test entity.
**Out of scope:** Domain tables (registries in 005, cache entries in 006, activity in 012), PostgreSQL, conformance suites (X-03).

```text
/speckit-specify Specify Hoard's embedded metadata store for a dependency-free installation: an SQLite database in the data directory that survives restarts, applies forward-only schema migrations with an automatic backup taken before migrating, recovers predictably from an interrupted migration, handles concurrent writes within one process, and refuses or warns clearly when placed on a network filesystem. The cache engine must reach it only through a small storage-neutral seam and never depend on the database technology. This spec establishes the store and its migration mechanics only; each later feature adds the tables it needs through its own migrations. Out of scope for this spec: registry, package, cache-entry or activity tables; a second database provider; a shared conformance framework.
```

<a id="spec-004"></a>
### 004: Filesystem artifact store

**Outcome:** Package files are stored once by content, written safely, and never served while incomplete.
**Acceptance focus:** Content-addressed SHA-256 storage; staged writes on the same volume with atomic visibility; identical bytes deduplicate; length and digest verified before commit; no path traversal; abandoned staging cleaned on restart; streaming reads without full buffering; disk-full and interruption leave no visible partial blob; deletion that tolerates open readers (including Windows).
**Out of scope:** Object storage (X-15), eviction policy (013).

```text
/speckit-specify Specify reliable local artifact storage for Hoard where package files are streamed to a staging area, verified (length and SHA-256), atomically committed under their content digest, shared when identical, and never readable while incomplete. Define behavior on disk-full, interrupted writes, corrupted staged data, duplicate content, concurrent writers of the same digest, deletion while a reader is streaming (including Windows file-locking semantics), and restart cleanup of abandoned staging files. Large files must stream with bounded memory. Keep the storage contract minimal and shaped by this filesystem implementation. Out of scope for this spec: object storage and eviction policy.
```

<a id="spec-005"></a>
### 005: Registries and safe upstream access

**Outcome:** Hoard knows which upstream registries it may contact, and can only ever contact those.
**Acceptance focus:** Registry table added by this spec; registries seeded from configuration on first run only, then the DB is the source of truth; one public upstream per registry; permitted upstream hosts are the upstream's own host plus hosts a protocol adapter discovers from it (e.g. NuGet service-index resources), all validated against SSRF policy; shared upstream client blocks unconfigured hosts, unsafe redirects, private/link-local/metadata addresses and DNS rebinding; timeouts, size limits, bounded retry; client credentials never forwarded; secret-free logging.
**Out of scope:** Registry management API (011), private feeds (X-01).

```text
/speckit-specify Specify how Hoard defines upstream registries and guarantees it only contacts approved upstream hosts. A registry has a unique ID, ecosystem, one public upstream URL and an enabled flag; registries are stored in the metadata database and may be seeded from configuration on first run only, after which the database is the source of truth. The permitted hosts for a registry are its upstream host plus any hosts a protocol adapter discovers from that upstream (for example the resource hosts listed in a NuGet service index), each validated against the same safety policy. All upstream traffic goes through one shared client that refuses unapproved hosts, validates redirects and resolved addresses against private, link-local and cloud-metadata ranges (including DNS rebinding), enforces timeouts and response-size limits, retries transient failures with bounded backoff, and never forwards client credentials upstream. Cover invalid URLs, duplicate IDs, disabled registries, unreachable upstreams and adversarial redirect cases. Out of scope for this spec: managing registries through an API or UI, and private or credentialed feeds.
```

<a id="spec-006"></a>
### 006: Cache core: hits, misses, single-flight and streaming

**Outcome:** One shared engine serves cached content locally and fetches each cold item from upstream exactly once, safely.
**Acceptance focus:** Cache-entry table added by this spec; fresh hits make zero upstream calls; misses fetch through the safe upstream client into verified atomic storage before becoming visible; immutable artifacts cached indefinitely, mutable metadata with a simple TTL; cache keys include registry and request variant; 10 concurrent cold requests → one upstream transfer; one client disconnecting does not abort others; failures release all waiters and allow retry; bounded-memory streaming; written test-first against a test protocol.
**Out of scope:** Conditional revalidation, negative caching and rate limits (009); offline/stale-if-error (X-10); multi-node (X-22).

```text
/speckit-specify Specify the core of Hoard's shared pull-through cache engine that every protocol adapter will use: fresh hits served locally with no upstream call; misses fetched once through the safe upstream client and stored through verified atomic storage before any request can see them; immutable artifacts kept until evicted and mutable metadata refreshed after a simple configurable lifetime; cache keys that prevent one registry or request variant being served for another; 401 and 403 responses never cached. When many clients request the same uncached item at once, exactly one upstream transfer occurs per instance; one client disconnecting must not abort the others; failures and timeouts release all waiters consistently and permit retry. Large content streams with bounded memory. Include concurrency, cancellation, timeout, corruption and recovery acceptance tests. Out of scope for this spec: conditional revalidation, negative caching, rate limiting, offline or stale-if-error behavior and multi-node coordination.
```

<a id="spec-007"></a>
### 007: NuGet restore through Hoard + real-client harness

**Outcome:** A developer adds Hoard as a NuGet source and `dotnet restore` works, faster the second time. **This is the walking skeleton's first real value.**
**Acceptance focus:** NuGet as the first real module: module registration, enable/disable by configuration (disabled module exposes no routes) and startup failure for unknown modules, built only as far as NuGet needs; local service index advertising only supported resources; PackageBaseAddress version index and `.nupkg` download; ID/version normalization; every advertised URL stays in Hoard; real-client harness with a controlled upstream asserts cold restore, warm restore after clearing client caches (no repeat upstream artifact fetch), transitive restore, `dotnet add package --version`, 10 concurrent cold requests → one upstream fetch, interrupted transfer leaves no entry, upstream outage; NuGet Audit warnings tolerated until 008; runs in CI.
**Out of scope:** Registration metadata and `dotnet add package` without a version, audit, search (008); management API (010).
**Open decision ([ADR-0002](adr/0002-explicit-tls-posture.md)):** how advertised absolute URLs stay correct behind a `proxy`-mode TLS reverse proxy (configured public base URL or trusted forwarded headers); decide before or at 007's clarify.

```text
/speckit-specify Specify a NuGet v3 pull-through cache that developers add as an ordinary source (NuGet.Config or dotnet nuget add source) so unmodified dotnet restore fetches direct and transitive packages through Hoard, and dotnet add package works when a version is given. NuGet is Hoard's first real module: introduce module registration and enable/disable by configuration only as far as this module needs (a disabled module exposes no routes; an unknown module fails startup). The local service index must advertise only resources Hoard supports, and every advertised URL must route through Hoard. Support package version listing and .nupkg downloads with correct package ID and version normalization and URL encoding. Include an automated harness that runs the real dotnet client against Hoard and a controlled test upstream, asserting returned packages and upstream request counts: cold restore, warm restore after clearing only client-side caches, transitive restore, concurrent cold requests producing one upstream fetch, interrupted upstream transfer, bad versions and upstream outage. NuGet Audit warnings are expected until vulnerability data is proxied in the next spec. Tests run in CI. Out of scope for this spec: registration metadata (needed by dotnet add package without a version), vulnerability data, search and the management API.
```

<a id="spec-008"></a>
### 008: NuGet compatibility coverage

**Outcome:** Everyday NuGet workflows beyond restore work through Hoard, and unsupported ones fail honestly.
**Acceptance focus:** Registration metadata (gzip/SemVer 2.0 hives as needed) with local URLs, so `dotnet add package` without a version works; `VulnerabilityInfo` proxied so default NuGet Audit restores emit no audit-source warnings; prerelease/SemVer 2.0/casing edge cases; HEAD where clients need it; search/autocomplete either supported or not advertised; published compatibility matrix of tested `dotnet` SDK versions.
**Out of scope:** Private feeds (X-01), vulnerability UI (X-19).

```text
/speckit-specify Specify complete NuGet consumption through Hoard beyond restore: registration metadata required by modern dotnet tooling (including dotnet add package without a version) with all URLs pointing back to Hoard; the vulnerability information resource so restores with default NuGet Audit settings produce no missing-audit-source warnings, which otherwise fail warnings-as-errors builds; prerelease and SemVer 2.0 versions; casing and normalization edge cases; and HEAD requests where clients use them. Decide whether search and autocomplete are supported; if not, they must not be advertised. Publish a compatibility matrix of tested dotnet SDK versions and supported and unsupported NuGet v3 resources, all verified with the real-client harness. Out of scope for this spec: private feeds and any vulnerability display in a UI.
```

<a id="spec-009"></a>
### 009: Revalidation, negative caching and rate limits

**Outcome:** Hoard keeps metadata fresh cheaply, absorbs requests for missing packages, and cannot be used to flood upstream.
**Acceptance focus:** Conditional revalidation of stale metadata (ETag/Last-Modified) with 304 handling; bounded negative caching of 404/410; inbound per-client rate limits and upstream concurrency limits so floods of unknown names do not amplify upstream; freshness defaults per NuGet endpoint documented; proven with the real-client harness and fault injection.
**Out of scope:** Offline mode and stale-if-error (X-10).

```text
/speckit-specify Specify how Hoard keeps cached metadata fresh and protects upstreams. Stale metadata is revalidated with conditional requests so unchanged content is not re-downloaded; not-found results are cached for a bounded time so repeated requests for missing packages do not hit upstream every time; inbound per-client rate limits and upstream concurrency limits prevent floods of unknown package names from amplifying into upstream traffic. Define default freshness lifetimes for each NuGet endpoint, client-visible behavior when limits are hit, and how a newly published upstream version becomes visible. Verify with the real-client harness and upstream fault injection. Out of scope for this spec: offline mode and serving stale content when upstream fails.
```

<a id="spec-010"></a>
### 010: Admin authentication and management API foundation

**Outcome:** A network-exposed Hoard can never be administered anonymously, and later management features have a consistent API to build on.
**Acceptance focus:** Bootstrap admin token generated on first run, shown once, stored hashed, rotatable, with a documented lost-token recovery path; when bound beyond loopback (including inside the container) every management endpoint requires the token; package reads from public registries stay anonymous so a default container install restores with no extra setup; versioned `/api/v1` conventions: OpenAPI, generated TypeScript types, error envelope, cursor pagination; a management health/readiness summary (building on spec 002's `/health/live` and `/health/ready` probes) and a "who am I / server info" endpoint as the first slices.
**Out of scope:** Catalog, overview and activity endpoints (built with the screens that use them), OIDC (X-07), package tokens (X-01), audit (X-08), module capability discovery (X-23).

```text
/speckit-specify Specify built-in admin authentication and the foundation of Hoard's management API. On first run Hoard generates an admin token, shows it once, stores only a hash, and supports rotation and documented recovery if it is lost. Whenever Hoard is bound beyond loopback, including inside its container where it cannot tell host-only publishing from network exposure, every management endpoint requires the token. Package reads from public registries remain anonymous so dotnet restore works on a default install; per-registry read control arrives later with package tokens. Establish the management API conventions every later feature will follow: versioned routes, an OpenAPI description with generated TypeScript client types, a consistent error format and cursor-based pagination; deliver health, readiness and server information as the first endpoints. Cover unauthenticated access, token rotation, lost-token recovery and loopback versus network-exposed behavior. Out of scope for this spec: package catalog, statistics or activity endpoints (each arrives with the screen that uses it), single sign-on, package-client tokens, audit history and module capability discovery.
```

<a id="spec-011"></a>
### 011: Registry management API

**Outcome:** Operators add, edit, test, disable and remove registries without editing files or restarting.
**Acceptance focus:** Admin-only create/update/enable/disable/delete with validation (ID collisions, invalid URLs, unsafe hosts); connectivity test with safe error feedback; defined impact on existing cache entries and client URLs; delete requires explicit confirmation; changes apply without restart.
**Out of scope:** Registry screens (017), credentials and private feeds (X-01), multiple upstreams per registry (X-14).

```text
/speckit-specify Specify how Hoard administrators manage upstream registries at runtime through the management API: create, edit, enable, disable and delete public registries, and test upstream connectivity with safe, informative feedback. Changes take effect without restarting. Define validation (duplicate IDs, invalid or unsafe URLs), what happens to cached entries and to client-facing URLs when a registry is changed, disabled or deleted, and explicit confirmation for destructive changes. Only administrators may make changes. Out of scope for this spec: console screens, credentials and private feeds, and multiple upstreams per registry.
```

<a id="spec-012"></a>
### 012: Request activity, statistics and telemetry

**Outcome:** Hoard records what it serves so operators can see hits, misses, bytes and latency, live and over time.
**Acceptance focus:** Activity tables added by this spec; per-request records written asynchronously in batches with bounded retention; time-bucketed aggregates; last-access tracking for LRU without per-hit DB writes; paginated activity API and a Server-Sent Events live stream (admin-protected when exposed); OpenTelemetry traces/metrics with low-cardinality labels; no credentials or sensitive URLs recorded; defined acceptable loss of buffered data on crash.
**Out of scope:** Console screens (015, 016), Prometheus export and savings analytics (X-12).

```text
/speckit-specify Specify how Hoard records and exposes request activity: every served request produces a record (time, registry, ecosystem, hit or miss, status, bytes served and fetched, durations) written asynchronously in batches so the request path is not slowed, with bounded retention and time-bucketed aggregates for hit rate, bandwidth and latency. Track last access per artifact for eviction without a database write per hit. Expose a paginated activity API and a live stream of requests through the management API, plus OpenTelemetry traces and low-cardinality metrics. Never record credentials or sensitive URLs. Define how much buffered data may be lost on a crash, and cover busy CI traffic. Out of scope for this spec: console screens, a Prometheus endpoint and bandwidth-savings analytics.
```

<a id="spec-013"></a>
### 013: Disk quota, LRU eviction and garbage collection

**Outcome:** A cache on a laptop or team server never grows without bound, and reclaiming space never breaks an active download.
**Acceptance focus:** Default disk cap (configurable); LRU eviction once over the cap; blob reclamation separate from entry eviction and safe for active readers (deferred deletion on Windows); orphan reconciliation after crash; low-disk safeguards; admin API to view usage and evict a selected package/version with confirmation, then refetch; reclaimed-space metrics.
**Out of scope:** Storage screen (017), pinning (X-09), per-registry quotas.

```text
/speckit-specify Specify how Hoard keeps its storage within a disk budget. A fresh install has a sensible default cap that operators can change; when usage exceeds it, Hoard evicts least-recently-used cache entries and reclaims their unreferenced artifacts without ever deleting content that is being streamed (including on Windows, where open files cannot be deleted) or leaving metadata pointing at missing content. After a crash, orphaned content and dangling records are reconciled. Through the management API, administrators can see usage and manually evict a package or version with confirmation, after which it is refetched on next request. Include low-disk behavior, concurrent eviction and download, and accurate reclaimed-space reporting. Out of scope for this spec: console screens, pinning and per-registry quotas.
```

<a id="spec-014"></a>
### 014: Console shell and design system

**Outcome:** A distinctive, accessible management console, served by Hoard itself, that later screens drop into.
**Acceptance focus:** Next.js latest stable App Router, TypeScript, Tailwind, shadcn/ui with Hoard design tokens; static export served by the .NET host (no Node runtime) with routing that handles arbitrary package IDs (no build-time dynamic params) and works behind a reverse proxy; admin-token sign-in; navigation Overview · Packages · Activity · Registries · Storage · Settings with placeholders; dark/light; loading/empty/error states; keyboard and screen-reader support; reduced motion; component and Playwright tests in CI.
**Out of scope:** Screen content (015–018).

```text
/speckit-specify Specify the visual and interaction foundation of Hoard's management console: a distinctive, modern developer-tool look with cohesive dark and light themes, responsive layouts from laptop to large monitor, accessible keyboard and screen-reader navigation, reduced-motion support, and reliable loading, empty and error states. The console is served by the Hoard server itself with no separate Node runtime, must handle arbitrary package names in its routes, and must work behind a reverse proxy. Users sign in with the admin token when Hoard requires it. Provide the navigation shell (Overview, Packages, Activity, Registries, Storage, Settings) with placeholder content; target WCAG 2.2 AA; include automated component and browser tests. Out of scope for this spec: the content of each screen.
```

<a id="spec-015"></a>
### 015: Overview and package explorer

**Outcome:** At a glance users can tell whether the cache is working and what it saves, and they can find any cached package.
**Acceptance focus:** Overview and catalog endpoints built here as vertical slices; KPIs (hit rate, bandwidth saved, storage, upstream health) and time-series charts over selectable ranges; package explorer with registry filter, search, version and artifact detail, digest, first/last fetched and used; drilldowns; empty states that guide to setup; numbers defined and reconciled with test traffic.
**Out of scope:** Live activity (016), management screens (017).

```text
/speckit-specify Specify Hoard's overview and package explorer, including the management API endpoints they need. The overview immediately shows whether Hoard is working and why it matters: hit rate, bandwidth saved, stored bytes, upstream health and trends over selectable time ranges. The package explorer lets users search and filter cached packages by registry and version and inspect artifacts, digests and when they were fetched and last used. Describe drilldowns, empty states that guide new users to setup, error states, and exact definitions of every displayed number so it matches real traffic. Out of scope for this spec: the live activity view and the registry, storage and settings screens.
```

<a id="spec-016"></a>
### 016: Live activity view

**Outcome:** Users watch requests flow through Hoard in real time and diagnose individual ones.
**Acceptance focus:** Live stream from spec 012 rendered with hit/miss/status/timing; pause, filters and per-request detail; history paging; graceful reconnect; performant under busy CI traffic; accessible announcements without flooding screen readers.
**Out of scope:** Prometheus/analytics (X-12).

```text
/speckit-specify Specify Hoard's activity view: a live stream of requests showing registry, package, hit or miss, status and timing, with pause and resume, filters, per-request detail for diagnosis, and paging back through recent history. It must stay responsive under busy CI traffic, reconnect gracefully after interruptions, and remain accessible without flooding screen-reader users with updates. Out of scope for this spec: metrics export and bandwidth analytics.
```

<a id="spec-017"></a>
### 017: Registries, storage and settings screens

**Outcome:** Operators manage registries, storage and security posture from the console.
**Acceptance focus:** Registries screen over the 011 API (add/edit/test/disable/delete with confirmation); storage screen over the 013 API (usage vs cap visualization, eviction candidates, manual eviction); settings (bindings and exposure warning, admin token rotation, diagnostics).
**Out of scope:** Setup assistant (018).

```text
/speckit-specify Specify the console's management screens. Registries: add, edit, test connectivity, disable and delete registries with clear validation and confirmation. Storage: visualize usage against the disk cap, show eviction candidates and evict selected packages with confirmation. Settings: show the deployment and security posture (bindings and whether Hoard is network-exposed), rotate the admin token and view diagnostics. Every destructive action is confirmed and its effect explained. Out of scope for this spec: the guided setup assistant.
```

<a id="spec-018"></a>
### 018: Setup assistant

**Outcome:** A developer points `dotnet` at Hoard in minutes and can prove it worked.
**Acceptance focus:** First-run guidance; copyable `NuGet.Config` and `dotnet nuget add source` snippets using the real public base URL; OS-appropriate variants; team-hosting security notes; a verification step proving a restore went through Hoard; snippets never contain secrets.
**Out of scope:** Other ecosystems until their adapters exist.

```text
/speckit-specify Specify a guided setup assistant in the console that gets a developer from first run to a restore through Hoard in minutes without installing any special tool: copyable, OS-appropriate NuGet.Config and dotnet CLI snippets using Hoard's actual public URL, guidance for team-hosted instances and their security considerations, troubleshooting, and a verification step that proves a restore actually went through Hoard. Out of scope for this spec: ecosystems other than NuGet.
```

<a id="spec-019"></a>
### 019: Distribution, first run and upgrades

**Outcome:** Anyone can install Hoard on any MVP platform in under five minutes and upgrade without losing their cache.
**Acceptance focus:** Documented install for the container (persistent volume contract; personal quickstart publishes to host loopback, team guide to the network with the admin token) and native binaries on Linux, macOS arm64 and Windows x64; first run initializes data, shows the admin token and the setup assistant URL; upgrade between versions preserves cache with pre-migration backup and documented rollback; graceful shutdown on each platform; single-node team deployment guide (TLS via reverse proxy or native HTTPS, anonymous public-package reads and rate limits, local-disk-only SQLite); quickstart checked against the five-minute target.
**Out of scope:** PostgreSQL, HA, nuget.org publishing.

```text
/speckit-specify Specify installation and lifecycle for individual developers and small teams on every MVP platform: run the Hoard container with a persistent volume, or download a self-contained executable for Linux, macOS (Apple silicon) or Windows. First run initializes storage, displays the admin token once and points to the console's setup assistant. Upgrading to a newer release preserves all cached data, backing up metadata before migrations, with documented rollback. Include graceful shutdown on each platform, a single-node team deployment guide (TLS via reverse proxy or native HTTPS, anonymous public-package reads and rate limits, local-disk-only SQLite), common error recovery, and a quickstart that gets a first cached restore in under five minutes. No external database, cache service or Node runtime may be required. Out of scope for this spec: PostgreSQL, high availability and nuget.org publishing.
```

## Post-MVP specifications

<a id="x-01"></a>
### X-01: Private upstream feeds and package tokens

Credentialed upstreams (Azure Artifacts, GitHub Packages, private NuGet servers); upstream credentials encrypted at rest with ASP.NET Core Data Protection and never exposed through the API; Hoard package tokens (scoped per registry, revocable, sent by clients as basic-auth password) required to read private registries; cache partitioning so private content is never served to unauthorized clients.

```text
/speckit-specify Specify support for private, credentialed upstream feeds in Hoard. Administrators add a registry with upstream credentials, which are encrypted at rest, never returned by the API or logged, and can be rotated. Content fetched from a private registry may only be served to clients presenting a Hoard package token scoped to that registry; tokens are issued, listed and revoked by administrators and work with standard NuGet credential configuration. Private registries always require a token; administrators can optionally require tokens on public registries too, replacing the MVP's always-anonymous public reads. Cover leaked or revoked tokens, rotated upstream credentials, upstream 401/403, and guarantees that private content never reaches unauthorized clients.
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

<a id="x-23"></a>
### X-23: Module capability discovery

Worth building once a second protocol module exists; with NuGet alone there is nothing to discover.

```text
/speckit-specify Specify a management experience where operators inspect installed, enabled and healthy Hoard modules, what each supports and why a capability is unavailable, exposed through the management API without secrets and reflected in the console so it never offers unsupported actions or confuses installed with enabled or healthy.
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

1. Exact default disk cap and freshness defaults per NuGet endpoint (settle in specs 013 and 009).
2. NuGet search/autocomplete in MVP or later (settle in spec 008).
3. Hoard namespace, domain and trademark clearance.
