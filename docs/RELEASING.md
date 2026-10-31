# Releasing

> This describes the intended release process. The workflows that implement it
> are built by the first specification (pipeline and skeleton); until then no
> releases exist.

## Product releases: one alpha per merged spec

Every specification is delivered on its own branch and pull request. After the
maintainer merges the PR, the **merge commit** on `main` is tagged:

```text
v0.0.<N>-alpha      N = the spec's numeric directory prefix, without leading zeroes
```

| Spec directory | Tag |
| --- | --- |
| `specs/001-pipeline-skeleton` | `v0.0.1-alpha` |
| `specs/012-console-activity` | `v0.0.12-alpha` |

Tags are annotated, must point at a commit reachable from `main`, and are never
moved or recreated. A bad release is corrected with a new version. The MVP
release switches to `v0.1.0` and normal SemVer prereleases (`-beta.1`, `-rc.1`).

Pushing a tag runs the release workflow, which:

1. Builds and tests the tagged commit.
2. Publishes a container image to GHCR (`ghcr.io/stonefly-labs/hoard`) for
   `linux/amd64` and `linux/arm64`, tagged with the version. Prereleases never
   move `latest`.
3. Builds self-contained executables for `linux-x64`, `linux-arm64`,
   `osx-arm64` and `win-x64`, with SHA-256 checksums.
4. Publishes changed NuGet packages to GitHub Packages (see below).
5. Creates a GitHub prerelease with generated notes, the binaries and checksums.
   Notes distinguish working capabilities from planned ones.

## Package versions

Each `Hoard.*` package carries its own SemVer version in its project file and
is bumped in the PR that changes it. The release workflow packs every package
and pushes only versions not already published to GitHub Packages; an
unchanged package keeps its version. Publishing to nuget.org is deferred until
the public provider contracts stabilize.

## CI

Every pull request runs build, tests (including real `dotnet` client tests and
architecture checks) and console checks in GitHub Actions. A PR merges only
when CI is green.
