# Implementation Plan: Host, configuration and deployment profiles

**Branch**: `feature/002-host-configuration-profiles` | **Date**: 2026-10-31 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-host-configuration-profiles/spec.md`

## Summary

`Hoard.Host` gets a validated option set from three sources, with command line >
environment > `hoard.json`:

- the listen address and port;
- the TLS mode and certificate;
- the data directory;
- the config file location.

Any unknown, malformed or contradictory input fails startup with exit code 2 and one report
naming every problem with its source. Nothing is opened in that case.

**Hosting.** The host is built with `WebApplication.CreateEmptyBuilder`, so no framework
configuration source exists (R1), and Kestrel is given exactly one endpoint from the
validated options.

**Exposure.** Binding beyond loopback requires TLS mode `https` (a certificate checked
before start; HTTPS only) or `proxy` (ADR-0002), and logs the network-exposure /
anonymous-reads warning.

**Health.** `/health/live` and `/health/ready` replace `/health`. Readiness tracks startup,
a per-request data-directory write check, and shutdown.

**Shutdown.** Hoard drains for up to 5 s and exits 0.

**Container.** The image declares `0.0.0.0` + `proxy` + `/data` through `HOARD_*`
variables, and both smoke scripts are reworked around the new behavior.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK per `global.json`); bash for smoke scripts

**Primary Dependencies**: ASP.NET Core shared framework only: Kestrel, built-in health
checks, `Microsoft.Extensions.Logging.Console`, `System.Text.Json`, and
`System.Security.Cryptography.X509Certificates`. No new packages; `Microsoft.AspNetCore.Mvc.Testing`
is removed (R12)

**Storage**: One data directory. Only the write check touches it in this spec

**Testing**: xUnit v3 on Microsoft.Testing.Platform (`dotnet test --solution Hoard.slnx`):
unit, in-process real-Kestrel, and process-level tests in `Hoard.Host.Tests`; the bash
smoke scripts for packaged artifacts

**Target Platform**: The four spec 001 executables (linux-x64, linux-arm64, osx-arm64,
win-x64) and the linux/amd64 + linux/arm64 image

**Project Type**: Web service host

**Performance Goals**: Healthy within 30 s of start (SC-001); stop within 10 s (SC-006)

**Constraints**:
- Loopback by default.
- No fallback address, port, directory or plain HTTP.
- No secret or unknown-option value in any output.
- One listener per instance.
- No `RUN` steps in the Dockerfile.

**Scale/Scope**: 1 product project changed (`Hoard.Host`), 1 test project changed,
2 smoke scripts, 1 Dockerfile, 4 docs

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | How this plan complies |
|---|---|---|
| I. Cache, not registry | ✅ (n/a) | No package behavior |
| II. Trustworthy bytes | ✅ (n/a) | No cache content. The data directory gets only a self-deleting write-check file |
| III. Secure by default | ✅ | Loopback default (`localhost`). Non-loopback needs explicit address + TLS mode or startup fails (ADR-0002). No framework source can touch the listener (R1). `https` mode has no plain-HTTP endpoint and no fallback on certificate errors. The anonymous-reads warning is logged. The password and unknown-option values are never printed (R9). This spec ships the TLS baseline that team exposure needs |
| IV. Native protocol fidelity | ✅ (n/a) | No protocol. Forwarded headers and the public base URL stay with spec 007 (ADR-0002 open question) |
| V. Simplicity / YAGNI | ✅ | Eight options, each consumed here. No options framework, no `IOptions` validation pipeline, no `System.CommandLine`, no shutdown-timeout option. Core stays empty. A test package is removed |
| VI. Enforced modular boundaries | ✅ | All code in `Hoard.Host`. Health is a vertical slice in `Features/Health/`. Host composition (options, listener, data directory) sits in `Startup/`, as it is not an endpoint. Built-in DI and Minimal APIs. Existing architecture tests unchanged and passing |
| VII. Test-first | ✅ | Option reader and validator, certificate checks and health behavior start from failing tests. There is no cache or protocol code |
| VIII. Small specs, CD | ✅ | One PR. CI gates it. Smoke and Dockerfile changes trigger the release dry run on all native runners |
| IX. Truthful docs | ✅ | README, DEVELOPMENT.md, CLAUDE.md commands and the release-status notes are updated (see below) |
| X. Console | ✅ (n/a) | Out of scope |

Post-design re-check: no violations; Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/002-host-configuration-profiles/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── configuration-and-startup.md
│   ├── health-endpoints.md
│   └── container-image.md
└── tasks.md            # created by /speckit-tasks
```

### Source Code (repository root)

```text
src/Hoard.Host/
├── Program.cs                       # thin: HoardStartup.RunAsync(args, environment, working dir) → exit code
├── Startup/
│   ├── OptionNames.cs               # the eight options: file path, env name, CLI name, secret flag
│   ├── OptionReader.cs              # CLI / HOARD_* env / JSON file → (option, value, source) + read errors (R2)
│   ├── OptionValidator.cs           # values, cross-option rules, preconditions → HoardOptions or errors (data-model)
│   ├── HoardOptions.cs              # validated options record
│   ├── ListenAddress.cs             # parse + loopback classification (R4)
│   ├── CertificateLoader.cs         # PEM / PKCS#12 load, key, validity, Windows re-export (R5)
│   ├── DataDirectory.cs             # resolve, create, write check (R6)
│   ├── HoardApp.cs                  # CreateEmptyBuilder, Kestrel endpoint, logging, shutdown timeout; calls each slice's Add/Map
│   ├── StartupReport.cs             # stderr problem report; summary + exposure warning text (R9)
│   └── HoardStartup.cs              # validate → build → start → summary → wait → exit codes (R8)
└── Features/Health/
    ├── HealthEndpoints.cs           # AddHealth + MapHealth: /health/live, /health/ready (GET, HEAD)
    ├── DataDirectoryCheck.cs
    └── StartupCheck.cs              # Serving between ApplicationStarted and ApplicationStopping
tests/Hoard.Host.Tests/
├── Startup/                         # OptionReader, OptionValidator, ListenAddress, CertificateLoader, DataDirectory tests
├── Features/Health/                 # in-process real-Kestrel tests (incl. drain, HTTPS)
├── Process/                         # dotnet Hoard.Host.dll: exit codes, stderr, summary, SIGTERM, restart, secrets
└── Support/                         # free-port helper, test certificates, host process runner
deploy/docker/Dockerfile             # + ENV HOARD_*, EXPOSE 8484, /data owned by $APP_UID (R10)
eng/smoke/smoke-binary.sh            # new health paths, exposure + refusal runs, --urls refused (R11)
eng/smoke/smoke-container.sh         # reachable on bridge, override, volume, https runs (R11)
```

`HealthEndpointTests.cs` (spec 001) is replaced by the tests under `Features/Health/`.
`Microsoft.AspNetCore.Mvc.Testing` is removed from the test project and from
`Directory.Packages.props`.

**Structure decision.** Everything stays in `Hoard.Host`. No new project, and no
`Hoard.Core` code, because nothing outside the host consumes options or the data directory
yet. Storage specs 003/004 decide what moves to Core when they first need it. Product
spec §11's `tests/integration` folder is not created. The process tests run from
`Hoard.Host.Tests` because they test only the host.
The composition folder is `Startup/`, not `Hosting/`, to avoid confusion with the
`Hoard.Hosting` assembly reserved by ARCHITECTURE §3. When spec 007 creates that
assembly, it decides whether option validation and readiness aggregation move there.

## Key design points

1. **One way in** (R1, R2, R3, R9):
   - `HoardStartup` reads the args, `HOARD_*` environment and the config file through
     `OptionReader`, then validates through `OptionValidator`.
   - The empty builder has no other configuration, so FR-003 holds by construction.
   - The smoke runs keep spec 001's hostile environment and appsettings. `--urls` is
     refused as an unknown option.
   - No secret leaks through diagnostics: the token after a known option is always its
     value; positional and malformed arguments are reported by position; exception and
     JSON parser messages are never printed.
2. **Collect, then fail** (data-model rules; R9):
   - Reading, per-option, cross-option and precondition errors all go into one list.
     Preconditions run only when the options they need are valid.
   - Any error → stderr report, exit 2, before Kestrel exists.
   - A bind failure → exit 1 with the address named.
3. **Exactly one endpoint** (R4, R5):
   - `localhost` → `ListenLocalhost(port)`; an IP → `Listen(ip, port)`.
   - In `https` mode, both kinds get `UseHttps(cert)`. A thumbprint test proves the
     configured certificate is the one served.
   - Addresses must round-trip canonically, and IPv4-mapped forms are rejected (R4).
   - No `ListenAnyIP`, no config loader, `PreferHostingUrls(false)`.
4. **Honest readiness** (R7):
   - Readiness = started ∧ not stopping ∧ write check passes, evaluated when requested.
     Concurrent probes share one check, which has a 2 s timeout (R6).
   - Liveness has no checks.
5. **Bounded shutdown** (R8): `ShutdownTimeout` is 5 s. A signal during startup exits 0
   without a stack trace.
6. **Container posture by configuration** (R10): the image's `HOARD_*` environment makes
   it an ordinary configured exposure, so the same validation, warning and override rules
   apply.

## Documentation changes (same PR)

- **`README.md`.**
  - Release-status section: "working" now lists configuration (sources, precedence,
    options), loopback default, explicit exposure with TLS modes, health endpoints and the
    container run command. Remove "the container is unreachable from the host".
  - "Try the alpha": the container command with `-p 127.0.0.1:8484:8484 -v
    hoard-data:/data`, `/health/ready`, the exposure warning, and the `https` and reverse-proxy
    routes for network exposure. Note that Docker port publishing bypasses host firewalls
    such as ufw.
  - A short options table linking to the contract-level detail in `docs/DEVELOPMENT.md`.
- **`docs/DEVELOPMENT.md`.**
  - The options table (all three name forms, defaults), precedence, exit codes and the
    health endpoints.
  - Secret handling: the password is accepted only from the environment or a mode-0600
    file, and the command-line form is refused. `-e` values are visible to `docker
    inspect`.
  - What each smoke script now proves.
  - Local HTTPS testing with `openssl`.
- **`CLAUDE.md`.** Smoke-script descriptions: the binary is loopback by default and
  refuses unsafe exposure; the image is reachable on 8484 with `proxy` declared, non-root,
  and adds no ports.
- **`docs/RELEASING.md`.** Change only if it describes the smoke checks' content; they are
  named, not described, so likely no change.

## Risks and follow-ups

- **`CreateEmptyBuilder` lifetime** (R1). If the console lifetime isn't registered,
  SIGTERM wouldn't stop the host. The first host task proves it with the process-level
  SIGTERM test; the fallback is named in R1.
- **Windows PEM keys** (R5). `dotnet test` runs only on Linux, so the binary smoke's
  `https` run on the win-x64 and osx-arm64 runners (release dry run) is the only proof
  there. The PKCS#12 round-trip is the known remedy. After a crash it can leave the key in
  the user's key store; this is documented.
- **Password not on the command line** (maintainer decision at the plan gate). Spec FR-008
  now refuses `--listen:tls:password`, so the password comes only from the environment or
  the file, and never appears in process listings.
- **`/data` ownership without `RUN`** (R10). Proven by the named-volume smoke run; the
  fallback is named.
- **Proxy mode is a declaration.** Hoard can't verify a proxy exists (ADR-0002), and URLs
  advertised behind a proxy remain spec 007's open decision.
- **Two instances, one data directory** are not prevented (spec edge case); storage specs
  revisit this.

## Complexity Tracking

No constitution violations to justify.
