#!/usr/bin/env bash
# Smoke-tests a published Hoard executable: it must report healthy on 127.0.0.1:8484
# within 30 s, answer 404 elsewhere, and listen on loopback only, including when the
# environment, command line or an appsettings.json next to it asks for a wider binding.
#
#   eng/smoke/smoke-binary.sh <path-to-executable>
#
# Runs on Linux and macOS (lsof) and on Windows under Git Bash (Get-NetTCPConnection).
set -euo pipefail

[[ $# -eq 1 && -f "$1" ]] || { echo "usage: $0 <path-to-executable>" >&2; exit 2; }
exe="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
base="http://127.0.0.1:8484"
work="$(mktemp -d)"
pid=""
run_name=""

case "$(uname -s)" in
  MINGW* | MSYS* | CYGWIN*) windows=true ;;
  *) windows=false ;;
esac

stop() {
  if [[ -n "$pid" ]]; then
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
    pid=""
  fi
}

cleanup() {
  stop
  rm -rf "$work"
}
trap cleanup EXIT

fail() {
  echo "smoke-binary: FAIL [$run_name]: $*" >&2
  if [[ -s "$work/listeners" ]]; then
    echo "--- listeners ---" >&2
    cat "$work/listeners" >&2
  fi
  if [[ -s "$work/host.log" ]]; then
    echo "--- host output (last 40 lines) ---" >&2
    tail -n 40 "$work/host.log" >&2
  fi
  exit 1
}

# Writes one listening address per line (127.0.0.1, [::1], 0.0.0.0, ...) to $work/listeners.
list_listeners() {
  if $windows; then
    local winpid
    winpid="$(cat "/proc/$pid/winpid")"
    powershell -NoProfile -Command \
      "Get-NetTCPConnection -State Listen -OwningProcess $winpid -ErrorAction SilentlyContinue | ForEach-Object { \$_.LocalAddress }" \
      | tr -d '\r' | sed -e 's/^::1$/[::1]/' > "$work/listeners"
  else
    lsof -nP -a -p "$pid" -iTCP -sTCP:LISTEN 2>/dev/null \
      | awk 'NR > 1 { sub(/:[0-9]+$/, "", $9); print $9 }' > "$work/listeners" || true
  fi
}

# smoke <name> <directory to run from> [args...]; extra env comes from the caller.
smoke() {
  run_name="$1"
  local dir="$2"
  shift 2
  : > "$work/listeners"

  if curl -s -o /dev/null --max-time 2 "$base/health"; then
    fail "something is already listening on $base before the host started"
  fi

  (cd "$dir" && exec "$exe" "$@") > "$work/host.log" 2>&1 &
  pid=$!

  local deadline=$((SECONDS + 30)) status=""
  while ((SECONDS < deadline)); do
    kill -0 "$pid" 2>/dev/null || fail "host exited before becoming healthy"
    status="$(curl -s -o "$work/body" -w '%{http_code}' --max-time 2 "$base/health" || true)"
    [[ "$status" == "200" ]] && break
    sleep 0.5
  done
  [[ "$status" == "200" ]] || fail "GET /health did not return 200 within 30 s (last status: ${status:-none})"
  [[ "$(cat "$work/body")" == "Healthy" ]] || fail "GET /health body was '$(cat "$work/body")', expected 'Healthy'"

  status="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$base/anything-else" || true)"
  [[ "$status" == "404" ]] || fail "GET /anything-else returned ${status:-no response}, expected 404"

  list_listeners
  [[ -s "$work/listeners" ]] || fail "no listening sockets found for the host process"
  while read -r address; do
    [[ "$address" == "127.0.0.1" || "$address" == "[::1]" ]] || fail "non-loopback listener: $address"
  done < "$work/listeners"

  stop
  echo "smoke-binary: PASS [$run_name] listeners: $(paste -sd ' ' "$work/listeners")"
}

smoke clean "$work"

(
  export ASPNETCORE_URLS="http://0.0.0.0:8080"
  export ASPNETCORE_HTTP_PORTS="8081"
  export ASPNETCORE_HTTPS_PORTS="8443"
  export DOTNET_URLS="http://0.0.0.0:8082"
  export ASPNETCORE_PREFERHOSTINGURLS="true"
  export Kestrel__Endpoints__H__Url="http://0.0.0.0:8485"
  smoke hostile-environment "$work"
)

smoke hostile-command-line "$work" --urls "http://0.0.0.0:8086"

mkdir "$work/appsettings"
cat > "$work/appsettings/appsettings.json" <<'JSON'
{ "Kestrel": { "Endpoints": { "Wide": { "Url": "http://0.0.0.0:8487" } } } }
JSON
smoke hostile-appsettings "$work/appsettings"

echo "smoke-binary: all runs passed for $(basename "$exe")"
