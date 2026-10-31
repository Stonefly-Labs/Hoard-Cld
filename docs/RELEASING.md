# Releasing

Every merged spec ships an alpha. The pipeline lives in
[`.github/workflows/release.yml`](../.github/workflows/release.yml); pull requests are
gated by [`.github/workflows/ci.yml`](../.github/workflows/ci.yml).

## Product releases: one alpha per merged spec

Every specification is delivered on its own branch and pull request. After the
maintainer merges the PR, the **merge commit** on `main` is tagged:

```text
v0.0.<N>-alpha      N = the spec's numeric directory prefix, without leading zeroes
```

| Spec directory | Tag |
| --- | --- |
| `specs/001-pipeline-skeleton` | `v0.0.1-alpha` |
| `specs/016-live-activity` | `v0.0.16-alpha` |

```bash
git tag -a v0.0.N-alpha <merge-sha> -m "Hoard v0.0.N-alpha — NNN <title>"
git push origin refs/tags/v0.0.N-alpha
```

Tags are annotated and are never moved or recreated. A bad release is corrected with a
new version. The MVP release switches to `v0.1.0` and normal SemVer prereleases
(`-beta.1`, `-rc.1`).

## The release workflow

Pushing a `v0.0.*-alpha` tag runs these jobs:

1. **`guard`** runs [`eng/release/verify-tag.sh`](../eng/release/verify-tag.sh). It
   reads the tag from the environment only and refuses the release, before anything is
   built, with one of these messages:
   - `release tags must match v0.0.N-alpha`: the tag does not match
     `^v0\.0\.[1-9][0-9]*-alpha$`. This is checked before the value is used anywhere else.
   - `release tags must be annotated`: the tag is lightweight.
   - `tag … does not match the commit being built`: the tag does not point at the
     commit the workflow checked out.
   - `tagged commit <sha> is not on main`: the commit is not reachable from `main`.

   On success it outputs the version (`0.0.N-alpha`), which every later job uses instead
   of the raw ref. The guard prevents **mistakes**; it is not access control. A tag push
   runs the workflow as it exists at the tagged commit, so anyone with write access could
   tag a branch whose workflow skips the guard. Write access is the trust boundary.
2. **Verify jobs**. They all must pass:
   - **`verify`**: build, `dotnet test` (including the architecture rules) and
     `dotnet pack` of the packable `Hoard.*` projects, with no version override.
   - **`binaries`**: one job per platform on its native runner (`linux-x64`
     `ubuntu-latest`, `linux-arm64` `ubuntu-24.04-arm`, `win-x64` `windows-latest`,
     `osx-arm64` `macos-latest`). Each publishes a self-contained single-file executable,
     checks its architecture with `file`, and runs
     [`eng/smoke/smoke-binary.sh`](../eng/smoke/smoke-binary.sh) on it. One platform's
     failure still reports the others.
   - **`container`**: builds the `linux/amd64` and `linux/arm64` image once, from the
     two Linux executables, into an OCI archive.
   - **`container-smoke`**: loads each architecture from that archive on its native
     runner and runs [`eng/smoke/smoke-container.sh`](../eng/smoke/smoke-container.sh).
3. **`publish`** runs only on a tag push, after the guard and every verify job
   succeeded. It is the only job with write permissions (`contents`, `packages`), and
   `GITHUB_TOKEN` is its only credential. In order:
   1. **Packages** go to GitHub Packages
      (`https://nuget.pkg.github.com/stonefly-labs/index.json`) with `--skip-duplicate`.
   2. **Image**: the smoke-tested archive is pushed to `ghcr.io/stonefly-labs/hoard:<version>`
      (both architectures), unless that tag already exists. `latest` is never written.
   3. **Release**: a GitHub prerelease for the tag, with the notes and every asset in one
      call.

The workflow never creates, moves or deletes tags, and never overwrites a published
artifact.

## Published artifacts

| Artifact | Where |
| --- | --- |
| Container image | `ghcr.io/stonefly-labs/hoard:<version>` (`linux/amd64`, `linux/arm64`) |
| Executables | Release assets `hoard-<version>-linux-x64`, `-linux-arm64`, `-osx-arm64`, `-win-x64.exe` |
| Checksums | Release asset `SHA256SUMS` |
| Packages | `Hoard.Core` in GitHub Packages |

`SHA256SUMS` has one line per executable. Verify a download with
`sha256sum -c SHA256SUMS --ignore-missing`. The file is hosted next to the executables,
so it proves a download is **intact**, not who built it.

The image and release assets download anonymously. GitHub Packages' NuGet registry
needs authentication even for public packages, so consuming `Hoard.Core` needs a token
with `read:packages`.

## Release notes

The notes are the README's release-status section (between
`<!-- release-status:start -->` and `<!-- release-status:end -->`, extracted by
[`eng/release/status-notes.sh`](../eng/release/status-notes.sh)), followed by GitHub's
generated list of merged PRs. Every spec PR updates that section, so the notes say what
works and what is planned. The release fails if the markers are missing or the section is
empty.

## Re-runs

Re-running a release run skips what already exists. An existing package version is
skipped, an existing image tag is not pushed, and only release assets that are missing
get uploaded. After a partial publish (for example, the package was pushed but the image
push failed), fix the cause outside the repository if you can and re-run the same run.

A re-run rebuilds the executables, and single-file publishing is not bit-for-bit
reproducible. If a failed run attached only some assets, a re-uploaded `SHA256SUMS` might
not match the originals. In that case, release a new version; never retag.

After `v0.0.1-alpha` publishes, its release run is re-run once to prove the
already-published skip.

## Package versions

Each `Hoard.*` package carries its own SemVer version (`<Version>`) in its project file
and is bumped in the PR that changes it. `Hoard.Core` starts at `0.0.1-alpha`. The release
packs every packable project and pushes with `--skip-duplicate`, so an unchanged version
is silently skipped. Reviewers check that a changed package's version was bumped.
Publishing to nuget.org is deferred until the public provider contracts stabilize.

## One-time settings

Before `v0.0.1-alpha`, the maintainer makes the repository public. After the first
publish, the maintainer sets the `hoard` container package and the `Hoard.Core` package
to public in the organization's package settings, then confirms
`docker pull ghcr.io/stonefly-labs/hoard:0.0.1-alpha` works anonymously. The workflow
never changes visibility.

## CI

Every pull request to `main`, and every push to `main`, runs `ci.yml`:

- **`build-test`**: `dotnet build` (warnings are errors) and `dotnet test`, including the
  architecture dependency rules and the release-script tests. Failing tests are named in
  the job output.
- **`smoke`**: publishes the `linux-x64` executable and builds an amd64 image from it,
  then runs both smoke scripts.

Pull requests that change `release.yml`, `eng/**` or `deploy/**` also run the release
workflow's verify jobs as a dry run (version `0.0.0-pr`, `publish` skipped). That covers
every executable and both image architectures on native standard runners, which are free
because the repository is public. A PR merges only when CI is green.
