<p align="center"><img src="docs/assets/hoard-banner.svg" alt="Hoard — Fetch once. Build forever." width="720"></p>

# Hoard

**Fetch once. Build forever.** Hoard is a lightweight, self-hosted pull-through
cache for software packages. Point your package manager at it with its normal
configuration; Hoard fetches each dependency from the upstream registry once,
verifies it, and serves it locally from then on.

<!-- release-status:start -->
## Status: early alpha

Hoard is built one specification at a time, and every merged spec ships an alpha
release. Hoard does not cache anything yet.

**Working now**

- A Hoard host with two endpoints: `GET /health/live` (the process is serving) and
  `GET /health/ready` (startup finished, the data directory is writable, and Hoard is not
  shutting down). Both answer `Healthy` or `Unhealthy`; every other path returns `404`.
- Configuration from the command line, `HOARD_*` environment variables and a `hoard.json`
  file, in that order of precedence. Unknown or malformed options, from any source, stop
  startup with one report naming each problem; Hoard never falls back to other values.
  Framework settings such as `ASPNETCORE_URLS` or `appsettings.json` are ignored.
- Loopback by default (`localhost:8484`). Listening on another address must be configured
  explicitly, together with a TLS mode: `https` with a certificate, or `proxy` for a
  TLS-terminating reverse proxy in front. Hoard then logs a warning that package reads are
  anonymous.
- A data directory (`./data` by default) that Hoard creates and checks it can write to. No
  data is stored in it yet.
- A clean stop on `SIGTERM` or Ctrl+C: in-flight requests get up to 5 seconds, then Hoard
  exits with code 0.
- A multi-arch container image (`linux/amd64`, `linux/arm64`) and self-contained
  executables for `linux-x64`, `linux-arm64`, `osx-arm64` and `win-x64`, with a
  `SHA256SUMS` file. Before each release, every executable and both image architectures
  are smoke-tested on native runners.
- The container listens on all interfaces inside the container with TLS mode `proxy`
  declared, so Docker port publishing decides whether the host's network can reach it.

**Planned**

- Metadata store and filesystem artifact storage.
- Registries with safe upstream access.
- The cache core: verified hits and misses, single-flight downloads and streaming.
- NuGet v3 pull-through caching, proven with real `dotnet restore` runs.
- Revalidation, negative caching, rate limits, disk quotas and eviction.
- Admin authentication, the management API, activity and telemetry.
- The web console and setup assistant.
- Other ecosystems (Go, Maven, npm, PyPI, Cargo) after the MVP.
<!-- release-status:end -->

## What it will do

The intended product. None of this ships yet; see the status above.

- **NuGet first.** The MVP is a NuGet v3 cache for public feeds such as
  nuget.org, verified with real `dotnet restore` runs. Go, Maven, npm, PyPI and
  Cargo follow.
- **One product, two profiles.** A personal install is a single container or
  executable bound to loopback, with SQLite and local disk and nothing else to
  run. A team install is the same binary exposed on the network with an
  explicit TLS posture (native HTTPS or a reverse proxy) and built-in admin
  authentication.
- **Trustworthy bytes.** Downloads are staged, checksum-verified and atomically
  committed; partial or corrupt content is never served as a hit.
- **A polished console.** A Next.js management UI, served by the Hoard process
  itself, shows hits, misses, bandwidth saved, cached packages and live activity.

Hoard is a cache, not a private package registry or publishing platform.

## Try the alpha

Releases are on the [releases page](https://github.com/Stonefly-Labs/Hoard-Cld/releases)
(prereleases named `v0.0.N-alpha`). The commands below need `0.0.2-alpha` or later.

**Container.** Publish the port on the host's loopback and keep data in a named volume:

```bash
docker run -d --name hoard -p 127.0.0.1:8484:8484 -v hoard-data:/data \
  --read-only --cap-drop ALL --security-opt no-new-privileges \
  ghcr.io/stonefly-labs/hoard:0.0.2-alpha
curl http://127.0.0.1:8484/health/ready      # Healthy
docker logs hoard                            # summary and the exposure warning
```

The warning in the logs is expected: inside the container Hoard listens on `0.0.0.0` with
TLS mode `proxy`. `-p 127.0.0.1:8484:8484` keeps it on the host's loopback. Plain
`-p 8484:8484` publishes it on every host interface, and Docker's port publishing bypasses
host firewalls such as ufw.

To reach the container from the network, put a TLS-terminating reverse proxy in front of
it, or serve HTTPS from Hoard with a mounted certificate:

```bash
docker run -d --name hoard -p 8484:8484 -v hoard-data:/data -v /etc/hoard/tls:/tls:ro \
  -e HOARD_LISTEN__TLS__MODE=https \
  -e HOARD_LISTEN__TLS__CERTIFICATEFILE=/tls/cert.pem \
  -e HOARD_LISTEN__TLS__KEYFILE=/tls/key.pem \
  ghcr.io/stonefly-labs/hoard:0.0.2-alpha
```

The image sets its listen address, TLS mode and data directory through `HOARD_*`
variables. Only `-e HOARD_...` and arguments after the image name override them; a mounted
`hoard.json` does not, because the file ranks below the environment.

**Executable.** Download your platform's executable and `SHA256SUMS` from the release,
then verify and run it:

```bash
sha256sum -c SHA256SUMS --ignore-missing      # or: shasum -a 256 -c SHA256SUMS --ignore-missing
chmod +x hoard-0.0.2-alpha-linux-x64
./hoard-0.0.2-alpha-linux-x64 &              # creates ./data, listens on localhost:8484
curl http://127.0.0.1:8484/health/ready       # Healthy
```

On macOS, also run `xattr -d com.apple.quarantine hoard-0.0.2-alpha-osx-arm64` before
starting it. On Windows, compare `Get-FileHash hoard-0.0.2-alpha-win-x64.exe` with the
matching line in `SHA256SUMS`. The checksums show that a download is intact, not who built
it.

**Options.** Each option has a file key, an environment variable and (except the password)
a command-line form; [Development](docs/DEVELOPMENT.md#configuration) has the full rules.

| File key | Environment | Command line | Default |
| --- | --- | --- | --- |
| `listen.address` | `HOARD_LISTEN__ADDRESS` | `--listen:address` | `localhost` |
| `listen.port` | `HOARD_LISTEN__PORT` | `--listen:port` | `8484` |
| `listen.tls.mode` | `HOARD_LISTEN__TLS__MODE` | `--listen:tls:mode` | none (`https` or `proxy`) |
| `listen.tls.certificateFile` | `HOARD_LISTEN__TLS__CERTIFICATEFILE` | `--listen:tls:certificateFile` | none |
| `listen.tls.keyFile` | `HOARD_LISTEN__TLS__KEYFILE` | `--listen:tls:keyFile` | none |
| `listen.tls.password` | `HOARD_LISTEN__TLS__PASSWORD` | not accepted | none |
| `data.path` | `HOARD_DATA__PATH` | `--data:path` | `./data` |
| (config file) | `HOARD_CONFIG` | `--config` | `./hoard.json` if present |

## Build and test

Requires the .NET 10 SDK. See [Development](docs/DEVELOPMENT.md) for the smoke tests.

```bash
dotnet build Hoard.slnx
dotnet test --solution Hoard.slnx
dotnet run --project src/Hoard.Host     # http://127.0.0.1:8484/health/ready
```

## Documentation

| Document | Contents |
| --- | --- |
| [Product vision](docs/PRODUCT_VISION.md) | Why Hoard exists and the experience it aims for |
| [Product charter](docs/PRODUCT_CHARTER.md) | Scope, MVP, non-goals, deployment profiles |
| [Product specification](docs/PRODUCT_SPEC.md) | Cache lifecycle, protocols, API, UI and security requirements |
| [Architecture](docs/ARCHITECTURE.md) | Modular monolith, provider categories, dependency rules |
| [ADR-0001](docs/adr/0001-modular-provider-architecture.md) | Composable modular monolith with vertical slices |
| [ADR-0002](docs/adr/0002-explicit-tls-posture.md) | Explicit TLS posture for network-exposed listeners |
| [Spec backlog](docs/HOARD_SPECKIT_MASTER_BACKLOG.md) | Ordered feature specifications and Spec Kit prompts |
| [Spec progress](docs/spec-progress.md) | Where each spec stands |
| [Development](docs/DEVELOPMENT.md) | Change flow and contributor tooling |
| [Releasing](docs/RELEASING.md) | Versioning, tags, the release pipeline and published artifacts |

## License

[MIT](LICENSE) © Christopher House
