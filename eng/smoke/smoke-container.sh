#!/usr/bin/env bash
# Smoke-tests a Hoard container image:
#   - non-root user, from the image config and from the running process's uids;
#   - default run: healthy within 30 s, reachable from a neighbouring container on the bridge at
#     :8484, exposure warning logged, and 0.0.0.0:8484 the only listener, also under a hostile
#     framework environment;
#   - a framework --urls argument is refused;
#   - -e HOARD_LISTEN__ADDRESS=127.0.0.1 listens on loopback only and is refused from the bridge;
#   - a named volume at /data survives recreating the container, and docker stop exits 0 in < 10 s;
#   - TLS mode https with a mounted PEM serves HTTPS on the bridge and no plain HTTP.
# Every run is --read-only with all capabilities dropped and compares the exact listener set.
#
#   eng/smoke/smoke-container.sh <image>
set -euo pipefail

[[ $# -eq 1 ]] || { echo "usage: $0 <image>" >&2; exit 2; }
image="$1"
# Multi-arch index digest, so the same script runs on amd64 and arm64 runners.
helper="curlimages/curl:8.23.0@sha256:3fb7e13e5d179a845c97d78204f3f1064d1907037f26a3ebf91710571151fd93"
network="hoard-smoke-$$"
volume="hoard-smoke-data-$$"
work="$(mktemp -d)"
containers=()
run_name="image"
cid=""

cleanup() {
  for c in "${containers[@]}"; do docker rm -f "$c" > /dev/null 2>&1 || true; done
  docker network rm "$network" > /dev/null 2>&1 || true
  docker volume rm "$volume" > /dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

fail() {
  echo "smoke-container: FAIL [$run_name]: $*" >&2
  if [[ -n "$cid" ]]; then
    echo "--- container logs (last 40 lines) ---" >&2
    docker logs --tail 40 "$cid" >&2 2>&1 || true
  fi
  exit 1
}

user="$(docker image inspect -f '{{.Config.User}}' "$image")"
[[ -n "$user" && "${user%%:*}" != "root" && "${user%%:*}" != "0" ]] || fail "image user is '${user:-<unset>}', expected a non-root user"
uid="${user%%:*}"
[[ "$uid" =~ ^[0-9]+$ ]] || fail "image user is '$user', expected a numeric uid"
echo "smoke-container: image user is $user"

docker network create "$network" > /dev/null

# Positive control: the probe path reaches a listener on this bridge, so a refusal of
# Hoard's port below means Hoard is not listening there, not that probing is broken.
control="$(docker run -d --network "$network" --entrypoint sh "$helper" \
  -c 'while true; do printf "HTTP/1.0 200 OK\r\n\r\nok" | nc -l -p 9000; done')"
containers+=("$control")
control_ip="$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' "$control")"
control_ok=false
for _ in $(seq 1 20); do
  if [[ "$(docker run --rm --network "$network" "$helper" -s --max-time 3 "http://$control_ip:9000/" || true)" == "ok" ]]; then
    control_ok=true
    break
  fi
  sleep 0.5
done
$control_ok || fail "positive control: probe could not reach a listener on the bridge network"

# Runs inside the Hoard container's network namespace: health paths, then the socket tables.
inside_script='
deadline=$(( $(date +%s) + 30 )); status=""
while [ "$(date +%s)" -lt "$deadline" ]; do
  status=$(curl -sk -o /tmp/body -w "%{http_code}" --max-time 2 "$SCHEME://127.0.0.1:8484/health/ready" || true)
  [ "$status" = "200" ] && break
  sleep 0.5
done
echo "ready=$status body=$(cat /tmp/body 2>/dev/null)"
echo "live=$(curl -sk -o /dev/null -w "%{http_code}" --max-time 5 "$SCHEME://127.0.0.1:8484/health/live" || true)"
echo "health=$(curl -sk -o /dev/null -w "%{http_code}" --max-time 5 "$SCHEME://127.0.0.1:8484/health" || true)"
echo "other=$(curl -sk -o /dev/null -w "%{http_code}" --max-time 5 "$SCHEME://127.0.0.1:8484/anything-else" || true)"
cat /proc/net/tcp /proc/net/tcp6 2>/dev/null | awk "\$4 == \"0A\" { print \"listen=\" \$2 }"
'

# The container's listener set in /proc/net/tcp hex: 0.0.0.0:8484 or 127.0.0.1:8484.
all_interfaces="00000000:2124"
loopback="0100007F:2124"

# probe <url> [curl options...]: prints curl's exit code and the status, as "<exit> <status>".
probe() {
  local url="$1" exit_code=0 status
  shift
  status="$(docker run --rm --network "$network" "$helper" -s "$@" -o /dev/null -w '%{http_code}' --max-time 5 "$url")" || exit_code=$?
  echo "$exit_code $status"
}

# smoke <run-name> <listeners> <outside: reachable|refused> <scheme> [docker run options...] [-- image arguments...]
# Leaves the container running in $cid; the caller removes it.
smoke() {
  run_name="$1"
  local expected="$2" outside="$3" scheme="$4"
  shift 4
  local options=() arguments=()
  while [[ $# -gt 0 && "$1" != "--" ]]; do options+=("$1"); shift; done
  [[ $# -gt 0 ]] && shift
  arguments=("$@")
  cid="$(docker run -d --network "$network" --read-only --cap-drop ALL \
    --security-opt no-new-privileges ${options[@]+"${options[@]}"} "$image" ${arguments[@]+"${arguments[@]}"})"
  containers+=("$cid")

  local report
  report="$(docker run --rm --network "container:$cid" -e SCHEME="$scheme" --entrypoint sh "$helper" -c "$inside_script")"
  grep -qx 'ready=200 body=Healthy' <<< "$report" || fail "inside view: expected /health/ready 200 Healthy within 30 s, got: $(head -n 1 <<< "$report")"
  grep -qx 'live=200' <<< "$report" || fail "inside view: GET /health/live: $(sed -n 2p <<< "$report"), expected 200"
  grep -qx 'health=404' <<< "$report" || fail "inside view: GET /health: $(sed -n 3p <<< "$report"), expected 404"
  grep -qx 'other=404' <<< "$report" || fail "inside view: GET /anything-else: $(sed -n 4p <<< "$report"), expected 404"

  # The static user check above can't resolve named users or forms like `00`; the kernel can.
  local uids
  uids="$(docker run --rm --pid "container:$cid" --entrypoint sh "$helper" \
    -c 'awk "\$1 == \"Uid:\" { print \$2, \$3, \$4, \$5 }" /proc/1/status')"
  [[ "$uids" =~ ^[0-9]+\ [0-9]+\ [0-9]+\ [0-9]+$ ]] || fail "process view: could not read the app's uids, got '$uids'"
  for u in $uids; do
    (( 10#$u != 0 )) || fail "process view: the app runs with uid 0 (real/effective/saved/fs: $uids)"
  done

  # 127.0.0.11 (0B00007F) is Docker's embedded DNS resolver, present in every container on a
  # user-defined network; it is not Hoard's.
  local listeners
  listeners="$(sed -n 's/^listen=//p' <<< "$report" | grep -v '^0B00007F:' | sort -u | paste -sd ' ' -)"
  [[ "$listeners" == "$expected" ]] || fail "inside view: listeners '$listeners' (/proc/net/tcp hex), expected exactly '$expected'"

  local ip result
  ip="$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' "$cid")"
  if [[ "$outside" == "reachable" ]]; then
    result="$(probe "$scheme://$ip:8484/health/ready" -k)"
    [[ "$result" == "0 200" ]] || fail "outside view: $scheme://$ip:8484/health/ready from the bridge gave curl exit/status '$result', expected '0 200'"
  else
    result="$(probe "http://$ip:8484/health/ready")"
    [[ "${result%% *}" == "7" ]] || fail "outside view: $ip:8484 from the bridge gave curl exit ${result%% *}, expected 7 (connection refused)"
  fi

  echo "smoke-container: PASS [$run_name] listeners: $listeners"
}

# Every --read-only run needs a writable /data: a tmpfs owned by the app user, or a named volume.
data_tmpfs="/data:uid=$uid,gid=$uid,mode=0700"

smoke default "$all_interfaces" reachable http --tmpfs "$data_tmpfs"
docker logs "$cid" 2>&1 | grep -q "Hoard is listening on 0.0.0.0, which is reachable from the network" || fail "no exposure warning in the logs"
docker logs "$cid" 2>&1 | grep -q "TLS-terminating reverse proxy" || fail "the exposure warning does not mention the reverse proxy"
docker rm -f "$cid" > /dev/null

smoke hostile-environment "$all_interfaces" reachable http --tmpfs "$data_tmpfs" \
  -e ASPNETCORE_URLS="http://0.0.0.0:8080" \
  -e ASPNETCORE_HTTP_PORTS="8081" \
  -e ASPNETCORE_HTTPS_PORTS="8443" \
  -e DOTNET_URLS="http://0.0.0.0:8082" \
  -e ASPNETCORE_PREFERHOSTINGURLS="true" \
  -e Kestrel__Endpoints__H__Url="http://0.0.0.0:8485"
docker rm -f "$cid" > /dev/null

run_name="hostile-command-line"
cid="$(docker run -d --network "$network" --read-only --cap-drop ALL --security-opt no-new-privileges \
  --tmpfs "$data_tmpfs" "$image" --urls "http://0.0.0.0:8086")"
containers+=("$cid")
code="$(docker wait "$cid")"
[[ "$code" == "2" ]] || fail "exit code $code, expected 2"
docker logs "$cid" 2>&1 | grep -qF -- "command line --urls: unknown option" || fail "the refusal does not name --urls"
docker rm -f "$cid" > /dev/null
echo "smoke-container: PASS [$run_name] refused with exit 2"

smoke loopback-override "$loopback" refused http --tmpfs "$data_tmpfs" -e HOARD_LISTEN__ADDRESS=127.0.0.1
! docker logs "$cid" 2>&1 | grep -q "reachable from the network" || fail "loopback run logged the exposure warning"
docker rm -f "$cid" > /dev/null

# Named volume: a clean stop, then a new container on the same volume finds the directory.
smoke named-volume "$all_interfaces" reachable http -v "$volume:/data"
started=$SECONDS
docker stop "$cid" > /dev/null
((SECONDS - started < 10)) || fail "docker stop took $((SECONDS - started)) s, expected under 10 s"
code="$(docker inspect -f '{{.State.ExitCode}}' "$cid")"
[[ "$code" == "0" ]] || fail "exit code after docker stop was $code, expected 0"
docker logs "$cid" 2>&1 | grep -q "Hoard stopped." || fail "no 'Hoard stopped.' in the logs"
docker rm -f "$cid" > /dev/null
# The image's /data ownership carries into the new volume (no RUN step sets it); a marker
# written as the app user must survive recreating the container.
owner="$(docker run --rm -v "$volume:/data" --entrypoint stat "$helper" -c '%u %a' /data)"
[[ "$owner" == "$uid 700" ]] || fail "the named volume's /data is owned/moded '$owner', expected '$uid 700'"
docker run --rm --user "$uid" -v "$volume:/data" --entrypoint touch "$helper" /data/.smoke-marker
smoke named-volume-again "$all_interfaces" reachable http -v "$volume:/data"
docker logs "$cid" 2>&1 | grep -q "Data directory: /data (existing)" || fail "the recreated container did not report the existing data directory"
docker run --rm --user "$uid" -v "$volume:/data" --entrypoint test "$helper" -f /data/.smoke-marker || fail "the marker did not survive recreating the container"
docker rm -f "$cid" > /dev/null

# HTTPS with a mounted PEM pair, readable by the app user.
mkdir "$work/tls"
openssl req -x509 -newkey rsa:2048 -nodes -keyout "$work/tls/key.pem" -out "$work/tls/cert.pem" -days 2 -subj /CN=hoard-smoke 2>/dev/null
chmod 0755 "$work/tls" && chmod 0644 "$work/tls/key.pem" "$work/tls/cert.pem"
smoke https "$all_interfaces" reachable https --tmpfs "$data_tmpfs" -v "$work/tls:/tls:ro" \
  -e HOARD_LISTEN__TLS__MODE=https \
  -e HOARD_LISTEN__TLS__CERTIFICATEFILE=/tls/cert.pem \
  -e HOARD_LISTEN__TLS__KEYFILE=/tls/key.pem
ip="$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' "$cid")"
result="$(probe "http://$ip:8484/health/ready")"
[[ "${result%% *}" == "52" || "${result%% *}" == "56" ]] || fail "plain HTTP to the HTTPS port gave curl exit/status '$result', expected exit 52 or 56 (no HTTP response)"
docker logs "$cid" 2>&1 | grep -q "Hoard is listening on 0.0.0.0, which is reachable from the network" || fail "no exposure warning in the logs"
docker rm -f "$cid" > /dev/null

echo "smoke-container: all runs passed for $image"
