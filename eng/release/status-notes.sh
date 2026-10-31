#!/usr/bin/env bash
# Writes the README's release-status section (the lines between the
# <!-- release-status:start --> and <!-- release-status:end --> markers) to a notes file
# for the GitHub prerelease. Fails if either marker is missing or the section is empty.
#
#   eng/release/status-notes.sh <readme> <out-file>
set -euo pipefail

[[ $# -eq 2 ]] || { echo "usage: $0 <readme> <out-file>" >&2; exit 2; }
readme="$1"
out="$2"
start="<!-- release-status:start -->"
end="<!-- release-status:end -->"

fail() {
  echo "status-notes: $*" >&2
  exit 1
}

grep -qxF "$start" "$readme" || fail "$readme has no release-status:start marker ($start)"
grep -qxF "$end" "$readme" || fail "$readme has no release-status:end marker ($end)"

awk -v start="$start" -v end="$end" '
  $0 == end { exit }
  inside { print }
  $0 == start { inside = 1 }
' "$readme" > "$out"

grep -q '[^[:space:]]' "$out" || fail "the release-status section in $readme is empty"
