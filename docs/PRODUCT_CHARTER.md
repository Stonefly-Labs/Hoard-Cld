# Hoard — Product Charter

**Status:** Draft v0.3 · **Revised:** 2026-10-31 · **Product name:** Hoard · **License:** MIT

## 1. Purpose
Build a lightweight, open-source, self-hosted, pull-through caching proxy for software package ecosystems. It should start effortlessly on a developer laptop yet function as a reliable shared service for development teams and CI/CD agents.

## 2. Problem
Dependency restoration repeatedly downloads the same public artifacts, wastes bandwidth, increases build latency, and makes builds vulnerable to public-registry disruptions. Existing artifact managers often bring substantial setup and administrative overhead; ecosystem-specific caches fragment operations.

## 3. Mission
**One simple cache for the dependencies your tools already use.** Preserve native package-manager experiences while reducing upstream traffic and improving repeat-build reliability.

## 4. Audiences
- **Primary:** Individual developers who want a zero-dependency local cache.
- **Primary:** Small-to-medium engineering teams and CI operators needing a centralized shared cache.
- **Secondary:** Self-hosters, labs, and organizations seeking a controllable dependency mirror/cache.

## 5. Product principles
1. **Cache first:** Be a transparent pull-through cache, not a package publishing suite.
2. **Native clients:** No custom CLI required; use standard NuGet/npm/pip/Cargo/Go/Maven configurations.
3. **One product, two profiles:** Personal and team modes share binaries, APIs, and features.
4. **Simple first run:** SQLite + filesystem by default, no required infrastructure services.
5. **Trustworthy bytes:** Atomic publishing, integrity checks, explicit stale/offline behavior.
6. **Secure by default:** Local-only default binding, controlled upstreams, protected management when network-exposed.
7. **Beautiful and useful UI:** A polished operational console rather than an afterthought.
8. **Open by design:** MIT licensed, contributor-friendly, vendor-neutral.
9. **Composable by design:** Protocol, persistence, blob storage and identity providers implement versioned, independently distributable contracts; the curated distribution remains zero-friction.
10. **Feature cohesion:** Business/API operations follow Vertical Slice Architecture (VSA); universal cache correctness, integrity, coordination and provider contracts remain cohesive shared services rather than duplicated per slice.
11. **Extensibility without mandatory complexity:** NuGet package composition and startup-time activation first; no arbitrary runtime assembly loading required for MVP.

## 6. Scope
### MVP (v0.1)
- **NuGet v3 only**, against public, unauthenticated upstreams (e.g. nuget.org). One upstream per registry.
- ASP.NET Core/.NET 10 **composable modular monolith** with module boundaries enforced by architecture tests. Provider contracts are stabilized when a second implementation needs them, not before.
- Feature-oriented Vertical Slice Architecture for management operations and protocol-specific endpoints; centralized shared caching engine for cross-cutting invariants.
- Filesystem content-addressed artifact storage; SQLite metadata.
- Shared cache pipeline: request deduplication, atomic staging, revalidation, error handling, SSRF-safe upstream fetching.
- Default disk cap with safe LRU eviction and garbage collection.
- Registries stored in the metadata database and managed through the console and management API.
- Built-in admin authentication: a bootstrap admin token protects the console and management API. Package reads from public registries are anonymous in the MVP (the content is public; rate limits bound abuse); per-registry read control arrives with package tokens.
- Next.js App Router management console (shadcn/ui, Tailwind CSS, accessible components), statically exported and served by the .NET host: overview, package explorer, live request activity, registries, storage, settings, client setup snippets.
- Distribution: Linux container (x64, arm64) plus self-contained executables for Linux, macOS (arm64) and Windows (x64).
- GitHub Actions CI on every PR and CD from the first spec: container image to GHCR, binaries attached to GitHub Releases, packages to GitHub Packages.
- Unit, protocol contract, and end-to-end tests using the real `dotnet` client; Playwright for the console.

### Subsequent releases
- **Private and credentialed upstream feeds** (e.g. Azure Artifacts, GitHub Packages), with Hoard package tokens to control who may read private content and upstream credentials encrypted at rest.
- Go GOPROXY, Maven and npm adapters; later PyPI and Cargo.
- PostgreSQL (the second metadata implementation that proves the persistence contract), S3-compatible storage and Azure Blob. SQL Server and MySQL may follow demand; document-store support is investigational, not promised.
- Multiple upstreams merged behind one registry, with dependency-confusion protections.
- Team OIDC login for the console, audit trail, richer retention policies, pinning, lockfile prewarming, offline mode.
- Publishing provider packages to nuget.org; community provider SDK/templates.
- Multi-instance coordination and HA only after validating demand.

### Explicit non-goals for MVP
- Hosting/publishing private packages; full artifact repository management.
- Enterprise RBAC/SCIM, comprehensive vulnerability scoring, provenance certification.
- Multi-tenant cache isolation within one instance.
- Distributed caching/active-active replicas, air-gapped synchronization.
- Custom **client-side** package-manager plugins or modifications to client software; dynamic loading or sandboxing of arbitrary third-party *server* modules. Independently compiled server provider packages are in scope.

## 7. Deployment profiles
| Attribute | Personal | Team |
|---|---|---|
| DB | SQLite | SQLite (single node) in MVP; PostgreSQL later |
| Blobs | Local filesystem | Persistent disk initially; object storage later |
| Runtime | Single executable/container | Single shared service initially |
| UI | Same Next.js interface | Same Next.js interface |
| Binding | Loopback by default | Explicit network exposure with authentication/TLS |
| Read access | Anonymous | Anonymous for public registries in MVP; package tokens later |
| Management | Local-only by default | Built-in admin token; OIDC later |

No feature paywalls, no separate codebase.

## 8. Success measures (targets to validate, not promises)
- First personal cache running in <5 minutes from documented install steps.
- Repeat immutable artifact requests served without upstream transfer.
- Concurrent identical cold requests produce one upstream fetch per single instance.
- No partially written or checksum-invalid artifact is exposed as valid cached content.
- NuGet v3 restore works with official `dotnet restore` and standard `NuGet.Config`.
- P95 local-hit overhead target under 50 ms for registry metadata, measured under defined benchmark conditions.
- CI demonstrates adapter conformance, crash recovery, and regression coverage.

## 9. Milestones
1. **Pipeline and skeleton:** Solution structure, architecture tests, CI/CD and per-spec releases from day one.
2. **First cached restore:** Host, configuration, SQLite metadata, filesystem storage, cache engine and NuGet flat container, proven with a real cold/warm `dotnet restore`.
3. **Complete NuGet and management:** Registration/audit/search coverage, management API with admin token, console-managed registries, quota and eviction.
4. **Console:** Shell and design system, overview/packages/live activity, registries/storage/settings, setup assistant.
5. **MVP release:** Multi-platform distribution, upgrade path, docs, security hardening.
6. **Expansion:** Private feeds and package tokens, Go, Maven, npm, PostgreSQL, OIDC, then PyPI, Cargo and cloud storage.

## 10. Risks and mitigations
- **Protocol divergence:** Versioned adapters and official-client integration suites.
- **Plugin coupling and version skew:** Strict dependency direction, capability metadata, module compatibility checks and provider contract tests.
- **Extension security:** Treat loaded modules as trusted application code; support only explicitly included/allowlisted providers, avoid untrusted hot loading.
- **Registry metadata references upstream URLs:** Explicit URL rewriting and contract tests.
- **Cache poisoning/SSRF:** Allowlisted upstreams, redirect validation, checksum verification, safe filenames, immutable committed blobs.
- **Single-node bottlenecks:** Streaming, bounded buffers, batched stats writes; measure before introducing distributed infrastructure.
- **Competitive overlap:** Differentiate through zero-friction setup, design quality, protocol reliability, and operational clarity.
- **Next.js runtime overhead:** Support static-exportable management UI served by .NET; avoid Next.js server-only features in the bundled profile.

## 11. Governance and licensing
- MIT license for original project code; verify compatibility and attribution of third-party dependencies/assets.
- Public issue tracker, architectural decision records (ADRs), RFCs for protocol changes, contribution guidelines, security reporting policy.
- API and adapter compatibility policies established before v1.0.

## 12. Open decisions
- Hoard repository namespace, domain availability, and trademark clearance (name chosen; clearance pending).
- How offline/stale reads should behave per ecosystem.
- Exact default disk cap value and eviction heuristics.

Resolved 2026-10-31: MVP is NuGet-only with public upstreams; team mode uses built-in admin authentication in the MVP; a default disk cap with LRU eviction ships in the MVP; the bundled console is static export only.

## 13. Platform and implementation boundaries (v0.2 decision)
- **Platform composition**: NuGet-distributed .NET providers supply protocols, metadata persistence, artifact storage, authentication, and later optional behaviors. A deployment enables installed providers through startup configuration.
- **Deployment flavors**: an official, curated, prebuilt runtime needs no rebuild for bundled providers; a custom distribution references additional provider packages, builds once and starts with validated config.
- **VSA applicability**: management API use cases and protocol-specific HTTP operations are feature slices. Cache key isolation, streaming, content verification, atomic publication, concurrency coordination, retention safety, audit/security primitives and provider contracts remain shared services.
- **Source of truth**: see [ARCHITECTURE.md](ARCHITECTURE.md) and [ADR-0001](adr/0001-modular-provider-architecture.md) for dependency/lifecycle/contract rules.
- **MVP restraint**: full runtime assembly plugin installation, third-party code isolation, arbitrary NoSQL support and an extension marketplace are deferred.
