#!/usr/bin/env bash
# Smoke-tests a published Hoard executable on port 8484:
#   - by default it serves /health/live and /health/ready (200 Healthy within 30 s), answers 404
#     for /health and anything else, and listens on loopback only, also when the environment, an
#     appsettings.json or Kestrel settings ask for a wider binding;
#   - a framework --urls argument, a non-loopback address without a TLS mode and a certificate
#     with the wrong key are refused: non-zero exit, nothing listening;
#   - --listen:address 0.0.0.0 with TLS mode proxy listens on 0.0.0.0:8484 only and logs the
#     exposure warning;
#   - TLS mode https with an openssl-generated PEM pair serves HTTPS only, on loopback only.
# Every serving run compares the full listener address:port set, so an extra listener fails it.
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

# Writes one listening address:port per line (127.0.0.1:8484, [::1]:8484, 0.0.0.0:8484, ...).
list_listeners() {
  if $windows; then
    local winpid
    winpid="$(cat "/proc/$pid/winpid")"
    powershell -NoProfile -Command \
      "Get-NetTCPConnection -State Listen -OwningProcess $winpid -ErrorAction SilentlyContinue | ForEach-Object { if (\$_.LocalAddress.Contains(':')) { '[' + \$_.LocalAddress + ']:' + \$_.LocalPort } else { \$_.LocalAddress + ':' + \$_.LocalPort } }" \
      | tr -d '\r' | sort -u > "$work/listeners"
  else
    # lsof prints the wildcard address as *; its TYPE column says which family it is.
    lsof -nP -a -p "$pid" -iTCP -sTCP:LISTEN 2>/dev/null \
      | awk 'NR > 1 { name = $9; if (name ~ /^\*:/) { sub(/^\*/, $5 == "IPv6" ? "[::]" : "0.0.0.0", name) } print name }' \
      | sort -u > "$work/listeners" || true
  fi
}

# The listener set must contain every <required> address and nothing outside <allowed>.
expect_listeners() {
  local required="$1" allowed="$2" address
  list_listeners
  [[ -s "$work/listeners" ]] || fail "no listening sockets found for the host process"
  for address in $required; do
    grep -qxF "$address" "$work/listeners" || fail "expected a listener on $address"
  done
  while read -r address; do
    [[ " $allowed " == *" $address "* ]] || fail "unexpected listener: $address (allowed: $allowed)"
  done < "$work/listeners"
}

nothing_listening() {
  local status
  status="$(curl -s -o /dev/null -w '%{http_code}' --max-time 2 "$base/health/live" || true)"
  [[ "$status" == "000" ]] || fail "something answered on $base (status $status)"
}

# start <directory to run from> [args...]; extra env comes from the caller.
start() {
  local dir="$1"
  shift
  : > "$work/listeners"
  nothing_listening
  (cd "$dir" && exec "$exe" "$@") > "$work/host.log" 2>&1 &
  pid=$!
}

# wait_healthy <url base> [curl options...]
wait_healthy() {
  local url="$1" deadline=$((SECONDS + 30)) status="" path
  shift
  while ((SECONDS < deadline)); do
    kill -0 "$pid" 2>/dev/null || fail "host exited before becoming healthy"
    status="$(curl -s "$@" -o "$work/body" -w '%{http_code}' --max-time 2 "$url/health/ready" || true)"
    [[ "$status" == "200" ]] && break
    sleep 0.5
  done
  [[ "$status" == "200" ]] || fail "GET /health/ready did not return 200 within 30 s (last status: ${status:-none})"
  for path in /health/live /health/ready; do
    status="$(curl -s "$@" -o "$work/body" -w '%{http_code}' --max-time 5 "$url$path" || true)"
    [[ "$status" == "200" && "$(cat "$work/body")" == "Healthy" ]] || fail "GET $path returned ${status:-no response} '$(cat "$work/body" 2>/dev/null)', expected 200 Healthy"
  done
  for path in /health /anything-else; do
    status="$(curl -s "$@" -o /dev/null -w '%{http_code}' --max-time 5 "$url$path" || true)"
    [[ "$status" == "404" ]] || fail "GET $path returned ${status:-no response}, expected 404"
  done
}

# serves <name> <directory> [args...]: loopback only, plain HTTP.
serves() {
  run_name="$1"
  shift
  start "$@"
  wait_healthy "$base"
  expect_listeners "127.0.0.1:8484" "127.0.0.1:8484 [::1]:8484"
  ! grep -q "reachable from the network" "$work/host.log" || fail "loopback run logged the exposure warning"
  stop
  echo "smoke-binary: PASS [$run_name] listeners: $(paste -sd ' ' "$work/listeners")"
}

# refused <name> <directory> <text the output must contain>... -- [args...]
refused() {
  run_name="$1"
  local dir="$2" texts=() text
  shift 2
  while [[ "$1" != "--" ]]; do texts+=("$1"); shift; done
  shift
  start "$dir" "$@"
  local deadline=$((SECONDS + 30))
  while kill -0 "$pid" 2>/dev/null && ((SECONDS < deadline)); do sleep 0.2; done
  kill -0 "$pid" 2>/dev/null && fail "host still running after 30 s, expected it to refuse to start"
  local code=0
  wait "$pid" || code=$?
  pid=""
  [[ "$code" == "2" ]] || fail "exit code $code, expected 2"
  nothing_listening
  for text in "${texts[@]}"; do
    grep -qF -- "$text" "$work/host.log" || fail "output does not mention '$text'"
  done
  echo "smoke-binary: PASS [$run_name] refused with exit 2"
}

serves clean "$work"

(
  export ASPNETCORE_URLS="http://0.0.0.0:8080"
  export ASPNETCORE_HTTP_PORTS="8081"
  export ASPNETCORE_HTTPS_PORTS="8443"
  export DOTNET_URLS="http://0.0.0.0:8082"
  export ASPNETCORE_PREFERHOSTINGURLS="true"
  export Kestrel__Endpoints__H__Url="http://0.0.0.0:8485"
  serves hostile-environment "$work"
)

mkdir "$work/appsettings"
cat > "$work/appsettings/appsettings.json" <<'JSON'
{ "Kestrel": { "Endpoints": { "Wide": { "Url": "http://0.0.0.0:8487" } } } }
JSON
serves hostile-appsettings "$work/appsettings"

refused hostile-command-line "$work" "--urls" -- --urls "http://0.0.0.0:8086"

refused no-tls-mode "$work" "'https'" "'proxy'" -- --listen:address 0.0.0.0

run_name="exposed"
start "$work" --listen:address 0.0.0.0 --listen:tls:mode proxy
wait_healthy "$base"
expect_listeners "0.0.0.0:8484" "0.0.0.0:8484"
grep -q "Hoard is listening on 0.0.0.0, which is reachable from the network" "$work/host.log" || fail "no exposure warning"
grep -q "TLS-terminating reverse proxy" "$work/host.log" || fail "the exposure warning does not mention the reverse proxy"
stop
echo "smoke-binary: PASS [$run_name] listeners: $(paste -sd ' ' "$work/listeners")"

# Certificates from openssl, which every runner has (Git Bash included). MSYS_NO_PATHCONV keeps
# Git Bash from rewriting -subj /CN=... into a Windows path.
mkdir "$work/tls"
(
  cd "$work/tls"
  export MSYS_NO_PATHCONV=1
  openssl req -x509 -newkey rsa:2048 -nodes -keyout key.pem -out cert.pem -days 2 -subj /CN=localhost 2>/dev/null
  openssl req -x509 -newkey rsa:2048 -nodes -keyout other-key.pem -out other-cert.pem -days 2 -subj /CN=other 2>/dev/null
)

run_name="https"
start "$work/tls" --listen:tls:mode https --listen:tls:certificateFile cert.pem --listen:tls:keyFile key.pem
wait_healthy "https://127.0.0.1:8484" -k
expect_listeners "127.0.0.1:8484" "127.0.0.1:8484 [::1]:8484"
plain=0
curl -s -o /dev/null --max-time 5 "$base/health/ready" || plain=$?
[[ "$plain" == "52" || "$plain" == "56" ]] || fail "plain HTTP to the HTTPS port gave curl exit $plain, expected 52 or 56 (no HTTP response)"
grep -q "TLS mode https, certificate CN=localhost" "$work/host.log" || fail "the summary does not name the certificate"
stop
echo "smoke-binary: PASS [$run_name] listeners: $(paste -sd ' ' "$work/listeners")"

refused bad-certificate "$work/tls" "--listen:tls:certificateFile" "does not match" -- \
  --listen:tls:mode https --listen:tls:certificateFile cert.pem --listen:tls:keyFile other-key.pem

echo "smoke-binary: all runs passed for $(basename "$exe")"
