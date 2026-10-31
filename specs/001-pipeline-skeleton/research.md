# Research: Pipeline and solution skeleton

All Technical Context unknowns are resolved below. Each entry: decision, rationale,
alternatives considered.

## R1. Solution and build conventions

- **Decision:** `Hoard.slnx` at the repo root (the .NET 10 default solution format);
  `global.json` pins SDK `10.0.100` with `rollForward: latestFeature`;
  `Directory.Build.props` sets `net10.0`, nullable, implicit usings,
  `TreatWarningsAsErrors`, `IsPackable=false` by default, and shared package metadata
  (MIT license expression, repository URL); `Directory.Packages.props` enables central
  package management.
  `Directory.Packages.props` pins **third-party dependency** versions only. Each
  `Hoard.*` package's own `<Version>` lives in its project file (`docs/RELEASING.md`), and
  `Directory.Build.props` sets no `Version`.
- **Rationale:** one place for third-party versions and warnings. Packability is opt-in,
  so only `Hoard.Core` packs (FR-015).
- **Alternatives:** classic `.sln` (more verbose, no benefit); third-party versions per
  project (they drift apart).

## R2. Architecture dependency rules — what to inspect

- **Decision:** Evaluate `src/Hoard.Core/obj/project.assets.json` (written by restore).
  Its `libraries` section lists every **project and package, direct and transitive**,
  that Core resolves. A rule fails when any library name matches
  `Hoard.Persistence.*`, `Hoard.Storage.*`, `Hoard.Protocols.*`, `Hoard.Identity.*` or
  `Microsoft.EntityFrameworkCore*` (case-insensitive prefix). The failure message lists
  each offending library and the rule it broke.
- **Hardening:**
  - For `type: project` libraries, the rule also matches the referenced project's file
    name (`path`/`msbuildProject`), so a renamed `PackageId`/`AssemblyName` cannot hide
    `Hoard.Persistence.Sqlite.csproj`.
  - A sixth rule follows ARCHITECTURE §3: Core may reference no `Hoard.*` project or
    package other than `Hoard.Abstractions`. This catches `Hoard.Hosting`,
    `Hoard.Application` and `Hoard.Host`.
  - A second layer asserts that `Hoard.Core.csproj` has no raw `<Reference>` items, and
    that `Hoard.Core.dll`'s referenced assemblies match none of the rules. HintPath
    references never appear in the assets file.
- **Locating the assets file:** `Hoard.Architecture.Tests` has
  `<ProjectReference Include="../../src/Hoard.Core/Hoard.Core.csproj"
  ReferenceOutputAssembly="false" />`, so Core is always restored and built first. The
  test reads the path from an assembly attribute filled from Core's MSBuild
  `ProjectAssetsFile`, not a hard-coded path. If the file is missing, or its
  `project.restore.projectName` is not `Hoard.Core`, the test **fails**; it never passes
  vacuously.
- **Residual blind spots (accepted):** reflection-based or runtime assembly loading, and
  a source file copied into Core from a provider. Code review covers these.
- **Rationale:** compiled-assembly inspection (`GetReferencedAssemblies`, NetArchTest,
  ArchUnitNET) misses a reference whose types are not used, because the compiler drops
  it. The assets file captures the declared graph, including references that come in
  transitively, and needs no project list, so it matches by pattern and covers
  projects that do not exist yet (FR-006). It is also present after every restore, so
  tests need no extra tooling.
- **Alternatives:** NetArchTest/ArchUnitNET type-level rules. Useful later for
  slice-level rules, but blind to unused references now; no consumer yet (YAGNI).
  Parsing `.csproj` XML misses transitive references.

## R3. Proving the rules fail (FR-007, SC-002)

- **Decision:** Two layers in `Hoard.Architecture.Tests`:
  1. One fixture test per forbidden pattern (five), each feeding a minimal assets
     document that contains the violation, plus one transitive case. Each test asserts
     the rule fails and the message names the library.
  2. One end-to-end test that writes a throwaway `Hoard.Core`-shaped project and a
     `Hoard.Persistence.Fake` project to a temp directory, runs `dotnet restore`
     (offline, project reference only) and asserts the rule fails on the generated
     assets file. This proves the real restore output is parsed correctly. The temp
     directory is outside the repo, so the repo's `Directory.*.props` and any
     `nuget.config` are not imported. The generated file goes through the same
     `CoreDependencyRules.Evaluate` path as the real test.
- **Rationale:** a check that is never seen failing is not a gate (spec edge case).
  The fixtures cover all five patterns cheaply; the end-to-end test covers the
  real-world path without network access.
- **Alternatives:** a CI job that mutates the repo to add a bad reference. Slower and
  harder to read, and it proves the same thing.

## R4. Test framework

- **Decision:** xUnit v3 (`xunit.v3`, `xunit.runner.visualstudio`,
  `Microsoft.NET.Test.Sdk`), run with `dotnet test Hoard.slnx`. Host tests use
  `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`). Plain `Assert`;
  no assertion library.
- **Rationale:** mainstream, works with the .NET 10 SDK; one command runs everything.
  FluentAssertions is excluded because of its license change. Licenses: xUnit is
  Apache-2.0; the Microsoft test packages are MIT. `Microsoft.Testing.Extensions.CodeCoverage`
  (proprietary license) must not be added; coverage, when a spec needs it, uses coverlet
  (MIT). `Mvc.Testing` stays on the 10.0.x line. `WebApplicationFactory<Program>` needs a
  public `Program`. The .NET 10 SDK generates it for top-level statements; if not,
  `Program.cs` declares `public partial class Program;`.
- **Alternatives:** MSTest or TUnit. Both viable; no reason to deviate from the most
  common choice.

## R5. Host, health endpoint and loopback binding

- **Decision:** `Hoard.Host` uses `WebApplication.CreateBuilder`, `AddHealthChecks()` and
  `MapHealthChecks("/health")` (200 `text/plain` `Healthy`). Port `8484` comes from the
  illustrative config in PRODUCT_SPEC §10. There are no other endpoints. The listener is
  fixed in code so that no configuration source can widen it:
  - `ConfigureKestrel` calls `options.Configure(new ConfigurationBuilder().Build())`
    first. This replaces the config-backed endpoint loader, so `Kestrel:Endpoints`
    entries from env vars, command-line arguments or an `appsettings.json` next to the
    binary add no listeners.
  - It then calls `options.ListenLocalhost(8484)`. Explicit listen options override
    `ASPNETCORE_URLS`, `--urls` and `ASPNETCORE_HTTP_PORTS` (the last is set to `8080`
    in the .NET container base images).
  - `builder.WebHost.PreferHostingUrls(false)` stops `ASPNETCORE_PREFERHOSTINGURLS=true`
    from letting hosting URLs win.
- **Rationale:** meets Constitution III and FR-003 without introducing configuration,
  which belongs to spec 002. `ListenLocalhost` binds `127.0.0.1` and `::1` and tolerates a
  missing IPv6 loopback, which is common in containers. Any widening mechanism the
  framework honors must be shown not to work, which the hostile-env smoke run does (R8).
- **Alternatives:** `UseUrls("http://127.0.0.1:8484")` (config sources can still win);
  clearing the env var in the Dockerfile only (binary would still honor user-set env
  vars).
- **Note:** `WebApplicationFactory` uses TestServer, so binding is proven by the smoke
  scripts (R8), not by unit tests.

## R6. Self-contained executables

- **Decision:**
  - Publish with `dotnet publish src/Hoard.Host -c Release -r <rid> --self-contained
    -p:PublishSingleFile=true -p:DebugType=none -p:Version=<guard version>`. No trimming
    or Native AOT.
  - `IncludeNativeLibrariesForSelfExtract` is **not** set. The runtime's native
    libraries are statically linked into the single-file host, ASP.NET Core adds none,
    so nothing extracts at startup and the container can run with a read-only root
    filesystem.
  - The `Version` override applies **only** to Host publish. `dotnet pack` runs with no
    `Version` override, so `Hoard.Core` keeps its own `<Version>` and `--skip-duplicate`
    skips it when unchanged (FR-015).
  - The output executable is renamed `hoard-<version>-<rid>[.exe]`.
  - Each RID is published, architecture-checked and smoke-tested on its own native
    standard runner: `linux-x64` on `ubuntu-latest`, `linux-arm64` on `ubuntu-24.04-arm`,
    `win-x64` on `windows-latest`, `osx-arm64` on `macos-latest` (so the apphost carries
    the ad-hoc signature Apple Silicon requires). Standard runners are free and unlimited
    on public repositories (R14).
- **Rationale:** a single file is the easiest thing to download and checksum. Trimming
  and AOT have known ASP.NET Core caveats and no benefit yet. Building on macOS avoids
  relying on cross-host signing behavior.
- **Alternatives:**
  - Archives (`.tar.gz`/`.zip`): they keep the exec bit, but users would then check
    archive checksums rather than executable checksums (US2 scenario 5). Raw executables
    are kept, and the docs give `chmod +x` and the macOS quarantine removal.
  - Cross-publishing everything on `ubuntu-latest`: cheaper on a private repo, but
    three of four executables would ship never having run. Superseded once the repo
    went public (R14).
- **Architecture check:** every published file is inspected with `file`. The job fails
  unless it reports ELF x86-64, ELF aarch64, PE32+ x86-64 or Mach-O arm64 respectively.
  It catches a wrong-architecture build before the smoke test's less specific failure.

## R7. Container image

- **Decision:** `deploy/docker/Dockerfile` is based on
  `mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled`, pinned by digest.
  - It copies the **already-published** `linux-x64` or `linux-arm64` executable,
    selected by `TARGETARCH`, with `COPY --chmod=0555`, so files stay root-owned and the
    app user cannot write them.
  - It sets `USER $APP_UID` explicitly and has no `RUN` steps.
  - It adds the OCI label
    `org.opencontainers.image.source=https://github.com/Stonefly-Labs/Hoard-Cld`, which
    links the GHCR package to the repo so `GITHUB_TOKEN` can push.
  - The verify stage builds it **once** with `docker buildx --platform
    linux/amd64,linux/arm64 --output type=oci,dest=hoard.oci.tar`. No QEMU is needed
    because nothing executes during the build.
  - The archive is a workflow artifact. Smoke tests load each architecture's variant
    from it on a native runner (`skopeo copy --override-arch <arch> oci-archive:…
    docker-daemon:…`), and `publish`
    pushes that same archive (`skopeo copy --all oci-archive:… docker://…`).
- **Rationale:** the released image is byte-for-byte the one that was smoke-tested,
  containing the architecture-checked binaries. The chiseled image is small, non-root
  and has no shell, so there is less to attack.
- **Alternatives:**
  - An SDK multi-stage build inside Docker: compiles twice and needs QEMU for arm64.
  - Rebuilding the image in `publish`: the published bytes could differ from the
    tested ones.
  - Per-arch tags merged with `imagetools`: leaves extra public tags.

## R8. Smoke tests

- **Decision:** two portable bash scripts, used by both workflows. Each runs **twice**:
  once with a clean environment, and once with a hostile environment
  (`ASPNETCORE_URLS=http://0.0.0.0:8080`, `ASPNETCORE_PREFERHOSTINGURLS=true`,
  `Kestrel__Endpoints__H__Url=http://0.0.0.0:8485`) that proves R5's guarantees.
  - `eng/smoke/smoke-binary.sh <exe>`:
    - Starts the executable and polls `http://127.0.0.1:8484/health` for up to 30 s
      (SC-004). It asserts `200 Healthy` and that `/anything-else` is `404`.
    - Lists the process's listeners with `lsof -nP -a -p $PID -iTCP -sTCP:LISTEN`. It
      fails if there are none, or if any address is not `127.0.0.1` or `[::1]`.
    - Runs on `linux-x64` (PR and release) and on `linux-arm64`, `osx-arm64` and
      `win-x64` (release), each on its native runner. On Windows the script runs under
      Git Bash; it maps `$!` to the Windows PID via `/proc/<pid>/winpid` and lists
      listeners with PowerShell `Get-NetTCPConnection -State Listen -OwningProcess`.
  - `eng/smoke/smoke-container.sh <image>`:
    - Asserts the image's configured user is not root. Runs the container with
      `--read-only --cap-drop ALL --security-opt no-new-privileges`, keeping the base
      image env (so `ASPNETCORE_HTTP_PORTS=8080` is present).
    - From a digest-pinned helper container sharing the container's network namespace
      (`--network container:<id>`):
      - asserts `/health` = `200 Healthy` and the `404`;
      - reads `/proc/net/tcp` and `/proc/net/tcp6`, and fails if there is no listening
        socket or any listening socket is on a non-loopback address. This covers
        **every** port, not just the expected ones.
    - As a second, outside-in check, a probe container on the same bridge network must
      get *connection refused* (not a timeout) on `<container-ip>:8484`.
    - Positive control: the same probe must reach a helper listener started on that
      bridge. This shows the probe path works, so a refusal is meaningful.
    - Runs on amd64 (`ubuntu-latest`, PR and release) and arm64 (`ubuntu-24.04-arm`,
      release), each loading its own variant from the same OCI archive. Helper images
      are pinned by multi-arch index digest.
- **Rationale:** the chiseled image has no shell or tools, so inspection happens from a
  helper that shares its network namespace. The socket table is the authoritative list
  of listeners. The bridge probe and its control prove the outside-in view.
- **Alternatives:** adding a health-probe CLI mode to the host. That is new behavior
  without a product consumer.

## R9. Release guards and their tests

- **Decision:** `eng/release/verify-tag.sh` reads the tag from an **environment
  variable**, never from text interpolated into `run:`. It fails, with a specific
  message, unless all of the following hold:
  1. The tag matches `^v0\.0\.[1-9][0-9]*-alpha$`. This is checked first, before the
     value is used anywhere else.
  2. After an explicit `git fetch --force origin "+refs/tags/$TAG:refs/tags/$TAG"`
     (`actions/checkout` can make an annotated tag look lightweight),
     `git cat-file -t "refs/tags/$TAG"` is `tag`, i.e. the tag is annotated.
  3. `git rev-parse "refs/tags/$TAG^{commit}"` equals `$GITHUB_SHA`, so the commit
     being built is the commit being checked.
  4. After `git fetch origin +refs/heads/main:refs/remotes/origin/main` on a full-depth
     checkout, the commit is an ancestor of `origin/main`.

  On success it outputs the version (`0.0.N-alpha`), and every later job uses that
  output instead of the raw ref. `tests/Hoard.Release.Tests` runs the script against
  temporary git repositories: accepted tag, wrong pattern, an injection-shaped name
  (`v0.0.$(id)-alpha`), lightweight tag, tag on a commit not on `main`, tag on a
  non-merge commit that is on `main`, and SHA mismatch. The class is skipped on Windows.
- **Rationale:** these guards protect the published record (US4), so they must not fail
  silently. Plain scripts are readable in the workflow and testable with the same
  `dotnet test` command.
- **Honest scope:** the guard prevents **mistakes**; it is not an access control. A tag
  push runs `release.yml` *as it exists at the tagged commit*, so someone with write
  access could tag a branch whose workflow skips the guard. Tag rulesets and protected
  environments are unavailable on this plan. Write access is the trust boundary, and
  RELEASING.md says so.
- **Alternatives:** inline YAML logic (untestable); bats (a second test runner).

## R10. Publishing order and idempotency (FR-012, FR-017)

- **Decision:** the release workflow has verify jobs (guard → build/test/pack →
  binaries per RID with arch check and smoke → container build and smoke) and one
  `publish` job that `needs` all of them. Its condition is explicit:
  `github.event_name == 'push' && startsWith(github.ref, 'refs/tags/') &&
  needs.guard.result == 'success'`, plus `success` for every verify job. So it cannot
  run on a PR, and it cannot run after a skipped or failed guard. (Verify jobs use
  `!cancelled() && (needs.guard.result == 'success' || needs.guard.result == 'skipped')`
  so they also run on PRs, where the guard is skipped.) In order, `publish`:
  1. `dotnet nuget push *.nupkg --source https://nuget.pkg.github.com/stonefly-labs/index.json --skip-duplicate`,
     with the API key passed through `env`, not argv. GitHub Packages answers `409` for
     an existing version, which `--skip-duplicate` treats as a skip.
  2. Pushes the verified OCI archive to `ghcr.io/stonefly-labs/hoard:<version>`, but
     **only if** `skopeo inspect` shows the tag does not exist yet. It never touches
     `latest`.
  3. Creates the GitHub prerelease with `gh release create --prerelease --verify-tag
     --generate-notes --notes-file <status notes>`, attaching the executables and
     `SHA256SUMS`. If the release already exists (a re-run), it uploads only the assets
     that are missing and never uses `--clobber`.
- **Order note:** RELEASING.md currently lists image → executables → packages → release.
  This order differs, but it is safe because nothing publishes until every verify job
  passes. RELEASING.md will be updated to describe the actual order.
- **Rationale:** artifacts already published are never overwritten, and a re-run fills in
  only what is missing. The GitHub release comes last because it is the user-visible
  "published" signal.
- **Known limitation:** a re-run rebuilds the executables. If an earlier run failed partway
  through uploading assets, the originals stay attached, and a newly uploaded
  `SHA256SUMS` could fail to match them, because single-file publish is not bit-for-bit
  reproducible.
  - Mitigation: upload all assets in a single `gh release create` call.
  - Remedy: release a new version (`docs/RELEASING.md`), not a retag.
- **Failing-test case (SC-005):** "a failing test publishes nothing" is guaranteed by the
  workflow's structure: `publish` needs `verify`, and `verify` runs `dotnet test`. It is
  shown in practice by a deliberately failing commit on the 001 PR (quickstart §4), which
  fails both `ci.yml` and the `release.yml` dry run. It is not shown by a failing tagged
  release, which would burn a version.

## R11. Release notes: working vs planned (FR-016, Constitution IX)

- **Decision:** the README gains a status section between `<!-- release-status:start -->`
  and `<!-- release-status:end -->` markers. The section says what works and what is
  planned. Every spec PR already has to update the README (Constitution IX), so it stays
  current. The release job extracts it into the notes file, and `--generate-notes`
  appends the list of merged PRs. The job fails if the markers are missing.
- **Checksums:** RELEASING.md states that the single `SHA256SUMS` file provides each
  executable's checksum (FR-014). It also states that the file proves the download is
  intact, **not** who built it, because it is hosted alongside the executables.
- **Rationale:** a single source of truth that is already kept current. No per-release
  file to forget.
- **Alternatives:** a per-version notes file (one more artifact per spec); generated
  notes only (they cannot state what is planned).

## R12. Exercising the release workflow before the first tag (plan decision)

- **Decision:** *(plan decision, beyond the spec's FRs)* `release.yml` also runs on
  `pull_request` when it, `eng/**` or `deploy/**` change. On a PR, the guard is skipped,
  the version is `0.0.0-pr`, every verify job runs, and `publish` is excluded by its
  condition (R10).
- **Rationale:** otherwise the four-platform build, macOS signing and multi-arch image
  build would run for the first time on the real `v0.0.1-alpha` tag. This proves them on
  the 001 PR itself, and on any later PR that changes the release pipeline.
- **Alternatives:** `workflow_dispatch`. It only works once the file is on `main`.

## R13. CI workflow, permissions and supply chain

- **Decision:**
  - `ci.yml` runs on `pull_request` to `main` and on `push` to `main`. It has one
    `build-test` job (restore, build, `dotnet test`) and one `smoke` job (publish
    `linux-x64`, binary smoke, build an amd64 image from it, container smoke; FR-009).
  - Both workflows declare `permissions: contents: read` at workflow level.
    `release.yml` grants `contents: write` and `packages: write` to the `publish` job
    only.
  - All actions, including `actions/*`, are pinned by commit SHA, with the version in a
    comment. Helper images (`curl`/busybox) and the base image are pinned by digest.
  - `.github/dependabot.yml` (ecosystems `github-actions`, `docker`, `nuget`) keeps the
    pins current.
  - Checkout uses `persist-credentials: false` in every job that runs repository code.
  - There is no `actions/cache` in `release.yml`, which avoids cache poisoning.
  - No `${{ }}` expression carrying a ref, tag or PR value appears inside `run:`; such
    values pass through `env:`.
- **Rationale:** least privilege and resistance to injection. `GITHUB_TOKEN` is the only
  credential, and no personal secrets are introduced (spec assumption).
- **Limitation:** until the repository is public, GitHub branch protection and rulesets
  are unavailable on the free plan (enabling them afterwards is a repository setting
  outside this spec). The gate is the PR status check plus the rule in
  `docs/RELEASING.md` that "a PR merges only when CI is green". SC-001 (a status
  reported on every PR) is unaffected.

## R14. Package and image visibility (decided by the maintainer, 2026-10-31)

- **Decision:** alphas are public. Before the first release tag the maintainer makes
  the repository public and, after the first push creates them, sets the
  `ghcr.io/stonefly-labs/hoard` image and the `Hoard.Core` package to public. The
  workflow never changes visibility. `docs/RELEASING.md` lists these as one-time manual
  settings, and the `release` step checks the image can be pulled anonymously.
- **Caveat:** GitHub Packages' NuGet registry requires authentication even for public
  packages, so `Hoard.Core` consumers need a token with `read:packages`. The docs say so;
  the image and release assets download anonymously.
- **Consequences:** standard hosted runners become free for every platform, so R6/R8
  smoke-test every artifact natively. The repository must be public before the first
  push of `release.yml` (implement), or the macOS/arm64 dry-run minutes are billed.
  Branch protection becomes available as a repository setting (R13).
- **Rationale:** the backlog outcome promises a downloadable alpha for early users.
