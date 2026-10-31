#!/usr/bin/env bash
# Refuses a release unless the pushed tag is a well-formed, annotated v0.0.N-alpha tag on
# the commit being built, and that commit is on main. Writes version=<0.0.N-alpha> to
# $GITHUB_OUTPUT. Inputs come from the environment only: TAG, GITHUB_SHA, GITHUB_OUTPUT.
# This prevents mistakes; it is not access control (docs/RELEASING.md).
set -euo pipefail

fail() {
  echo "verify-tag: $*" >&2
  exit 1
}

tag="${TAG:?TAG is required}"
sha="${GITHUB_SHA:?GITHUB_SHA is required}"
output="${GITHUB_OUTPUT:?GITHUB_OUTPUT is required}"

# Validate before the value is used anywhere else.
[[ "$tag" =~ ^v0\.0\.[1-9][0-9]*-alpha$ ]] || fail "release tags must match v0.0.N-alpha"

# actions/checkout can leave an annotated tag looking lightweight; fetch the real object.
git fetch --quiet --force origin "+refs/tags/$tag:refs/tags/$tag"
[[ "$(git cat-file -t "refs/tags/$tag")" == "tag" ]] || fail "release tags must be annotated"

tagged="$(git rev-parse "refs/tags/$tag^{commit}")"
[[ "$tagged" == "$sha" ]] || fail "tag $tag points at $tagged, which does not match the commit being built ($sha)"

git fetch --quiet origin "+refs/heads/main:refs/remotes/origin/main"
git merge-base --is-ancestor "$tagged" refs/remotes/origin/main || fail "tagged commit $tagged is not on main"

echo "version=${tag#v}" >> "$output"
