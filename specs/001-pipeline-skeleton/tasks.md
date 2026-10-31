---
description: "Task list for spec 001 — Pipeline and solution skeleton"
---

# Tasks: Pipeline and solution skeleton

**Input**: Design documents from `specs/001-pipeline-skeleton/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: Required. Constitution VII (test-first) and the spec's FR-007, US3 and US4 call for
them. Architecture rules, host behavior and release guards are written as failing tests first.
Packaged artifacts are proven by smoke scripts, which also cover the failure paths.

**Organization**: grouped by user story. Phases run in **dependency order**, not strict priority
order. US2 (P1, release) comes last because it puts together the smoke scripts (US3) and the tag
guard (US4). See Dependencies.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4 from spec.md
- Paths are relative to the repository root

---

## Phase 1: Setup (shared infrastructure)

**Purpose**: solution conventions every project inherits (research R1).

- [X] T001 Create `global.json` pinning SDK `10.0.100` with `"rollForward": "latestFeature"`
- [X] T002 [P] Create `Directory.Build.props`: `TargetFramework` `net10.0`, `Nullable` enable, `ImplicitUsings` enable, `TreatWarningsAsErrors` true, `IsPackable` false, package metadata (`Authors`, `PackageLicenseExpression` `MIT`, `RepositoryUrl` `https://github.com/Stonefly-Labs/Hoard-Cld`, `PackageProjectUrl`). It MUST NOT set `Version` (R1)
- [X] T003 [P] Create `Directory.Packages.props` with `ManagePackageVersionsCentrally` true. Pin third-party versions only: `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, and `Microsoft.AspNetCore.Mvc.Testing` on the 10.0.x line. Do NOT add `Microsoft.Testing.Extensions.CodeCoverage` or FluentAssertions (R4 licensing)
- [X] T004 [P] Add .NET build outputs (`bin/`, `obj/`, `out/`, `*.oci.tar`) to `.gitignore` and create `.dockerignore`. The `.dockerignore` excludes everything except the published-executables build context (R7)
- [X] T005 Create `src/Hoard.Core/Hoard.Core.csproj`: `IsPackable` true, `<Version>0.0.1-alpha</Version>`, `PackageId` `Hoard.Core`, a short `Description`, no source files and no references (FR-004, FR-015, spec Clarifications)
- [X] T006 Create `src/Hoard.Host/Hoard.Host.csproj` (`Microsoft.NET.Sdk.Web`, `AssemblyName` `Hoard.Host`, no project references, not packable) with a placeholder `Program.cs` that only builds the app and calls `Run` (no endpoints yet)
- [X] T007 Create the three test projects. `tests/Hoard.Architecture.Tests/Hoard.Architecture.Tests.csproj` has `<ProjectReference Include="../../src/Hoard.Core/Hoard.Core.csproj" ReferenceOutputAssembly="false" />`. `tests/Hoard.Host.Tests/Hoard.Host.Tests.csproj` references `Hoard.Host` and `Microsoft.AspNetCore.Mvc.Testing`. `tests/Hoard.Release.Tests/Hoard.Release.Tests.csproj` has no product references. All three use xUnit v3 (R4)
- [X] T008 Create `Hoard.slnx` containing exactly `Hoard.Core`, `Hoard.Host` and the three test projects (FR-001), and confirm `dotnet build Hoard.slnx` succeeds with zero warnings

**Checkpoint**: the solution builds. It has no behavior and no tests yet.

---

## Phase 2: Foundational

No separate foundational work. Phase 1 is the shared prerequisite. Each story's tests come first
inside its own phase.

---

## Phase 3: User Story 1 — Every pull request is gated (P1) 🎯 MVP

**Goal**: every PR to `main` gets an automatic build, test and architecture-check status (FR-005–FR-008).

**Independent test**: open a PR and see `build-test` pass. A PR with a failing test, or a Core
reference to `Hoard.Persistence.*`, fails and names the cause (US1 scenarios 1–4; quickstart §1, §4).

### Tests first (must fail before implementation) ⚠️

- [X] T009 [P] [US1] Write fixture-based failing tests in `tests/Hoard.Architecture.Tests/CoreDependencyRulesFixtureTests.cs`. There is one test per deny pattern (`Hoard.Persistence.*`, `Hoard.Storage.*`, `Hoard.Protocols.*`, `Hoard.Identity.*`, `Microsoft.EntityFrameworkCore*`), each feeding a minimal `project.assets.json` document from `tests/Hoard.Architecture.Tests/Fixtures/`. Extra cases:
  - a transitive case (the forbidden library reached only through another library)
  - a renamed-`PackageId` project case (library name harmless, `path`/`msbuildProject` is `Hoard.Persistence.Sqlite.csproj`)
  - the sixth rule: any `Hoard.*` other than `Hoard.Abstractions` (e.g. `Hoard.Hosting`) fails, and `Hoard.Abstractions` passes
  - case-insensitive matching
  - an assets document whose `project.restore.projectName` is not `Hoard.Core` fails
  - a missing assets file fails (never passes vacuously)

  Each failure asserts the exact message shape `Hoard.Core must not reference '<name>' (rule: <pattern>)` (FR-005–FR-007, SC-002, R2, R3)
- [X] T010 [P] [US1] Write the end-to-end violation test in `tests/Hoard.Architecture.Tests/CoreDependencyRulesRestoreTests.cs`. It creates a temp directory **outside the repo** containing a `Hoard.Core`-shaped project with a `ProjectReference` to a `Hoard.Persistence.Fake` project, runs `dotnet restore` (project references only, no network), passes the generated `obj/project.assets.json` through `CoreDependencyRules.Evaluate`, and asserts failure naming `Hoard.Persistence.Fake`. It cleans up the temp directory (R3)
- [X] T011 [P] [US1] Write the real-repo tests in `tests/Hoard.Architecture.Tests/CoreDependencyRulesTests.cs`:
  - Core's actual assets file (path read from an assembly attribute, see T013) passes every rule
  - `src/Hoard.Core/Hoard.Core.csproj` has no `<Reference>` items
  - the referenced assemblies of the built `Hoard.Core.dll` match no rule (R2 second layer)
- [X] T012 [P] [US1] Write host tests in `tests/Hoard.Host.Tests/HealthEndpointTests.cs` using `WebApplicationFactory<Program>`:
  - `GET /health` returns `200`, `text/plain`, body `Healthy`
  - `GET /` and `GET /nope` return `404`

  Follows contracts/health-endpoint.md, FR-002, US3 scenario 4
- [X] T013 [US1] Run `dotnet test Hoard.slnx` and confirm T009–T012 fail (compile failure or red) for the expected reasons. Record the result in the implement log

### Implementation

- [X] T014 [US1] Implement `tests/Hoard.Architecture.Tests/CoreDependencyRules.cs`: `Evaluate(string assetsJsonPath)` returns violations. It matches `libraries` keys (name before `/`) and, for `type: project`, the project file name. It applies the five deny prefixes plus "no `Hoard.*` except `Hoard.Abstractions`", case-insensitively, and fails on a missing file or a wrong `projectName` (R2)
- [X] T015 [US1] Pass Core's `ProjectAssetsFile` path to the test assembly. In `tests/Hoard.Architecture.Tests/Hoard.Architecture.Tests.csproj`, add an MSBuild target that reads `Hoard.Core`'s `ProjectAssetsFile` and emits an `AssemblyMetadataAttribute` (also Core's project path and built dll path for T011). No hard-coded paths (R2)
- [X] T016 [US1] Implement `src/Hoard.Host/Program.cs`:
  - `AddHealthChecks()` and `MapHealthChecks("/health")`, with no other endpoints
  - `builder.WebHost.PreferHostingUrls(false)`
  - `ConfigureKestrel(o => { o.Configure(new ConfigurationBuilder().Build()); o.ListenLocalhost(8484); })`
  - `public partial class Program;` only if the SDK does not generate it

  Follows R5, FR-002, FR-003
- [X] T017 [US1] Run `dotnet test Hoard.slnx`. All tests pass with zero build warnings, and `dotnet run --project src/Hoard.Host` answers `curl http://127.0.0.1:8484/health` with `Healthy` (quickstart §1–2)
- [X] T018 [US1] Create `.github/workflows/ci.yml` with the `build-test` job, triggered on `pull_request` to `main` and `push` to `main`. Requirements:
  - workflow-level `permissions: contents: read`
  - `actions/checkout` and `actions/setup-dotnet` (from `global.json`) pinned by commit SHA with a version comment, and `persist-credentials: false`
  - steps: restore, `dotnet build Hoard.slnx --no-restore`, then `dotnet test Hoard.slnx --no-build` with a logger that names failing tests in the job output
  - no `${{ }}` carrying ref, tag or PR values inside `run:` (FR-008, R13)
- [X] T019 [P] [US1] Create `.github/dependabot.yml` for ecosystems `github-actions`, `docker` (`/deploy/docker`) and `nuget` (`/`), weekly. It has a consumer: the SHA and digest pins T018–T036 introduce would otherwise go stale (plan Constitution Check V, R13)

**Checkpoint**: US1 is complete once `build-test` runs green on PR #4.

---

## Phase 4: User Story 3 — The released build actually starts (P2)

**Goal**: the packaged executable and image start, report healthy within 30 s, expose only
`/health`, and listen on loopback only, even under a hostile environment (FR-003, FR-009, SC-004).

**Independent test**: `eng/smoke/smoke-binary.sh` and `eng/smoke/smoke-container.sh` pass
locally against a `linux-x64` publish and an amd64 image (quickstart §3).

### Smoke scripts (they are the tests; write and run them against the artifact first)

- [X] T020 [P] [US3] Write `eng/smoke/smoke-binary.sh <exe>` (bash, `set -euo pipefail`, portable to Linux and macOS):
  - **Clean run**: starts the executable in the background, polls `http://127.0.0.1:8484/health` for at most 30 s (SC-004), asserts `200` + `Healthy` and that `/anything-else` is `404`.
  - **Listeners**: lists listeners with `lsof -nP -a -p "$PID" -iTCP -sTCP:LISTEN`. It fails if there are none, or if any address is not `127.0.0.1` or `[::1]`.
  - **Hostile runs** (gap fold-in, security CHK002/CHK003) repeat every assertion with:
    - (a) env `ASPNETCORE_URLS=http://0.0.0.0:8080`, `ASPNETCORE_HTTP_PORTS=8081`, `ASPNETCORE_HTTPS_PORTS=8443`, `DOTNET_URLS=http://0.0.0.0:8082`, `ASPNETCORE_PREFERHOSTINGURLS=true`, `Kestrel__Endpoints__H__Url=http://0.0.0.0:8485`
    - (b) command-line `--urls http://0.0.0.0:8086`
    - (c) an `appsettings.json` placed next to a copy of the executable in a temp dir, declaring a `Kestrel:Endpoints` entry on `0.0.0.0:8487`
  - **Windows** (runs under Git Bash on `windows-latest`): maps `$!` to the Windows PID via `/proc/$PID/winpid`, lists listeners with `powershell -NoProfile -Command "Get-NetTCPConnection -State Listen -OwningProcess <winpid>"`, and accepts only `127.0.0.1`/`::1`. Linux and macOS use `lsof`.
  - Always kills the process and removes temp files via `trap`. Prints the failing assertion and the listener table on failure (R8).
- [X] T021 [P] [US3] Create `deploy/docker/Dockerfile`:
  - `FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled@sha256:<digest>`, using the current published digest
  - `ARG TARGETARCH` maps `amd64` → `linux-x64` and `arm64` → `linux-arm64`, then `COPY --chmod=0555` the prebuilt `hoard-*-<rid>` executable from the `bin` build context to `/app/hoard` (root-owned)
  - `USER $APP_UID`, `ENTRYPOINT ["/app/hoard"]`, OCI label `org.opencontainers.image.source=https://github.com/Stonefly-Labs/Hoard-Cld`
  - no `RUN` steps; leave the base image's `ASPNETCORE_HTTP_PORTS` untouched so the smoke test proves the override (R7, security CHK018)
- [X] T022 [P] [US3] Write `eng/smoke/smoke-container.sh <image>`:
  - **Image checks**: asserts the image's `Config.User` is set and is not `root`/`0`. Runs the container detached with `--read-only --cap-drop ALL --security-opt no-new-privileges`, keeping base env.
  - **Inside view**: from a helper (busybox/curl) pinned by **multi-arch index digest** so the same script runs on amd64 and arm64 runners using `--network container:<id>`, polls `/health` for at most 30 s and asserts `200 Healthy` and the `404`. It reads `/proc/net/tcp` and `/proc/net/tcp6` and fails if there is no listening socket (state `0A`) or any is on a non-loopback address.
  - **Outside view**: a probe container on the same bridge must get *connection refused* (not a timeout) on `<container-ip>:8484`. A positive control first shows the probe reaches a helper listener on that bridge.
  - **Hostile run**: repeats everything with the hostile env from T020(a).
  - **Cleanup**: removes all containers and the network via `trap` (R8, security CHK009).
- [X] T023 [US3] Run the smoke scripts locally against a `linux-x64` (or host-RID) publish and an amd64 image built from it, as in quickstart §3. Confirm that the clean and hostile runs pass, and that a deliberately broken case fails the script: temporarily bind `0.0.0.0` in a scratch build, then revert. Record the outcome
- [X] T024 [US3] Add a `smoke` job to `.github/workflows/ci.yml` (`needs: build-test`, same pinning and permission rules). It publishes `linux-x64` with `dotnet publish src/Hoard.Host -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:DebugType=none -p:Version=0.0.0-ci`, renames the output to `hoard-0.0.0-ci-linux-x64`, and runs `eng/smoke/smoke-binary.sh`. It then builds the amd64 image with `docker buildx build -f deploy/docker/Dockerfile --platform linux/amd64 --build-context bin=<dir> --load` and runs `eng/smoke/smoke-container.sh` (FR-009, R13)

**Checkpoint**: on PR #4, `ci.yml` shows both `build-test` and `smoke` green.

---

## Phase 5: User Story 4 — Releases refuse unsafe inputs (P2)

**Goal**: a tag that is malformed, lightweight, mismatched, or not on `main` is refused before
anything is published (FR-010, FR-011, FR-018, R9).

**Independent test**: `dotnet test Hoard.slnx` runs the guard tests against temporary git
repositories (US4 scenario 1). Skip and re-run behavior (scenarios 3–4) is built in US2 (T035) and verified by the post-merge `release` step (quickstart §7).

### Tests first ⚠️

- [X] T025 [US4] Write `tests/Hoard.Release.Tests/VerifyTagTests.cs`. Each test builds a temporary bare "origin" and clone with `git`, runs `eng/release/verify-tag.sh` with env-only inputs (`TAG`, `GITHUB_SHA`, `GITHUB_OUTPUT` pointing at a temp file), and asserts the exit code, the stderr message and the output. Cases:
  - **Accepted**: an annotated `v0.0.1-alpha` on the `main` merge commit outputs `version=0.0.1-alpha`. An annotated tag on a non-merge commit reachable from `main` is also accepted.
  - **Wrong pattern**: `v0.0.0-alpha`, `v0.1.0-alpha`, `v0.0.1-beta` and `v0.0.01-alpha` fail with "release tags must match v0.0.N-alpha".
  - **Injection-shaped**: `v0.0.$(id)-alpha` fails the pattern check, and a sentinel proves nothing executed.
  - **Lightweight tag**: fails with "release tags must be annotated".
  - **Not on main**: a tag on a commit only on a side branch fails with "tagged commit <sha> is not on main".
  - **SHA mismatch**: a `GITHUB_SHA` different from the tag's commit fails.

  The class is skipped on Windows (R9, security CHK015)
- [X] T026 [US4] Run `dotnet test Hoard.slnx` and confirm T025 fails because the script does not exist

### Implementation

- [X] T027 [US4] Implement `eng/release/verify-tag.sh` (bash, `set -euo pipefail`, executable bit set in git):
  1. Read `TAG` from env only, and validate it against `^v0\.0\.[1-9][0-9]*-alpha$` before any other use.
  2. `git fetch --force origin "+refs/tags/$TAG:refs/tags/$TAG"`, then require `git cat-file -t "refs/tags/$TAG"` = `tag`.
  3. Require `git rev-parse "refs/tags/$TAG^{commit}"` = `$GITHUB_SHA`.
  4. `git fetch origin +refs/heads/main:refs/remotes/origin/main`, then require `git merge-base --is-ancestor`.
  5. Write `version=${TAG#v}` to `$GITHUB_OUTPUT`.

  It never creates, moves or deletes tags (FR-018) and never echoes tokens (R9)
- [X] T028 [US4] Run `dotnet test Hoard.slnx`. All guard tests pass on macOS/Linux

**Checkpoint**: the guard is proven against every refusal case.

---

## Phase 6: User Story 2 — A merged spec becomes a downloadable alpha (P1)

**Goal**: an annotated `v0.0.N-alpha` tag on `main` publishes the multi-arch image, four
executables plus `SHA256SUMS`, the `Hoard.Core` package and a prerelease with working/planned
notes, with nothing published unless every verify job passed (FR-012–FR-017).

**Independent test**: the `release.yml` dry run on PR #4 builds and verifies everything with
`publish` skipped (quickstart §5). After merge, `v0.0.1-alpha` publishes everything (quickstart §6).

### Tests first ⚠️

- [X] T029 [US2] Write `tests/Hoard.Release.Tests/StatusNotesTests.cs`. It runs `eng/release/status-notes.sh` against temp README files and asserts:
  - the text between `<!-- release-status:start -->` and `<!-- release-status:end -->` is extracted exactly, without the markers
  - a missing start marker, a missing end marker, or an empty section exits non-zero with a clear message (R11, release CHK018)

  The class is skipped on Windows
- [X] T030 [US2] Run `dotnet test Hoard.slnx` and confirm T029 fails

### Implementation

- [X] T031 [US2] Implement `eng/release/status-notes.sh <readme> <out-file>` (bash, `set -euo pipefail`, executable bit). Make T029 pass
- [X] T032 [US2] Add the `<!-- release-status:start -->`/`<!-- release-status:end -->` section to `README.md`, replacing "Status: planning". It contains:
  - **Working**: a health-only host on `127.0.0.1:8484`; container and self-contained executables; loopback only; the container is not reachable from the host yet (until spec 002)
  - **Planned**: everything else, in one line per area
  - every executable and both image architectures are smoke-tested on native runners before release

  Follows FR-016, R11, spec Assumptions and Clarifications
- [X] T033 [US2] Create `.github/workflows/release.yml`, part 1 — triggers and guard:
  - **Triggers**: `push` of tags `v0.0.*-alpha`, and `pull_request` to `main` on paths `.github/workflows/release.yml`, `eng/**`, `deploy/**` (R12).
  - **Permissions**: workflow-level `contents: read`. No `actions/cache`.
  - **Pinning**: all actions pinned by SHA.
  - **`guard` job**: runs only on tag push. Full-depth checkout with `persist-credentials: false`; passes `TAG: ${{ github.ref_name }}` via `env:` to `eng/release/verify-tag.sh`; outputs `version`.
  - **`version` job or step**: yields `0.0.0-pr` on PRs and the guard output on tags.
  - **Verify job condition**: `!cancelled() && (needs.guard.result == 'success' || needs.guard.result == 'skipped')` (R9, R10, R13, security CHK011)
- [X] T034 [US2] Add to `.github/workflows/release.yml`, part 2 — verify jobs:
  - **`verify`**: on `ubuntu-latest` (the release-guard and status-notes tests are skipped on Windows). Restore, build, `dotnet test Hoard.slnx`, then `dotnet pack src/Hoard.Core -c Release -o out/packages` **without** any `Version` override. Uploads the nupkg artifact.
  - **`binaries`**: a matrix pairing each RID with its native standard runner: `linux-x64` → `ubuntu-latest`, `linux-arm64` → `ubuntu-24.04-arm`, `win-x64` → `windows-latest`, `osx-arm64` → `macos-latest`. Every step uses `shell: bash`.
    - Runs `dotnet publish src/Hoard.Host -c Release -r <rid> --self-contained -p:PublishSingleFile=true -p:DebugType=none -p:Version=<version>` and renames the output to `hoard-<version>-<rid>[.exe]`.
    - Architecture check with `file`, failing unless the output is ELF x86-64 / ELF aarch64 / PE32+ x86-64 / Mach-O arm64 respectively.
    - Runs `eng/smoke/smoke-binary.sh` on **every** RID, then uploads the executable as an artifact. `fail-fast: false`, so one platform's failure still reports the others (spec edge case).
  - **`container`**: `needs: binaries`, on `ubuntu-latest`. Downloads the linux executables, runs `docker buildx build --platform linux/amd64,linux/arm64 --output type=oci,dest=hoard.oci.tar`, and uploads `hoard.oci.tar`.
  - **`container-smoke`**: `needs: container`, a matrix `amd64` → `ubuntu-latest`, `arm64` → `ubuntu-24.04-arm`. Downloads `hoard.oci.tar`, installs `skopeo` if the runner image lacks it, loads its variant via `skopeo copy --override-arch <arch> oci-archive:hoard.oci.tar docker-daemon:hoard:<version>`, and runs `eng/smoke/smoke-container.sh`.

  Follows R6, R7, FR-012–FR-014
- [X] T035 [US2] Add to `.github/workflows/release.yml`, part 3 — the `publish` job:
  - **Needs and condition**: `needs: [guard, verify, binaries, container, container-smoke]`; `if: github.event_name == 'push' && startsWith(github.ref, 'refs/tags/') && needs.guard.result == 'success' && needs.verify.result == 'success' && needs.binaries.result == 'success' && needs.container.result == 'success' && needs.container-smoke.result == 'success'`.
  - **Permissions**: `contents: write` and `packages: write` on this job only.
  - **Steps, in order**:
    0. Setup on `ubuntu-latest`: SHA-pinned checkout (`persist-credentials: false`; needed for `eng/release/status-notes.sh` and `README.md`), `actions/setup-dotnet` from `global.json` (for `dotnet nuget push`), and `actions/download-artifact` for the four executables, the `.nupkg` and `hoard.oci.tar` into known directories. Install `skopeo` if the runner image lacks it.
    1. In the executables directory, `sha256sum hoard-* > SHA256SUMS`, so each line carries the bare file name that `sha256sum -c SHA256SUMS --ignore-missing` expects.
    2. `dotnet nuget push out/packages/*.nupkg --source https://nuget.pkg.github.com/stonefly-labs/index.json --skip-duplicate`, with the key passed via `env` and never on the command line.
    3. `skopeo inspect docker://ghcr.io/stonefly-labs/hoard:<version>`. Push with `skopeo copy --all oci-archive:hoard.oci.tar docker://ghcr.io/stonefly-labs/hoard:<version>` only if that tag is absent. Never write `latest`.
    4. Generate notes with `eng/release/status-notes.sh`. If `gh release view` finds no release, run `gh release create "$TAG" --prerelease --verify-tag --generate-notes --notes-file notes.md` with all executables and `SHA256SUMS` in one call. Otherwise upload only the missing assets, never with `--clobber`.
  - **Rules**: all ref and version values pass via `env:`. Credentials are never echoed, and `set -x` is never enabled around them (gap fold-in, security CHK007). The job creates or moves no tags.

  Follows R10, FR-013, FR-015–FR-018
- [X] T036 [US2] Review the finished `.github/workflows/release.yml` and `ci.yml` against the rules below. Fix any breach:
  - every action is pinned by SHA
  - every image is pinned by digest
  - there is no `${{ }}` in any `run:` block that carries ref, tag or PR data
  - `persist-credentials: false` is set on every checkout
  - `publish` is the only job with write permissions
  - `publish` `needs` the guard and every verify job and requires `success` from each, so a failing test, arch check or smoke test can never reach it (SC-005 failing-test case)
  - no `--clobber`, no `latest`, no `git tag`/`git push`

  Follows R13, FR-017, FR-018, security CHK006/CHK011
- [X] T037 [US2] **Precondition: the repository is public** (otherwise the macOS/arm64 dry-run jobs bill private minutes; stop and ask the maintainer if not). Push to PR #4 and confirm the `release.yml` dry run: all four executables are built, architecture-checked and pass smoke on their native runners (including the Windows Git Bash path), the multi-arch OCI archive builds, amd64 and arm64 images pass smoke, and `publish` is skipped. Report the actual `gh pr checks` result (quickstart §5)

**Checkpoint**: the dry run is green on PR #4. The real publish is verified by the `release` step after merge.

---

## Phase 7: Polish and documentation (FR-019, Constitution IX)

- [X] T038 [P] Update `README.md` beyond the status section:
  - **Running the container**: `docker run` with the same hardening flags the smoke test uses (`--read-only --cap-drop ALL --security-opt no-new-privileges`) (gap fold-in, security CHK005), plus a note that it is not reachable from the host until spec 002.
  - **Running an executable**: download from the prerelease, verify with `sha256sum -c SHA256SUMS --ignore-missing`, `chmod +x`, and `xattr -d com.apple.quarantine` on macOS.
  - **Build and test**: `dotnet build Hoard.slnx`, `dotnet test Hoard.slnx`.
  - **Scope**: describe shipped behavior only.
- [X] T039 [P] Rewrite `docs/RELEASING.md` to describe the actual pipeline:
  - **Guard**: its checks and messages, and that it prevents mistakes but is not access control (write access is the trust boundary).
  - **Publish order**: packages → image → release. Nothing publishes until every verify job passes.
  - **Artifact names**, and that `latest` is never moved.
  - **`SHA256SUMS`**: one line per executable, verified with `sha256sum -c SHA256SUMS --ignore-missing`. It proves integrity, **not** origin (gap fold-in, release CHK012–CHK014).
  - **Release notes**: they come from the README status section.
  - **PR dry run**.
  - **Re-runs**: skip what exists and upload only missing assets. After a partial publish (e.g. package pushed, image push failed), re-run the same run. If assets were partly uploaded and `SHA256SUMS` might not match, release a new version, never retag (gap fold-in, release CHK004/CHK006).
  - **First-release check**: after `v0.0.1-alpha` publishes, re-run its release run once to prove the already-published skip (quickstart §7, SC-005).
  - **Package versions**: bump `<Version>` in a `Hoard.*` project file whenever a spec changes that package. An unchanged version is silently skipped by `--skip-duplicate`, so reviewers check the bump (gap fold-in, release CHK017).
  - **One-time manual settings before `v0.0.1-alpha`**: make the repository public. After the first publish, set the `hoard` GHCR package and the `Hoard.Core` package to public, and confirm `docker pull ghcr.io/stonefly-labs/hoard:0.0.1-alpha` works anonymously. Consuming `Hoard.Core` from GitHub Packages still needs a token with `read:packages`, even though the package is public (R14).
  - **CI section**: correct it to what runs today: PRs run build, tests, architecture checks and `linux-x64`/amd64 smoke; releases (and PRs touching the pipeline) smoke every executable and both image architectures on native standard runners, which are free because the repository is public.
- [X] T040 [P] Update `docs/DEVELOPMENT.md`: replace "No application code exists yet" with prerequisites (.NET 10 SDK, Docker, bash, `lsof`), the build, test and smoke commands, and the two PR checks
- [X] T041 [P] Add a "Commands" section to `CLAUDE.md`: `dotnet build Hoard.slnx`, `dotnet test Hoard.slnx`, `eng/smoke/smoke-binary.sh <exe>` and `eng/smoke/smoke-container.sh <image>` (FR-019)
- [X] T042 [P] Update `specs/001-pipeline-skeleton/quickstart.md`:
  - §3: use the exact Dockerfile build-context inputs the implementation settled on
  - §6: add the one-time visibility settings and the anonymous `docker pull` check
  - §7: keep the re-run idempotency check (SC-005) and state that it runs once, in the `release` step for `v0.0.1-alpha`, with the outcome recorded in the progress log
- [X] T043 Prove the PR gate fails once on PR #4 (quickstart §4, SC-005 failing-test case). Push a temporary commit with a deliberately failing test, confirm `build-test` (and the `release.yml` dry run, if triggered) fails and names the test, then revert it in the next commit. Record both CI results
- [X] T044 Final verification: `dotnet build Hoard.slnx` (zero warnings), `dotnet test Hoard.slnx`, and both smoke scripts locally. Then confirm `gh pr checks 4` is all green. Report the actual results

---

## Dependencies and execution order

- **Setup (T001–T008)** blocks everything.
- **US1 (T009–T019)** comes first. It is the MVP gate, and the host `Program.cs` (T016) is the artifact every later story packages.
- **US3 (T020–T024)** depends on US1's host (T016) and `ci.yml` (T018).
- **US4 (T025–T028)** depends only on Setup (the test project). It can run in parallel with US3.
- **US2 (T029–T037)** depends on US3's smoke scripts and Dockerfile (T020–T022) and on US4's guard (T027).
- **Polish (T038–T044)** comes after US2. T038–T042 can run in parallel with each other. T043 and T044 run last.

Within each story: tests are written and seen failing, then implementation, then the story's verification task.

## Parallel examples

- **US1**: T009, T010, T011 and T012 (separate test files), then T019 alongside T018.
- **US3**: T020, T021 and T022 (separate files).
- **US3 ∥ US4**: the T025–T028 chain can proceed while US3's scripts are written.
- **Polish**: T038, T039, T040, T041 and T042 (separate docs).

## Implementation strategy

1. **MVP**: Setup and US1. PR #4 gets a trustworthy `build-test` status on a health-only host.
2. **Add US3**: packaging is proven on every PR (`smoke`).
3. **Add US4**: the release guard is proven by tests.
4. **Add US2**: `release.yml` is complete, and its dry run proves every platform before merge.
5. **Polish**: docs describe what ships, and the failing-gate proof is recorded once.

Commit after each checkpoint, and push so CI runs on PR #4.

## Implementation notes

- **Test runner (T003, T007, all test commands).** xUnit v3 4.x runs on Microsoft.Testing.Platform,
  and the .NET 10 SDK no longer runs it through VSTest. `global.json` opts `dotnet test` into MTP
  (`"test": { "runner": "Microsoft.Testing.Platform" }`). So `Microsoft.NET.Test.Sdk` and
  `xunit.runner.visualstudio` have no consumer and are not referenced, and the solution test
  command is `dotnet test --solution Hoard.slnx` (MTP does not accept a positional solution).
- **T013.** Before implementation: the architecture tests failed to compile (`CoreDependencyRules`
  missing), `Health_returns_healthy_as_plain_text` failed with `404` and the two `404` tests passed.
- **T010.** `dotnet restore` runs from the temp root with a relative project path. macOS links
  `/var` to `/private/var`, and NuGet silently drops a project reference it resolves under a
  different path, which made the first version of the test see no violation.
- **T015.** Core's `ProjectAssetsFile` comes from a `GetProjectAssetsFile` target that
  `tests/Hoard.Architecture.Tests/CoreProjectPaths.targets` adds to Core only for that
  `MSBuild` call (`CustomAfterMicrosoftCommonTargets`); the dll path comes from `GetTargetPath`.
- **T023.** Run locally on macOS arm64 against an `osx-arm64` executable and a `linux-arm64` image:
  clean and all hostile runs passed. A scratch build using `ListenAnyIP(8484)` failed both scripts
  (`non-loopback listener: *`; `non-loopback listener 000…000:2124`) and was reverted. In user-defined
  Docker networks the socket table also shows Docker's embedded DNS on `127.0.0.11`, which is loopback.
- **T037.** `release.yml` dry run on PR #4 (run 41244625332): `verify`, all four `binaries` jobs
  (architecture check and every smoke run, including the Windows Git Bash path, with real loopback
  listeners) and `container`, `container-smoke (amd64)` and `container-smoke (arm64)` passed;
  `guard` and `publish` skipped.
- **T043.** Commit 3faf8ad added a deliberately failing test: `ci.yml` run 41244851498 failed
  `build-test`, naming `DeliberateFailureTests.Gate_must_fail_on_this_test`, and skipped `smoke`.
  Reverted in 86f4e0d.
- **T044.** On 86f4e0d: `dotnet build Hoard.slnx` 0 warnings; `dotnet test --solution Hoard.slnx`
  34/34 passed; both smoke scripts passed locally (macOS arm64); `gh pr checks 4` all green
  (`build-test`, `smoke`, and the `release.yml` dry run with `publish` skipped).

---

## Phase 8: Review round 1

From the `review` step (docs-grounding-auditor, architecture-boundary-reviewer, security-reviewer).

- [X] T045 Pack the solution instead of naming `Hoard.Core` in `.github/workflows/release.yml` (`dotnet pack Hoard.slnx -c Release --no-build -o out/packages` after a Release build), so every packable `Hoard.*` project is published as FR-015 and `docs/RELEASING.md` say (MAJOR, grounding + architecture)
- [X] T046 Restore, build and test in `-c Release` in the release `verify` job so the architecture rules check the same Core configuration that is packed (architecture MINOR)
- [X] T047 In `eng/smoke/smoke-container.sh`, reject a user whose part before `:` is `root` or `0` (e.g. `root:root`) (security MINOR)
- [X] T048 Let `smoke()` in `eng/smoke/smoke-container.sh` pass arguments to the image and add a `hostile-command-line` run with `--urls http://0.0.0.0:8086` (security MINOR)
- [X] T049 Use the runner image's preinstalled `skopeo` in `release.yml` (`container-smoke`, `publish`) and fail if it is missing, instead of an unpinned `apt-get install` in the job holding the write token (security MINOR)
- [X] T050 Add `global.json`, `Directory.*.props` and `src/**/*.csproj` to the `release.yml` pull-request path filter and match the path list in `docs/RELEASING.md` (grounding MINOR)
- [X] T051 `docs/DEVELOPMENT.md`: say the binary smoke also tries a hostile `--urls` and `appsettings.json`; show a native-RID publish for `smoke-binary.sh` and a Linux-RID publish into `out/bin` for the image (grounding MINOR)
- [X] T052 `plan.md`: replace `dotnet test Hoard.slnx` with `dotnet test --solution Hoard.slnx` and add a one-line MTP deviation note pointing to the implementation notes (grounding MINOR)

Deferred (no task; for the maintainer at `accept`): the first spec that adds behavior to `Hoard.Core`
must bump its `<Version>` or `--skip-duplicate` will silently skip it; README's "Try the alpha"
commands work only once `v0.0.1-alpha` is published and made public, checked at the `release` step.

---

## Phase 9: Review round 2

From the `review` step (docs-grounding-auditor, architecture-boundary-reviewer, security-reviewer).
The maintainer chose to fix the three items below and defer or accept the rest.

- [x] T053 In `specs/001-pipeline-skeleton/contracts/release-outputs.md`, list the full `release.yml` pull-request path filter (`.github/workflows/release.yml`, `eng/**`, `deploy/**`, `global.json`, `Directory.*.props`, `src/**/*.csproj`), matching `release.yml` and `docs/RELEASING.md` (grounding MINOR)
- [x] T054 In `specs/001-pipeline-skeleton/research.md`, replace `dotnet test Hoard.slnx` with `dotnet test --solution Hoard.slnx` and drop the VSTest packages from the decision, pointing to the MTP deviation note in `plan.md` (grounding MINOR)
- [x] T055 In `smoke()` in `eng/smoke/smoke-container.sh`, read the running app's uid from `/proc/1/status` through the helper container (`--pid container:$cid`) and fail unless it is non-empty and not `0`, so numeric forms like `00`/`+0` and uid-0 named users can't pass the static user check; prove it fails with a scratch `--user 0` run (security MINOR)

Deferred (no task; for the maintainer at `accept`): a repo `nuget.config` with `<clear/>` and package
source mapping, plus lock files and locked-mode restores, goes with the next spec that adds a real
dependency. Accepted as known limitations: `dotnet pack Hoard.slnx` prints a "packaging has been
disabled" warning for `Hoard.Host` (harmless, exit 0), and `Hoard.Core` has no package readme until it has content.

---

## Phase 10: Review round 3

From the `review` step (docs-grounding-auditor). The maintainer chose to fix it.

- [x] T056 In `specs/001-pipeline-skeleton/research.md` R12, list the full `release.yml` pull-request path filter (`.github/workflows/release.yml`, `eng/**`, `deploy/**`, `global.json`, `Directory.*.props`, `src/**/*.csproj`) (grounding MINOR)
