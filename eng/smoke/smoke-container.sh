#!/usr/bin/env bash
# Smoke-tests a Hoard container image: non-root user; healthy on 127.0.0.1:8484 inside
# the container within 30 s, 404 elsewhere; every listening socket on loopback; and
# unreachable (connection refused) from a neighbouring container. Runs once with the base
# image environment, once with a hostile environment and once with a hostile command line,
# both asking for wider bindings.
#
#   eng/smoke/smoke-container.sh <image>
set -euo pipefail

[[ $# -eq 1 ]] || { echo "usage: $0 <image>" >&2; exit 2; }
image="$1"
# Multi-arch index digest, so the same script runs on amd64 and arm64 runners.
helper="curlimages/curl:8.23.0@sha256:3fb7e13e5d179a845c97d78204f3f1064d1907037f26a3ebf91710571151fd93"
network="hoard-smoke-$$"
containers=()
run_name="image"

cleanup() {
  for c in "${containers[@]}"; do docker rm -f "$c" > /dev/null 2>&1 || true; done
  docker network rm "$network" > /dev/null 2>&1 || true
}
trap cleanup EXIT

fail() {
  echo "smoke-container: FAIL [$run_name]: $*" >&2
  if [[ -n "${cid:-}" ]]; then
    echo "--- container logs (last 40 lines) ---" >&2
    docker logs --tail 40 "$cid" >&2 2>&1 || true
  fi
  exit 1
}

user="$(docker image inspect -f '{{.Config.User}}' "$image")"
[[ -n "$user" && "${user%%:*}" != "root" && "${user%%:*}" != "0" ]] || fail "image user is '${user:-<unset>}', expected a non-root user"
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

# Runs inside the Hoard container's network namespace: health, 404, then the socket tables.
inside_script='
deadline=$(( $(date +%s) + 30 )); status=""
while [ "$(date +%s)" -lt "$deadline" ]; do
  status=$(curl -s -o /tmp/body -w "%{http_code}" --max-time 2 http://127.0.0.1:8484/health || true)
  [ "$status" = "200" ] && break
  sleep 0.5
done
echo "health=$status body=$(cat /tmp/body 2>/dev/null)"
echo "other=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 http://127.0.0.1:8484/anything-else || true)"
cat /proc/net/tcp /proc/net/tcp6 2>/dev/null | awk "\$4 == \"0A\" { print \"listen=\" \$2 }"
'

# Loopback in /proc/net/tcp* hex (little-endian words): 127.0.0.0/8, ::1, ::ffff:127.x.
is_loopback() {
  local address="${1%:*}"
  case "${#address}" in
    8) [[ "${address:6:2}" == "7F" ]] ;;
    32) [[ "$address" == "00000000000000000000000001000000" || "$address" == 0000000000000000FFFF0000??????7F ]] ;;
    *) return 1 ;;
  esac
}

# smoke <run-name> [docker run options...] [-- image arguments...]
smoke() {
  run_name="$1"
  shift
  local options=() arguments=()
  while [[ $# -gt 0 && "$1" != "--" ]]; do options+=("$1"); shift; done
  [[ $# -gt 0 ]] && shift
  arguments=("$@")
  cid="$(docker run -d --network "$network" --read-only --cap-drop ALL \
    --security-opt no-new-privileges ${options[@]+"${options[@]}"} "$image" ${arguments[@]+"${arguments[@]}"})"
  containers+=("$cid")

  local report
  report="$(docker run --rm --network "container:$cid" --entrypoint sh "$helper" -c "$inside_script")"
  grep -qx 'health=200 body=Healthy' <<< "$report" || fail "inside view: expected 200 Healthy within 30 s, got: $(head -n 1 <<< "$report")"
  grep -qx 'other=404' <<< "$report" || fail "inside view: GET /anything-else: $(sed -n 2p <<< "$report"), expected 404"

  local listeners
  listeners="$(sed -n 's/^listen=//p' <<< "$report")"
  [[ -n "$listeners" ]] || fail "inside view: no listening sockets in the container"
  while read -r address; do
    is_loopback "$address" || fail "inside view: non-loopback listener $address (/proc/net/tcp hex)"
  done <<< "$listeners"

  local ip probe_exit=0
  ip="$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' "$cid")"
  docker run --rm --network "$network" "$helper" -s -o /dev/null --max-time 5 "http://$ip:8484/health" || probe_exit=$?
  [[ "$probe_exit" == "7" ]] || fail "outside view: $ip:8484 from the bridge gave curl exit $probe_exit, expected 7 (connection refused)"

  docker rm -f "$cid" > /dev/null
  echo "smoke-container: PASS [$run_name] listeners: $(paste -sd ' ' - <<< "$listeners")"
}

smoke base-environment

smoke hostile-environment \
  -e ASPNETCORE_URLS="http://0.0.0.0:8080" \
  -e ASPNETCORE_HTTP_PORTS="8081" \
  -e ASPNETCORE_HTTPS_PORTS="8443" \
  -e DOTNET_URLS="http://0.0.0.0:8082" \
  -e ASPNETCORE_PREFERHOSTINGURLS="true" \
  -e Kestrel__Endpoints__H__Url="http://0.0.0.0:8485"

smoke hostile-command-line -- --urls "http://0.0.0.0:8086"

echo "smoke-container: all runs passed for $image"
