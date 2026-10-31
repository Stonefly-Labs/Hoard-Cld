# Quickstart: validating spec 001

Prerequisites: .NET 10 SDK, Docker, bash, `lsof`, `gh` (for release checks).

## 1. Build and test locally

```bash
dotnet build Hoard.slnx
dotnet test --solution Hoard.slnx
```

Expected: build succeeds with no warnings. All architecture, host and release-guard tests
pass, including the tests that prove each forbidden reference fails (research R2, R3).

## 2. Run the host

```bash
dotnet run --project src/Hoard.Host
curl -i http://127.0.0.1:8484/health     # 200 Healthy
curl -i http://127.0.0.1:8484/nope       # 404
```

See the [health endpoint contract](contracts/health-endpoint.md).

## 3. Smoke-test packaged artifacts (as CI does)

```bash
dotnet publish src/Hoard.Host -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:DebugType=none -o out/publish
mkdir -p out/bin && cp out/publish/Hoard.Host out/bin/hoard-0.0.0-local-linux-x64   # release naming
eng/smoke/smoke-binary.sh out/bin/hoard-0.0.0-local-linux-x64

docker buildx build -f deploy/docker/Dockerfile --platform linux/amd64 \
  --build-context bin=out/bin -t hoard:local --load .
eng/smoke/smoke-container.sh hoard:local
```

The Dockerfile takes the prebuilt executables from the `bin` build context, picking
`hoard-*-linux-x64` or `hoard-*-linux-arm64` by target architecture. On an arm64 machine,
publish `linux-arm64` and build `--platform linux/arm64`. Run `smoke-binary.sh` on an
executable for the machine's own RID.

Expected: both report healthy within 30 s, in the clean run and in every hostile run.
Every listener is on loopback, and a neighboring container gets connection refused.

## 4. Prove the PR gate fails (once, on the 001 PR)

This also covers SC-005's "failing test publishes nothing". `publish` depends on the
same test job, so the guarantee is structural (research R10).

Push a temporary commit with a failing test, then revert it. Expected: `build-test`
fails and names the test. Adding a `ProjectReference` from Core to a
`Hoard.Persistence.*` project is already covered by the restore-based architecture test.

## 5. Release dry run (on the 001 PR)

`release.yml` runs because the PR adds it. Expected: all four executables are built,
architecture-checked and pass smoke on their native runners, the multi-arch image builds,
both the amd64 and arm64 images pass smoke, and `publish` is skipped.

## 6. First release (after the maintainer merges)

```bash
git tag -a v0.0.1-alpha <merge-sha> -m "Hoard v0.0.1-alpha — 001 Pipeline and solution skeleton"
git push origin refs/tags/v0.0.1-alpha
gh run watch <id>
gh release view v0.0.1-alpha
```

Expected:
- A prerelease with notes (working vs planned), four executables and `SHA256SUMS`.
  `sha256sum -c SHA256SUMS --ignore-missing` passes for a downloaded executable.
- `docker buildx imagetools inspect ghcr.io/stonefly-labs/hoard:0.0.1-alpha` lists
  amd64 and arm64.
- `Hoard.Core` appears in the org's GitHub Packages.

One-time settings (docs/RELEASING.md): the repository is already public. After this first
publish, set the `hoard` container package and the `Hoard.Core` package to public, then
check that `docker pull ghcr.io/stonefly-labs/hoard:0.0.1-alpha` works without logging in.

## 7. Re-run proves idempotency (SC-005)

Done once, during the `drive-speckit` `release` step for `v0.0.1-alpha`, after §6 passes.
Re-run the successful release run (`gh run rerun <id>`). Expected: the package is skipped
as already published, the image push is skipped, no release assets are replaced, and the
run succeeds. Record the outcome in the progress log's `release` entry.
