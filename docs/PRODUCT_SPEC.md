# Hoard — Product Specification

**Status:** Draft v0.3 · **Revised:** 2026-10-31 · **Product name:** Hoard · **License:** MIT

## 1. Overview
A self-hosted, pull-through package-cache service that presents ecosystem-native registry endpoints. The initial release supports NuGet v3 against public upstreams and is designed to add Go, Maven, npm, Python/PyPI, and Rust/Cargo via modular protocol adapters. One ASP.NET Core executable hosts registry APIs and management APIs and can serve a statically exported Next.js management console.

## 2. Architecture and runtime
- **Backend:** .NET 10, ASP.NET Core Minimal APIs, dependency injection, typed `HttpClient` clients, background hosted services, OpenTelemetry.
- **Frontend:** Next.js 16.4 as of 2026-10-19 (upgrade to latest supported stable at implementation), App Router, TypeScript, Tailwind CSS 4, shadcn/ui, Radix/Base UI choice finalized during design setup, Lucide, TanStack Table, Recharts, TanStack Query for client data.
- **Bundling:** Next.js static export and .NET static-file hosting in the default distribution. No SSR, server actions, Next.js API routes, or other runtime-only features in bundled console; management API lives in .NET. Optional separate Next.js hosting may be introduced later.
- **Persistence:** SQLite initially; PostgreSQL provider planned. EF Core resides **inside relational persistence providers**, including migrations; `Hoard.Core` does not expose `DbContext` or depend on EF Core.
- **Blob store:** Filesystem initially with SHA-256 content addressing; S3-compatible and Azure Blob later.
- **Structure:** Composable modular monolith with registry/protocol, metadata persistence, blob storage and identity provider packages, shared cache core, Vertical Slice Architecture for endpoint/use-case features, management APIs, and bundled UI.
- **Background services:** Cleanup, stats aggregation, expired-entry maintenance, orphan blob reclamation.

## 2A. Modular provider platform (normative v0.2)

**Package topology:** `Hoard.Abstractions` defines stable contracts (minimal dependencies); `Hoard.Core` implements shared caching invariants against those contracts; `Hoard.Hosting` implements module registration/lifecycle and ASP.NET Core integration; `Hoard.Application` contains management vertical slices; `Hoard.Protocols.*`, `Hoard.Persistence.*`, `Hoard.Storage.*`, and `Hoard.Identity.*` are independently distributable provider packages; `Hoard.Host` is a concrete assembled executable. No core-to-concrete-provider references. The Next.js UI consumes APIs/capability descriptors and never references .NET modules.

**Module descriptor:** Unique stable ID and category, module version, supported host/abstractions compatibility range, required/optional dependencies, configuration schema, declared capabilities, routes (where applicable), health/readiness metadata and provenance/support status. Duplicate IDs or incompatible dependencies fail startup with actionable errors.

**Contract maturity:** Module boundaries are enforced from the first spec by project references and architecture tests. Public provider contracts are versioned and stabilized when a second implementation (e.g. PostgreSQL, a second protocol) exercises them; speculative abstractions without a consumer are not built.

**Lifecycle:** Known packaged modules are discovered or explicitly registered during host composition; configuration allowlists/enables modules; validation and dependency ordering occur before activation; services/options and routes are wired during application build; initialization runs in a controlled startup phase; readiness remains false on required module failure; shutdown releases resources. Runtime install/uninstall without rebuilding and restarting is **not** promised. No automatic loading of arbitrary assemblies from disk.

**Selection semantics:** Multiple protocol providers enabled concurrently. One authoritative metadata provider and one authoritative blob provider per initial deployment. Multiple auth schemes may coexist while authorization is normalized into Hoard permissions; package-manager clients use noninteractive credentials/API tokens independently of browser login. Policy and event-handler extension points are prospective and must not weaken cache invariants.

**Storage boundaries:** Metadata providers own DB schemas/migrations, transaction and concurrency semantics, indexing and recovery. Artifact providers own staging/commit semantics, streaming, validation/visibility and deletion; filesystem/object-store durability and atomicity differ and must be expressed via capabilities. Cross-store consistency is coordinated by core. NoSQL is an exploratory family requiring contract-specific evaluation and cannot be assumed EF-compatible.

**Distribution:** Curated default Docker/executable distribution bundles a supported set of providers and selects them using configuration without a rebuild. Custom hosts reference further provider NuGet packages and rebuild/redeploy. Disabled providers expose no endpoints or background jobs. Community modules execute as trusted in-process code and need explicit operator approval.

**Compatibility:** Public abstraction/API versions follow documented semantic compatibility; module metadata declares supported platform ranges; startup detects version skew and unsupported required capabilities. Tests must validate default and alternative provider permutations as they become supported.

## 2B. Vertical Slice Architecture (normative v0.2)

A feature slice colocates endpoint mapping, request/response DTOs, validation, authorization requirements, handler/orchestration and focused tests. Organize management operations as `Features/Registries/CreateRegistry`, `Features/Packages/PurgePackage`, `Features/Cache/GetStatistics`, and similar. Protocol adapter modules may organize `Features/GetServiceIndex`, `Features/GetPackageVersions`, and `Features/DownloadPackage` as slices **inside their package**. Cross-slice communication uses explicit application/core contracts, not direct access to another slice's internals. ASP.NET Core Minimal APIs and DI are the default; neither MediatR nor CQRS is required.

**Exceptions—intentionally shared and NOT reimplemented per slice:** HTTP cache lifecycle, canonical cache identities and auth variant partitioning, cache policies/freshness, integrity/hash verification, staged atomic publication, deduplication/single-flight, bounded streaming and backpressure, eviction/reference safety, transport SSRF policy, shared telemetry/audit foundations, provider abstractions and coordination. Their reusable implementations live in cohesive `Hoard.Core` / security / hosting services. Provider implementations share *contracts*, not their concrete EF/SDK classes.

**Dependency rule:** `Host → Hosting/Application/Core + installed Providers`; `Application slices → Core/Abstractions`; `Protocol slices → Core/Abstractions`; `Core → Abstractions`; `Providers → Abstractions` (and only explicitly approved hosting contracts). Never `Core → SQLite`, `NuGet → Postgres` or `Feature A → Feature B` internals.

## 3. Core entities
| Entity | Example fields / responsibility |
|---|---|
| `Registry` | Id, ecosystem, upstream base URL, permitted upstream hosts, enabled, policy (auth reference post-MVP) |
| `Package` | Id, ecosystem, canonical name, namespace, optional display fields |
| `Artifact` | Digest SHA-256, length, content type, storage locator, committed timestamp |
| `CacheEntry` | Registry Id, normalized request key, method, status, headers, artifact digest, ETag, LastModified, freshness timestamps |
| `FetchLease` | Resource key, owner, creation/expiry (future distributed coordination) |
| `AccessAggregate` | Interval, registry, hits, misses, bytes served, bytes fetched, latency metrics |
| `Policy` | Scope, metadata TTL, retention, max size, immutable behavior, stale-if-error |
| `CredentialReference` | Post-MVP (private feeds): identifier and upstream credential encrypted at rest with ASP.NET Core Data Protection; never plaintext |
| `RequestRecord` | Timestamp, registry, ecosystem, cache result, status, bytes, durations; feeds the Activity view and aggregates, with bounded retention |
| `AdminCredential` | Hashed bootstrap/admin token, creation and revocation timestamps |

### Identity and invariants
- Request keys include registry identity, protocol-specific canonical path/query, and any response-varying dimensions. Do not cache arbitrary authenticated variants together.
- Artifact bytes keyed by cryptographic digest, not solely package/version.
- A cache entry never references an uncommitted blob.
- Upstream metadata and package artifacts use different caching policies.
- Preserve protocol-required methods, status codes, headers, URL forms, and response content types.

## 4. Cache request lifecycle
1. Route incoming request to an ecosystem adapter.
2. Adapter validates request, produces canonical cache key, determines protocol-specific response handling.
3. Engine checks cache entry and freshness; validates backing artifact availability.
4. Serve fresh hit; for stale metadata, perform conditional upstream revalidation when appropriate.
5. On miss, use in-process per-key single-flight coordination; waiting requests re-check committed entry.
6. Fetch configured upstream using a restricted HTTP client with SSRF and redirect protections; stream response with bounded memory.
7. Stage payload in temporary storage, verify size/hash and optional ecosystem checksum, then atomically commit blob.
8. Atomically publish cache metadata; only then can other requests treat it as a valid hit.
9. Respond with protocol-correct transformed metadata or package content; collect asynchronous access metrics.
10. On error, follow explicit negative cache, retry, stale-if-error and timeout policy; never turn corrupt or partial bytes into hits.

### HTTP behavior
- Support conditional requests (If-None-Match / If-Modified-Since) where applicable.
- Decide 404/410 negative-cache TTL per ecosystem; don't cache 401/403 as shared negative entries by default.
- Correct HEAD and Range handling must be specified and tested before claiming support.
- Stream large artifacts without loading fully into RAM.
- Handle upstream rate limits, redirects and retries safely, with bounded concurrency and exponential backoff for transient failures.
- Define cache-control behavior for transformed registry metadata so clients don't retain stale endpoint URLs.

## 5. Registry adapters: rollout and contracts
### Priority
1. **NuGet v3** — first implementation and acceptance gate.
2. **Go GOPROXY** — second.
3. **Maven** — third.
4. **npm** — fourth.
5. **PyPI** — subsequent.
6. **Cargo** — subsequent.

### Adapter responsibilities
- Register URL routes under stable namespace, e.g. `/r/nuget/{registry}/...`.
- Parse and validate package coordinate/path according to registry's case/encoding rules.
- Select safe upstream endpoint and map request to cache key.
- Transform returned discovery and metadata URLs to local cache endpoints.
- Declare immutable-blob vs mutable-metadata cache behavior.
- Provide setup instructions and an end-to-end compatibility suite.

### NuGet v3 requirements (P0)
- Serve local NuGet v3 service-index JSON (`index.json`) containing correct local resource URLs for supported endpoints.
- Support PackageBaseAddress/flat-container version indexes and `.nupkg` downloads for standard restore.
- Handle registration metadata required by tested clients; consider package metadata/search endpoint requirements explicitly, not silently assume full protocol support.
- Correct NuGet package ID/version normalization, semantic-version edge cases, URL encoding and authentication propagation.
- Ensure every advertised required resource URL routes through cache rather than bypassing it.
- Configure standard clients via `NuGet.Config` or `dotnet nuget add source`.
- Test `dotnet restore`, `dotnet add package`, multi-project transitive restores, fresh cache, warm cache and upstream failure scenarios.
- Proxy the `VulnerabilityInfo` resource so `dotnet restore`'s default NuGet Audit keeps working; without it, restore emits audit warnings that fail warnings-as-errors builds.
- Derive the permitted upstream hosts for a NuGet registry from its upstream service index (nuget.org spans several hosts, e.g. API and CDN flat-container) and validate them against SSRF policy.
- Document unsupported NuGet endpoints until implemented.

### Go requirements (P1)
- Implement GOPROXY endpoints: `@v/list`, `@v/{version}.info`, `@v/{version}.mod`, `@v/{version}.zip` and module escaping rules.
- Document `GOPROXY` and `GOSUMDB`/private module considerations; never pretend caching substitutes for checksum verification.

### Maven requirements (P1)
- Maven repository-style paths for POM, JAR and related artifacts, metadata (`maven-metadata.xml`), snapshot semantics, checksums and plugin dependencies.
- Document `settings.xml` mirrors; do not treat snapshots as immutable release assets.

### npm requirements (P2)
- Registry metadata documents, scoped packages, tarballs, URL rewriting and request headers; preserve auth boundaries.
- Support common npm clients using `.npmrc`; test lockfile and integrity semantics.

### PyPI and Cargo (P3)
- PyPI: Simple Repository API HTML/JSON negotiation, normalized project names, file links and wheel/sdist handling.
- Cargo: sparse index, index `config.json` download URL rewriting, crate content and authentication limitations.

## 6. Management API (initial contract proposal)
- `GET /api/v1/health` — health/readiness summary (separate unauthenticated liveness endpoint as needed).
- `GET /api/v1/overview` — statistics and status.
- `GET /api/v1/packages?ecosystem=&q=&page=` — searchable catalog.
- `GET /api/v1/packages/{id}` — package details/artifacts.
- `GET /api/v1/requests?cursor=` — recent requests/latency/errors.
- `GET /api/v1/requests/stream` — live request stream (Server-Sent Events) for the Activity view.
- `GET /api/v1/registries` — configured registries.
- `POST /api/v1/registries` — create registry (restricted).
- `PATCH /api/v1/registries/{id}` — update registry (restricted).
- `DELETE /api/v1/registries/{id}` — remove registry (restricted, confirmation required).
- `POST /api/v1/cache/evictions` — selected artifact/entry eviction (restricted, auditable).
- `GET /api/v1/setup/{ecosystem}` — native configuration guidance.
- `GET /metrics` — Prometheus metrics when enabled, restricted on shared networks.

Restricted endpoints require the admin token whenever Hoard is bound to a non-loopback address. Use OpenAPI and generated TypeScript client types. API pagination should be cursor-based on high-volume activity endpoints. Require confirmation for destructive UI operations.

## 7. UX / design system requirements
### Visual language
- Beautiful, professional infrastructure console; compact information density, crisp typography, neutral tones with restrained accent, subtle depth, dark mode and light mode.
- shadcn/ui components styled with application tokens, not unmodified stock appearance.
- WCAG 2.2 AA target, keyboard-friendly command/search interfaces, reduced-motion support, screen-reader labels and robust color contrast.

### Screens
1. **Overview:** Hits/misses, bandwidth saved, registry health, storage usage, recent request stream.
2. **Packages:** Search, filter by ecosystem/registry, version/artifact details, checksums, last fetched/used.
3. **Activity:** Timeline, latency, hit/miss, status, filters, request diagnostics.
4. **Registries:** Add/edit upstream registry, test reachability, priority and status.
5. **Storage:** Quota, usage distribution, eviction/pinning (pinning later), garbage-collection status.
6. **Settings:** Deployment/security settings, diagnostics, client setup guides.

### Libraries
- Next.js App Router / TypeScript; Tailwind CSS 4; shadcn/ui; Lucide icons; TanStack Table for advanced grids; Recharts for time series; Sonner for notifications; React Hook Form + Zod for validation; `next-themes` for appearance.
- Default bundle uses static export. Keep data loaded client-side from the ASP.NET Core management API. Make base URLs work behind reverse proxies.

## 8. Security and isolation
- Bind only to loopback by default; explicit opt-in to `0.0.0.0`. The official container image opts in inside the container, so exposure on the host is governed by Docker port publishing (personal docs use `-p 127.0.0.1:8484:8484`).
- UI management and registry read/write permissions are separate surfaces.
- Team management endpoints are protected by the built-in admin token (generated on first run, shown once, stored hashed, rotatable) and HTTPS, either natively or via a TLS-terminating reverse proxy.
- Anonymous network package reads are allowed only by explicit deployment opt-in. Scoped package tokens arrive with private feeds (post-MVP); OIDC console login is post-MVP.
- Inbound rate limits bound requests per client and upstream fetch concurrency, so floods of unknown package names cannot amplify into upstream traffic.
- Strict allowlist of upstream hosts; validate DNS resolution and redirects against private/link-local/metadata addresses. Avoid open-proxy behavior.
- Enforce path and archive handling constraints, request limits, timeouts and upload/download byte caps where appropriate.
- Never log authorization headers, credential-bearing URLs or secret values.
- Secrets obtained via environment variables, mounted files or external secret providers; do not put cleartext secrets in SQLite.
- Dependency vulnerability scanning and licensing checks in CI; responsible-disclosure policy before public beta.

## 9. Reliability and operations
- Cache hit operations require no upstream network call for fresh immutable content.
- Single-process deduplication on cold misses; multi-node locking deferred.
- Store temp downloads on the same filesystem or use copy/rename semantics that guarantee final atomic visibility.
- Recover on restart by removing expired staging files and reconciling orphan records/blobs.
- Retain access counters asynchronously; batch writes to avoid DB contention.
- Expose OpenTelemetry spans/metrics including request duration, cache result, upstream fetch duration, bytes served/fetched, corruption and eviction counts. Avoid package ID labels on high-cardinality metrics.
- Schema migrations are forward-only; Hoard backs up the SQLite database before applying a migration and documents restore.
- SQLite must not run on network filesystems (NFS, SMB, Azure Files); team docs state this explicitly.
- Garbage collection must tolerate platforms where open files cannot be deleted (Windows): deletion is deferred until no reader holds the blob.
- DB backups, data restore, migration procedures, and artifact verification command documented.

## 10. Configuration sketch (illustrative, not final schema)
Startup configuration (command line > environment > file) selects modules and providers, listeners and storage paths; module activation changes require restart. **Registries live in the metadata database** and are managed through the console and management API; the configuration file may seed registries on first run only, after which the database is the source of truth. Unknown provider IDs, missing providers and invalid options fail startup.
```yaml
server:
  listen: 127.0.0.1:8484
  publicBaseUrl: http://localhost:8484
storage:
  type: filesystem
  path: ./data/blobs
  maxDiskBytes: 21474836480   # default cap; LRU eviction above it
metadata:
  provider: sqlite
  connection: Data Source=./data/cache.db
seedRegistries:               # applied on first run only
  - id: nuget-org
    ecosystem: nuget
    upstream: https://api.nuget.org/v3/index.json
policies:
  metadataTtl: 15m
```
This is a target UX for configuration, not a promise that a parser or route shape already exists.

## 11. Repository layout (v0.2 proposal)
```text
/apps/web                             # Next.js console
/src/Hoard.Host                       # prebuilt curated executable
/src/Hoard.Hosting                    # composition, module lifecycle
/src/Hoard.Abstractions               # stable provider/core contracts
/src/Hoard.Core                       # central caching invariants
/src/Hoard.Application/Features       # management vertical slices
/src/Hoard.Protocols.NuGet/Features   # protocol-specific slices
/src/Hoard.Protocols.Go/Features
/src/Hoard.Protocols.Maven/Features
/src/Hoard.Protocols.Npm/Features
/src/Hoard.Persistence.Sqlite         # EF Core SQLite implementation
/src/Hoard.Persistence.Postgres       # EF Core PostgreSQL implementation (later)
/src/Hoard.Storage.FileSystem
/src/Hoard.Storage.S3                 # later
/src/Hoard.Identity.Local             # optional local credentials
/src/Hoard.Identity.Oidc              # later
/tests/contracts                      # shared provider conformance
/tests/integration
/tests/protocol                       # real package manager client tests
/tests/composition                    # provider combinations/version skew
/deploy/docker
/docs/adr
/ARCHITECTURE.md
```
Package naming and public API names are proposed; fix via ADRs before publishing stable packages.

## 12. Acceptance criteria — v0.1 NuGet slice
- Core builds/tests without referencing concrete protocol, database, storage or identity implementations.
- An official distribution starts with NuGet + SQLite + filesystem selected by configuration; disabling an optional protocol does not leave its routes active.
- Unsupported module IDs and invalid module configuration yield actionable startup failure; disabled modules expose no routes. (Capability discovery is post-MVP, once a second module exists.)
- Management and protocol endpoints are feature-oriented slices; shared cache invariants and provider contracts are centralized.
- Every built provider passes its behavior tests, including crash/failure cases. Shared category conformance suites and a custom host composition arrive with the second implementations (post-MVP).
- From a clean environment, install/start one container with persistent volume and no external DB.
- `/` serves the polished management console; health endpoint reflects initialization and disk/database readiness.
- NuGet v3 source configuration points exclusively to local proxy discovery/resource endpoints.
- Fresh `dotnet restore` downloads packages successfully and records cache misses and artifact metadata.
- Second restore after clearing only the *client-side* NuGet caches completes with server-side hits and no repeat upstream artifact fetches.
- 10 simultaneous identical fresh package requests issue one upstream artifact download per server instance.
- Interrupted upstream transfer does not create a valid cache entry.
- Verify sample `.nupkg` digest and provenance; manually evict then refetch it.
- When bound to a non-loopback address, management API and console require the admin token; package reads are anonymous only if explicitly enabled.
- With the default disk cap reached, LRU eviction reclaims space without deleting a blob that is being served.
- `dotnet restore` with default NuGet Audit settings completes without audit-source warnings.
- UI presents real metrics, search/detail view, live activity, status and errors with accessible loading states.
- Tests run in GitHub Actions on every PR; each merged spec publishes an alpha container image, binaries for all MVP platforms and packages, with documented upgrade/backup steps.

## 13. Release plan and priorities
| Stage | Focus | Exit gate |
|---|---|---|
| M0 | Pipeline + skeleton | CI on every PR; per-spec alpha releases publishing image, binaries, packages |
| M1 | **First cached restore** | cold/warm `dotnet restore` through Hoard on SQLite + filesystem |
| M2 | NuGet complete + management | registration/audit coverage, admin token, console-managed registries, quota/eviction |
| M3 | Console | all screens including live activity; WCAG 2.2 AA target |
| MVP | Distribution + hardening | all MVP platforms, upgrade path, documented single-node team deployment |
| Later | Private feeds, Go, Maven, npm, PostgreSQL, OIDC, then PyPI, Cargo, object storage | protocol tests; contract qualification with second implementations |

See [RELEASING.md](RELEASING.md) for versioning and tags.

## 14. Open questions
1. Hoard repository organization, domain availability, and trademark clearance (name chosen; clearance pending).
2. NuGet search (`SearchQueryService`) and autocomplete: MVP or later (decide in the NuGet coverage spec).
3. Cache freshness defaults by endpoint and ecosystem; exact default disk cap.

Resolved 2026-10-31: non-loopback binding requires the built-in admin token for management; MVP is public upstreams only with one upstream per registry (private feeds and merging are post-MVP); the team release gate is built-in admin auth; the bundled console is static export only; MVP platforms are Linux container (x64, arm64), Linux, macOS (arm64) and Windows (x64) executables.

## 15. References
- Next.js release news: https://nextjs.org/blog
- shadcn/ui official Next.js setup: https://ui.shadcn.com/docs/installation/next
- NuGet v3 protocol: https://learn.microsoft.com/en-us/nuget/api/overview
- Go module proxy protocol: https://go.dev/ref/mod#goproxy-protocol
- PyPI Simple API: https://packaging.python.org/en/latest/specifications/simple-repository-api/
- Cargo registries: https://doc.rust-lang.org/cargo/reference/registries.html

---
These documents are planning specifications, not claims of completed implementation. Routes and default policy values remain subject to ADRs and protocol tests. The product name is Hoard; legal and namespace clearance remains pending.

## 16. Provider conformance & rollout decisions (v0.2)
- **P0 required:** module boundaries enforced by architecture tests, startup activation validation, SQLite/filesystem/NuGet behavior tests, feature-slice conventions, secure default composition.
- **Post-MVP, first expansion:** PostgreSQL and a second protocol adapter; these are where shared provider conformance suites and versioned contracts are qualified.
- **Later optional providers:** SQL Server and MySQL relational backends; generic OIDC plus Entra convenience integration; Azure Blob and S3. NoSQL requires separate design and operational qualification.
- **Deferred:** arbitrary dynamic assembly loading, runtime marketplace, remote code installation, provider process isolation, live database swaps and multi-store tiering.
- **Reference documents:** `ARCHITECTURE.md`, `docs/adr/0001-modular-provider-architecture.md`, and `HOARD_SPECKIT_MASTER_BACKLOG.md`.
