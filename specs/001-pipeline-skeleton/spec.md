# Feature Specification: Pipeline and solution skeleton

**Feature Branch**: `feature/001-pipeline-skeleton`

**Created**: 2026-10-31

**Status**: Draft

**Input**: User description: "Set up Hoard's delivery pipeline and minimal solution skeleton so that from the first merge every pull request is gated by GitHub Actions (build, tests, architecture dependency checks) and every merged spec can be released as an alpha per docs/RELEASING.md. A release tag on a main commit must publish a multi-arch (amd64, arm64) container image to GHCR, self-contained executables for linux-x64, linux-arm64, osx-arm64 and win-x64 with SHA-256 checksums, any packable NuGet packages to GitHub Packages, and a GitHub prerelease with notes. The only runtime behavior is a minimal Hoard host answering a health endpoint; CI must smoke-test both the container and a native binary. Create only the host, core and test projects. Architecture tests must encode the dependency rules by project naming pattern (core may never reference persistence, storage, protocol or identity providers, or EF Core) and prove they fail on a violation. Cover failure cases: a tag not on main, a failing test, an already-published package version. Out of scope for this spec: configuration, storage, protocols, console, and any project not needed here."

## Clarifications

### Session 2026-10-31

- Q: Should `Hoard.Core` be published as a package from this first release, or should no project be packable until a provider package needs it? → A: Publish `Hoard.Core` now, as an essentially empty package, so the package path is exercised from the first release.
- Q: Who can download alpha artifacts? → A: Everyone. The maintainer makes the repository, the `ghcr.io/stonefly-labs/hoard` image and the `Hoard.Core` package public before the first release tag; the pipeline does not change visibility.
- Q: What is `Hoard.Core`'s initial package version? → A: `0.0.1-alpha`; afterwards it changes independently of the release version, only when a spec changes `Hoard.Core`.
- Q: Is shipping executables and an image that CI built but never ran (`win-x64`, `linux-arm64`, arm64 image) acceptable? → A: Yes for alphas, stated as a known limitation in the release notes; revisit before the MVP release.

## User Scenarios & Testing *(mandatory)*

Actors: the **maintainer** (reviews and merges PRs, pushes release tags), a **contributor**
(opens PRs, human or agent), and an **early user** (downloads an alpha to try it).

### User Story 1 - Every pull request is gated (Priority: P1)

A contributor opens a pull request against `main`. Without any manual action, checks run that
build the solution, run all tests, and run the architecture dependency checks. The maintainer
sees a single pass/fail signal on the PR before deciding to merge.

**Why this priority**: The constitution requires CI to gate every PR from the first spec. Every
later spec depends on this signal being trustworthy.

**Independent Test**: Open a PR with a trivial change and observe all checks run and pass; open a
PR that breaks a test or a dependency rule and observe the checks fail.

**Acceptance Scenarios**:

1. **Given** a PR whose code builds and whose tests pass, **When** the PR is opened or updated,
   **Then** the build, test and architecture checks run automatically and all report success.
2. **Given** a PR containing a failing test, **When** checks run, **Then** the test check fails
   and the PR shows a failing status naming the failed test.
3. **Given** a PR in which the core project references a persistence, storage, protocol or
   identity provider project, or EF Core, **When** checks run, **Then** the architecture check
   fails and its output names the forbidden reference.
4. **Given** a PR that does not compile, **When** checks run, **Then** the build check fails.

---

### User Story 2 - A merged spec becomes a downloadable alpha (Priority: P1)

After the maintainer merges a spec's PR and pushes the annotated tag `v0.0.N-alpha` on the merge
commit (per `docs/RELEASING.md`), the release process publishes, without further manual steps:
a container image for `linux/amd64` and `linux/arm64`; self-contained executables for
`linux-x64`, `linux-arm64`, `osx-arm64` and `win-x64` with SHA-256 checksums; any packable
packages whose version is not yet published; and a GitHub prerelease carrying notes, the
executables and the checksums.

**Why this priority**: The constitution requires every merged spec to produce a published alpha
so progress is visible. Spec 001 is itself released as `v0.0.1-alpha` once merged.

**Independent Test**: Push a release tag on a merge commit on `main` and verify the prerelease,
its attachments, the container image for both architectures, and the published packages.

**Acceptance Scenarios**:

1. **Given** an annotated tag `v0.0.N-alpha` on a commit reachable from `main`, **When** the tag
   is pushed, **Then** the tagged commit is built and tested before anything is published.
2. **Given** tests pass for the tagged commit, **When** the release runs, **Then** a container
   image tagged with the version is available at `ghcr.io/stonefly-labs/hoard` for both
   `linux/amd64` and `linux/arm64`, and the `latest` tag is not moved.
3. **Given** the release runs, **When** it completes, **Then** a GitHub release marked as a
   prerelease exists for the tag with notes, the four platform executables, and a SHA-256
   checksum for each executable.
4. **Given** the release notes, **When** a reader opens them, **Then** they distinguish what
   works in this build from what is planned.
5. **Given** a downloaded executable and its published checksum, **When** the user computes the
   file's SHA-256, **Then** it matches.

---

### User Story 3 - The released build actually starts (Priority: P2)

Before a build is published, CI starts both the container image and a native executable and
confirms each answers the health endpoint. An early user who runs the released container or
executable gets a running Hoard host that reports itself healthy.

**Why this priority**: A published artifact that does not start is worse than none; smoke tests
catch packaging failures (missing runtime pieces, wrong architecture, bad entry point) that unit
tests cannot.

**Independent Test**: Run the container image and one native executable, request the health
endpoint, and receive a healthy response.

**Acceptance Scenarios**:

1. **Given** the built container image, **When** CI starts it and requests the health endpoint,
   **Then** it receives a successful healthy response within a bounded wait.
2. **Given** a built native executable for the CI runner's platform, **When** CI starts it and
   requests the health endpoint, **Then** it receives a successful healthy response.
3. **Given** a smoke test fails, **When** the pipeline evaluates results, **Then** nothing from
   that run is published.
4. **Given** the running host, **When** any path other than the health endpoint is requested,
   **Then** it is not found (the host exposes no other behavior).

---

### User Story 4 - Releases refuse unsafe inputs (Priority: P2)

The release process protects the published record: it refuses tags not on `main`, publishes
nothing when tests fail, and never overwrites an already-published package version.

**Why this priority**: `docs/RELEASING.md` requires tags to point at commits reachable from `main`
and never be moved; package versions are immutable. These guards keep alphas trustworthy.

**Independent Test**: Push a release-pattern tag on a commit not reachable from `main`, and
separately re-run a release where a package version already exists; observe the refusal and the
skip respectively.

**Acceptance Scenarios**:

1. **Given** a release-pattern tag on a commit not reachable from `main`, **When** it is pushed,
   **Then** the release fails before publishing anything and states that the tag is not on
   `main`.
2. **Given** a tag on `main` whose commit has a failing test, **When** the release runs,
   **Then** it fails and no image, executable, package or GitHub release is published.
3. **Given** a packable package whose version already exists in GitHub Packages, **When** the
   release runs, **Then** that package is skipped (not overwritten, not an error), and packages
   with new versions are still published.
4. **Given** a release run that failed partway, **When** the maintainer re-runs it for the same
   tag, **Then** it completes without duplicating or overwriting already-published artifacts.

---

### Edge Cases

- A tag that does not match `v0.0.N-alpha` does not trigger a release (the `-beta`/`-rc` scheme
  arrives with the v0.1.0 MVP release, `docs/RELEASING.md`).
- A lightweight (non-annotated) tag matching the pattern is rejected before anything is
  published, with a message saying release tags must be annotated.
- A tag pushed on a non-merge commit that is still reachable from `main` is accepted;
  reachability from `main` is the rule (`docs/RELEASING.md`).
- One architecture's container build or one platform's executable build fails: the release fails
  and publishes no GitHub release; partial outcomes are reported, not hidden.
- A GitHub release for the tag already exists (e.g. re-run): it is reused or updated, never
  duplicated.
- The architecture check itself must be shown to detect a violation; a check that cannot fail
  provides no gate.
- The health endpoint is requested before the host has finished starting: the smoke test retries
  within a bounded wait rather than failing immediately or waiting forever.

## Requirements *(mandatory)*

### Functional Requirements

**Solution skeleton**

- **FR-001**: The solution MUST contain only the Hoard host project, the Hoard core project, and
  the test projects needed for this spec. No other projects (abstractions, hosting, application,
  providers, console) are created.
- **FR-002**: The host MUST answer `GET /health` with a successful response indicating it is
  healthy, and MUST expose no other endpoints.
- **FR-003**: The host, as an executable and inside the container image, MUST listen on loopback
  only (Constitution III), overriding any all-interfaces default inherited from a base image.
  A test MUST verify the container is not listening on non-loopback interfaces. Configurable
  binding, including the container's explicit all-interfaces opt-in with its startup warning, is
  delivered by spec 002.
- **FR-004**: The core project MUST contain no behavior beyond what the architecture tests need
  to exercise it; no contract or abstraction is added without a consumer (Constitution V).

**Architecture dependency rules**

- **FR-005**: Architecture tests MUST encode by project/assembly naming pattern that the core
  project never references `Hoard.Persistence.*`, `Hoard.Storage.*`, `Hoard.Protocols.*` or
  `Hoard.Identity.*`, nor EF Core.
- **FR-006**: The rules MUST apply to projects that do not exist yet: a future project matching a
  forbidden pattern is caught without editing the tests.
- **FR-007**: The test suite MUST prove each rule fails on a deliberate violation, and the failure
  message MUST name the offending reference.

**Pull request gate**

- **FR-008**: Every PR targeting `main` MUST automatically run build, all tests, and the
  architecture checks, and report a pass/fail status on the PR.
- **FR-009**: *Spec decision (beyond the docs' minimum of build, tests and architecture checks):*
  the PR gate MUST also run the container and native-executable smoke tests (US3) on the PR
  runner's own architecture, so packaging regressions are caught before merge rather than at
  release time.

**Release**

- **FR-010**: Pushing a tag matching `v0.0.N-alpha` MUST start the release process; a tag that is
  not annotated MUST be rejected before anything is published (`docs/RELEASING.md`: tags are
  annotated).
- **FR-011**: The release MUST refuse to publish when the tagged commit is not reachable from
  `main`.
- **FR-012**: The release MUST build and test the tagged commit and MUST publish nothing if build,
  tests, architecture checks or smoke tests fail.
- **FR-013**: The release MUST publish a container image to `ghcr.io/stonefly-labs/hoard` for
  `linux/amd64` and `linux/arm64`, tagged with the version; prereleases MUST NOT move `latest`.
- **FR-014**: The release MUST produce self-contained executables (no separately installed
  runtime) for `linux-x64`, `linux-arm64`, `osx-arm64` and `win-x64`, each with a SHA-256
  checksum.
- **FR-015**: The release MUST pack every packable `Hoard.*` package and publish to GitHub
  Packages only versions not already published; an existing version is skipped, never
  overwritten. In this spec `Hoard.Core` is the one packable project: it is published as a
  package even though it carries no behavior yet, so the package path, including the
  already-published skip, is proven end to end from the first release. `Hoard.Host` and the
  test projects are not packable.
- **FR-016**: The release MUST create a GitHub release marked as a prerelease with generated notes
  that distinguish working capabilities from planned ones, with the executables and checksums
  attached.
- **FR-017**: A re-run of a release for the same tag MUST NOT duplicate or overwrite already
  published artifacts (follows from `docs/RELEASING.md`: tags and published versions are never
  moved or recreated; a bad release is corrected with a new version).
- **FR-018**: The release process MUST never create, move or delete tags.

**Documentation**

- **FR-019**: The README and `docs/RELEASING.md` MUST be updated to describe what ships: the
  health-only host, how to run the container and executable, the PR checks, and the release
  outputs. `CLAUDE.md` MUST gain the build and test commands section that the `drive-speckit`
  implement step expects spec 001 to add.

### Key Entities

- **Release tag**: annotated `v0.0.N-alpha` tag on a commit reachable from `main`; immutable once
  pushed.
- **Release artifacts**: the container image (two architectures), four platform executables,
  their SHA-256 checksums, the `Hoard.Core` package, and the GitHub prerelease that groups them.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of PRs to `main` receive an automatic pass/fail status covering build, tests
  and architecture checks, with no manual trigger.
- **SC-002**: A deliberate violation of each core dependency rule (four provider patterns plus EF
  Core) causes the checks to fail, naming the reference, in 5 of 5 cases.
- **SC-003**: After the spec 001 PR merges and is tagged, `v0.0.1-alpha` is published with an
  image for both architectures, all four executables, matching checksums, and the `Hoard.Core`
  package in GitHub Packages, with no manual publishing steps.
- **SC-004**: The container and a native executable each report healthy within 30 seconds of
  start in CI (a spec-chosen bound that makes "bounded wait" measurable).
- **SC-005**: A tag not on `main`, a failing test, and an already-published package version each
  result in no unintended publication, verified once each.

## Assumptions

- The maintainer pushes release tags manually per `docs/RELEASING.md` (the `drive-speckit`
  `release` step); the release process does not create tags.
- Pushing to GHCR and GitHub Packages uses the repository's built-in CI credentials; no personal
  secrets are introduced.
- The native-executable smoke test runs on at least one platform CI natively supports
  (linux-x64; the release also smoke-runs osx-arm64); the other platforms are built and
  checksummed but not smoke-tested in this spec.
  Release notes state this as a known limitation; adding native smoke runs is revisited before
  the MVP release.
- Alpha artifacts are public: the maintainer makes the repository, image and package public
  before the first release tag (a manual, one-time setting outside the pipeline).
- The container smoke test checks health from inside the container, since the host listens on
  loopback only until spec 002 adds the container's explicit binding. Release notes for this
  alpha state that the container is not reachable from the host yet.
- The health endpoint reports liveness only; the liveness/readiness distinction belongs to spec
  002.
- Generated release notes come from merged PR titles plus a fixed "working vs planned" section;
  rich changelog curation is not required.
- Out of scope, owned by later specs: configuration and deployment profiles (002), metadata store
  (003), artifact storage (004), upstream access (005), protocols (007), console (014), and any
  project not needed here.
