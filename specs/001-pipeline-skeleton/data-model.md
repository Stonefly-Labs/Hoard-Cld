# Data model: Pipeline and solution skeleton

There is no persisted data in this spec. The entities are release-process records,
named here so the workflows, scripts and docs use them consistently.

## Release tag

| Field | Rule |
|---|---|
| Name | `^v0\.0\.[1-9][0-9]*-alpha$`, e.g. `v0.0.1-alpha` |
| Type | Annotated (`git cat-file -t` = `tag`); a lightweight tag is rejected |
| Target | A commit reachable from `origin/main` (normally the spec's merge commit) |
| Version | The tag name without the leading `v` (`0.0.1-alpha`); stamped into the host as `-p:Version` |
| Lifecycle | Pushed by the maintainer, then immutable. The pipeline never creates, moves or deletes it |

## Release artifact set (per version)

| Artifact | Identity | Published to | Re-run behavior |
|---|---|---|---|
| Executable × 4 | `hoard-<version>-<rid>` (`.exe` for `win-x64`); RIDs `linux-x64`, `linux-arm64`, `osx-arm64`, `win-x64` | GitHub release assets | Uploaded only if missing |
| Checksums | `SHA256SUMS` (`sha256sum` format, one line per executable; proves integrity, not origin) | GitHub release asset | Uploaded only if missing |
| Container image | `ghcr.io/stonefly-labs/hoard:<version>`; manifest list `linux/amd64` + `linux/arm64` | GHCR | Pushed only if the tag does not exist; `latest` never written |
| Package | `Hoard.Core` at the `<Version>` in its project file (initially `0.0.1-alpha`; independent of the release version) | GitHub Packages (`nuget.pkg.github.com/stonefly-labs`) | Existing version skipped (`--skip-duplicate`) |
| GitHub release | Tag `v<version>`, prerelease, notes = README `release-status` section + generated PR list | GitHub Releases | Reused, never duplicated |

## Release run state

```text
guard ──► verify (build, test, pack) ──► binaries[rid] (publish, arch check, smoke*, checksum)
                                                  │
                                                  ▼
                                    container (build multi-arch, smoke amd64 + arm64)
                                                  │
                         all succeeded? ──no──► run fails, nothing published
                                                  │ yes
                                                  ▼
                            publish: packages → image → GitHub release
```

\* Every executable is architecture-checked and smoke-tested on its native runner.
