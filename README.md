<p align="center"><img src="docs/assets/hoard-banner.svg" alt="Hoard — Fetch once. Build forever." width="720"></p>

# Hoard

**Fetch once. Build forever.** Hoard is a lightweight, self-hosted pull-through
cache for software packages. Point your package manager at it with its normal
configuration; Hoard fetches each dependency from the upstream registry once,
verifies it, and serves it locally from then on.

> **Status: planning.** No application code exists yet. The documents below
> describe intended behavior, not shipped features. Work proceeds one
> specification at a time with [GitHub Spec Kit](https://github.com/github/spec-kit).

## What it will do

- **NuGet first.** The MVP is a NuGet v3 cache for public feeds such as
  nuget.org, verified with real `dotnet restore` runs. Go, Maven, npm, PyPI and
  Cargo follow.
- **One product, two profiles.** A personal install is a single container or
  executable bound to loopback, with SQLite and local disk and nothing else to
  run. A team install is the same binary exposed on the network with
  built-in admin authentication.
- **Trustworthy bytes.** Downloads are staged, checksum-verified and atomically
  committed; partial or corrupt content is never served as a hit.
- **A polished console.** A Next.js management UI, served by the Hoard process
  itself, shows hits, misses, bandwidth saved, cached packages and live activity.

Hoard is a cache, not a private package registry or publishing platform.

## Documentation

| Document | Contents |
| --- | --- |
| [Product vision](docs/PRODUCT_VISION.md) | Why Hoard exists and the experience it aims for |
| [Product charter](docs/PRODUCT_CHARTER.md) | Scope, MVP, non-goals, deployment profiles |
| [Product specification](docs/PRODUCT_SPEC.md) | Cache lifecycle, protocols, API, UI and security requirements |
| [Architecture](docs/ARCHITECTURE.md) | Modular monolith, provider categories, dependency rules |
| [ADR-0001](docs/adr/0001-modular-provider-architecture.md) | Composable modular monolith with vertical slices |
| [Spec backlog](docs/HOARD_SPECKIT_MASTER_BACKLOG.md) | Ordered feature specifications and Spec Kit prompts |
| [Spec progress](docs/spec-progress.md) | Where each spec stands |
| [Development](docs/DEVELOPMENT.md) | Change flow and contributor tooling |
| [Releasing](docs/RELEASING.md) | Versioning, tags and published artifacts |

## License

[MIT](LICENSE) © Christopher House
