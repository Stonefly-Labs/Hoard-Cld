# Contract: CI checks and release outputs

## Pull request checks (`ci.yml`)

Triggered on every `pull_request` targeting `main` and on `push` to `main`.

| Check | Fails when |
|---|---|
| `build-test` | the solution does not compile (warnings are errors), or any test fails, including the architecture rules |
| `smoke` | the `linux-x64` executable or the amd64 image does not report `200 Healthy` within 30 s, answers a non-`/health` path with anything but `404`, or listens on a non-loopback address |

`release.yml` also runs its verify jobs (no publishing) on PRs that change
`.github/workflows/release.yml`, `eng/**`, `deploy/**`, `global.json`,
`Directory.*.props` or `src/**/*.csproj`.

## Release trigger (`release.yml`)

| Input | Outcome |
|---|---|
| Push of tag matching `v0.0.*-alpha` | Release runs |
| Tag not matching `^v0\.0\.[1-9][0-9]*-alpha$` | Fails in `guard`: "release tags must match v0.0.N-alpha" (or the workflow is not triggered at all) |
| Lightweight tag | Fails in `guard`: "release tags must be annotated" |
| Tag on a commit not reachable from `main` | Fails in `guard`: "tagged commit <sha> is not on main" |
| Any build, test, arch-check or smoke failure | Fails before `publish`; nothing is published |

## Published outputs (all only after every verify job passes)

| Output | Location |
|---|---|
| Container image | `ghcr.io/stonefly-labs/hoard:<version>` (`linux/amd64`, `linux/arm64`); `latest` untouched |
| Executables | GitHub release assets `hoard-<version>-linux-x64`, `-linux-arm64`, `-osx-arm64`, `-win-x64.exe` |
| Checksums | GitHub release asset `SHA256SUMS`; verify with `sha256sum -c SHA256SUMS --ignore-missing` |
| Package | `Hoard.Core` in GitHub Packages, `https://nuget.pkg.github.com/stonefly-labs/index.json` |
| Release | GitHub prerelease for the tag; notes = README release-status section + generated PR list |

## Guarantees

- The release never creates, moves or deletes tags.
- The release never overwrites: an existing package version is skipped, an existing image
  tag is not pushed, existing release assets are not replaced.
- A re-run for the same tag publishes only what is missing.
