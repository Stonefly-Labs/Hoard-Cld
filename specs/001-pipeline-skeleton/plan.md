# Implementation Plan: Pipeline and solution skeleton

**Branch**: `feature/001-pipeline-skeleton` | **Date**: 2026-10-31 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-pipeline-skeleton/spec.md`

## Summary

This spec creates a .NET 10 solution with only `Hoard.Host` (a health-only ASP.NET Core
app bound to `localhost:8484`), an empty packable `Hoard.Core`, and three test projects.
It adds two GitHub Actions workflows:

- `ci.yml` gates every PR with build, tests (including architecture rules), and
  binary and container smoke tests.
- `release.yml` runs on an annotated `v0.0.N-alpha` tag. It checks the tag is on `main`,
  builds and tests, publishes four self-contained single-file executables and runs smoke
  tests on them, builds a multi-arch chiseled image from those same binaries, and only
  then publishes the package to GitHub Packages (skipping existing versions), the GHCR
  image (never moving `latest`) and the GitHub prerelease with `SHA256SUMS`.

The architecture rules read Core's restore graph (`project.assets.json`), so they catch
direct and transitive references to projects that do not exist yet (research R2, R3).

## Technical Context

**Language/Version**: C# / .NET 10 (`global.json`: SDK 10.0.100, `rollForward: latestFeature`); bash for CI scripts

**Primary Dependencies**: ASP.NET Core (Minimal APIs, built-in health checks). Tests: xUnit v3, Microsoft.AspNetCore.Mvc.Testing. CI: GitHub Actions, Docker Buildx, skopeo, `gh`

**Storage**: N/A

**Testing**: `dotnet test --solution Hoard.slnx` (architecture, host, release-guard tests); bash smoke scripts for packaged artifacts

**Target Platform**: Executables for linux-x64, linux-arm64, osx-arm64, win-x64; container for linux/amd64 and linux/arm64

**Project Type**: Web service (host) plus build/release pipeline

**Performance Goals**: Container and native executable healthy within 30 s of start in CI (SC-004)

**Constraints**: Loopback-only listener in every distribution (FR-003). No configuration surface (spec 002). Publish nothing unless every verify job passes. Never overwrite a published artifact or move a tag. Only `GITHUB_TOKEN` as a credential

**Scale/Scope**: 2 product projects, 3 test projects, 2 workflows, 3 scripts, 1 Dockerfile

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | How this plan complies |
|---|---|---|
| I. Cache, not registry | ✅ | No package hosting. Publishing `Hoard.Core` to GitHub Packages is Hoard's own distribution, not a registry feature |
| II. Trustworthy bytes | ✅ (n/a to runtime) | No cache yet. In the same spirit, the release publishes only verified artifacts, with checksums |
| III. Secure by default | ✅ | The listener is fixed in code: config-backed Kestrel endpoints are dropped, `ListenLocalhost(8484)` is set, and `PreferHostingUrls(false)`. Smoke tests prove every listener is loopback, including under a hostile environment. The container runs non-root, read-only and with all capabilities dropped. Workflows have least-privilege permissions, SHA-pinned actions and no expression injection. No secrets beyond `GITHUB_TOKEN` |
| IV. Native protocol fidelity | ✅ (n/a) | No protocol |
| V. Simplicity / YAGNI | ✅ | Core is empty. No Abstractions, Hosting or config. No arch-test library (the restore graph plus Core's assembly references suffice). No health-probe CLI mode. The extra `Hoard.Release.Tests` project exists only to test the release guards the spec requires. Dependabot exists only to keep the pins current |
| VI. Enforced modular boundaries | ✅ | Architecture tests enforce the Core rules by naming pattern, including future projects; built-in DI, Minimal APIs |
| VII. Test-first | ✅ | Architecture rules and release guards start from failing tests. No cache or protocol code exists yet |
| VIII. Small specs, CD | ✅ | One PR. CI gates PRs. `release.yml` produces the alpha. The full release path is dry-run on this PR (R12, a plan decision) |
| IX. Truthful docs | ✅ | README status section (also the release notes source), RELEASING.md, DEVELOPMENT.md and CLAUDE.md commands are updated in this PR |
| X. Console | ✅ (n/a) | Out of scope |

Post-design re-check: no violations; Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/001-pipeline-skeleton/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── health-endpoint.md
│   └── release-outputs.md
└── tasks.md            # created by /speckit-tasks
```

### Source Code (repository root)

```text
Hoard.slnx
global.json
Directory.Build.props          # net10.0, nullable, warnings-as-errors, IsPackable=false, package metadata
Directory.Packages.props       # central package versions
.dockerignore
src/
├── Hoard.Host/
│   ├── Hoard.Host.csproj      # Microsoft.NET.Sdk.Web; no project references
│   └── Program.cs             # health checks, ListenLocalhost(8484), MapHealthChecks("/health")
└── Hoard.Core/
    └── Hoard.Core.csproj      # IsPackable=true, <Version>0.0.1-alpha</Version>; no source files
tests/
├── Hoard.Architecture.Tests/  # CoreDependencyRules (assets graph + csproj <Reference> + dll refs); fixtures; restore-based violation test; ProjectReference to Core with ReferenceOutputAssembly=false
├── Hoard.Host.Tests/          # /health = 200 Healthy; other paths 404 (WebApplicationFactory)
└── Hoard.Release.Tests/       # verify-tag.sh against temp git repos (skipped on Windows)
eng/
├── release/
│   ├── verify-tag.sh          # env-only input; pattern, annotated, tag SHA = GITHUB_SHA, reachable from origin/main; outputs version
│   └── status-notes.sh        # extract README release-status section
└── smoke/
    ├── smoke-binary.sh
    └── smoke-container.sh
deploy/docker/Dockerfile       # runtime-deps chiseled (digest-pinned), USER $APP_UID, COPY --chmod=0555 prebuilt exe by TARGETARCH
.github/
├── dependabot.yml             # github-actions, docker, nuget
├── workflows/ci.yml           # PR + main: build-test, smoke (linux-x64 binary, amd64 image)
└── workflows/release.yml      # tag: guard → verify → binaries(matrix) → container (OCI archive) → publish; PR dry-run on pipeline changes
```

**Structure decision.** This follows the `src/`, `tests/` and `deploy/docker` layout from
PRODUCT_SPEC §11, with only the projects this spec needs. Tests are one project per
concern, named `Hoard.<Area>.Tests`. The §11 folders `tests/contracts`, `integration`,
`protocol` and `composition` arrive with the specs that first need them. `Hoard.Host` does
not reference `Hoard.Core` yet because nothing consumes it. The reference is added by the
first spec that puts behavior in Core.

## Key design points

1. **Architecture rule** (R2, R3):
   - `CoreDependencyRules` evaluates Core's restore graph. It matches library names and,
     for project references, project file names. It applies five deny patterns plus
     "no `Hoard.*` other than `Hoard.Abstractions`" (ARCHITECTURE §3).
   - Second layer: Core's `.csproj` must have no raw `<Reference>` items, and
     `Hoard.Core.dll`'s assembly references must match no rule.
   - A missing assets file, or one for the wrong project, fails the test rather than
     passing it.
   - Fixture tests cover each pattern plus a transitive case and a renamed-`PackageId`
     case. A temp-dir `dotnet restore` test, outside the repo, proves a real violation
     is caught.
   - Messages read `Hoard.Core must not reference 'Hoard.Persistence.Fake' (rule:
     Hoard.Persistence.*)`.
2. **Loopback** (R5, R8):
   - The listener is fixed in code. Config-backed endpoints are dropped,
     `ListenLocalhost(8484)` is set, and `PreferHostingUrls(false)`.
   - Smoke scripts run clean and with a hostile env. They require at least one listener
     and every listener on loopback: `lsof` for the binary, `/proc/net/tcp*` from a
     helper in the container's network namespace. A bridge probe, with a positive
     control, must get connection refused.
3. **Release ordering** (R9, R10):
   - The guard reads the tag only from env and validates it first. It checks the tag is
     annotated, points at `GITHUB_SHA` and is on `main`, then outputs the version used
     downstream.
   - `publish` runs only on a tag push after the guard succeeded and every verify job
     passed. It publishes packages (`--skip-duplicate`, key via env), then the OCI
     archive (skipped if the tag exists), then the GitHub release (create, or upload
     missing assets only).
   - It never writes tags or `latest`, and never uses `--clobber`.
   - The guard prevents mistakes; it is not access control (write access is the trust
     boundary).
4. **Same bytes everywhere** (R6, R7):
   - Executables are published once per RID, architecture-checked with `file`,
     smoke-tested where runnable and checksummed.
   - The image is built once into an OCI archive, smoke-tested from that archive, and
     pushed from it.
   - `pack` never receives the release version.
5. **Release notes** (R11): the README `release-status` section plus generated PR notes.
6. **Pre-merge proof** (R12, plan decision): `release.yml` dry-runs on PRs that change
   the pipeline.

## Documentation changes (same PR)

- `README.md`:
  - Replace "Status: planning" with a marked `release-status` section. Working: the
    health-only host, container and executables, loopback only. Planned: the rest.
    Alphas are public (R14).
  - How to run the container and the executable, including `chmod +x` and macOS
    quarantine removal.
  - How to build and test.
- `docs/RELEASING.md`:
  - Drop the "intended" framing and describe the actual workflow: guard checks, publish
    order (packages → image → release), artifact names, the `SHA256SUMS` file (one line
    per executable; proves integrity, not origin), the notes source, re-run behavior, the
    PR dry run, and the fact that the guard is not access control.
  - Correct the CI section to what runs today: build, tests, architecture checks and
    smoke tests. No `dotnet` client or console checks yet.
- `docs/DEVELOPMENT.md`: replace "No application code exists yet" with the build/test
  commands and the PR checks.
- `CLAUDE.md`: a "Commands" section (`dotnet build Hoard.slnx`, `dotnet test --solution Hoard.slnx`,
  smoke scripts), which `drive-speckit`'s implement step expects.

Deviation: tests run on Microsoft.Testing.Platform, so the command is
`dotnet test --solution Hoard.slnx` (see the implementation notes in `tasks.md`).

## Risks and follow-ups

- **No enforced required checks yet** (R13). Branch protection becomes available once the
  repo is public; until the maintainer enables it, merging only on green CI follows
  `docs/RELEASING.md`.
- **Guard is not access control** (R9). Anyone with write access can tag a branch with
  an edited workflow. Write access is the trust boundary.
- **GHCR first push.** The `hoard` package does not exist yet. The OCI `source` label
  links it to this repo on first push. If the org restricts package creation by
  `GITHUB_TOKEN`, the first release fails at `publish`. That gets recorded as a release
  blocker and fixed in org settings, not by retagging.
- **Visibility** (R14): alphas are public. Making the repo, image and package public is
  a manual, one-time maintainer setting; the first release cannot be pulled anonymously
  until the image is set public after its first push.
- **Repo must be public before implement pushes `release.yml`.** Otherwise the PR dry
  run's macOS and arm64 jobs bill private-repo minutes.
- **Windows smoke under Git Bash** (R8): PID mapping and PowerShell listener listing are
  the least-proven part of the scripts; T023 exercises them on the PR dry run.
- **Re-run checksum caveat** after a mid-upload failure (R10). The remedy is a new
  version.

## Complexity Tracking

No constitution violations to justify.
