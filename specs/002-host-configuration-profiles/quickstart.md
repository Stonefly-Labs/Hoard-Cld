# Quickstart: validating spec 002

Expected outputs are defined in [contracts/](contracts/); this guide only says how to
exercise them.

## Prerequisites

- .NET SDK per `global.json`, Docker with Buildx, `curl`, `openssl`, `lsof` (Linux/macOS).
- Build a local executable and image as in `docs/DEVELOPMENT.md`.

## 1. Automated checks

```bash
dotnet build Hoard.slnx
dotnet test --solution Hoard.slnx
eng/smoke/smoke-binary.sh <exe>
eng/smoke/smoke-container.sh <image>
```

All must pass. PR CI runs the same checks, and the release dry run repeats the smoke tests
on every native runner.

## 2. First run (US1)

```bash
mkdir /tmp/hoard-try && cd /tmp/hoard-try && <exe>
curl -i http://127.0.0.1:8484/health/live     # 200 Healthy
curl -i http://127.0.0.1:8484/health/ready    # 200 Healthy
curl -i http://127.0.0.1:8484/health          # 404
```

The summary shows `/tmp/hoard-try/data (created)` and `Config file: none`. Stop with
Ctrl+C: `Hoard stopped.` and exit code 0. Run again: `(existing)`.

## 3. Invalid configuration (US2)

Each command exits 2 without opening a port, and names the option and source:

```bash
<exe> --listen:prot 9000
HOARD_LISTEN__PORT=abc <exe>
<exe> --listen:address example.com
echo '{ "listen": ' > hoard.json && <exe>; rm hoard.json
<exe> --config missing.json
<exe> --urls http://0.0.0.0:8086
touch state && <exe> --listen:port 0 --listen:address 10.0.0.300 --data:path state   # three problems, one report
```

Port in use: start one instance, then a second → exit 1 naming `127.0.0.1:8484`.

## 4. Exposure and TLS (US3)

```bash
<exe> --listen:address 0.0.0.0                                # exit 2, names https and proxy
<exe> --listen:address 0.0.0.0 --listen:tls:mode proxy        # serves; exposure + proxy warning
openssl req -x509 -newkey rsa:2048 -nodes -keyout key.pem -out cert.pem -days 1 -subj /CN=localhost
<exe> --listen:tls:mode https --listen:tls:certificateFile cert.pem --listen:tls:keyFile key.pem
curl -k https://127.0.0.1:8484/health/ready                   # 200
curl http://127.0.0.1:8484/health/ready                       # no normal response
<exe> --listen:tls:mode proxy --listen:tls:certificateFile cert.pem   # exit 2, contradiction
```

Precedence: put `{"listen":{"port":9001}}` in `hoard.json`, set `HOARD_LISTEN__PORT=9002`
and pass `--listen:port 9003`. Hoard listens on 9003, and the summary names the command
line as the source.

## 5. Container (US4)

Follow [contracts/container-image.md](contracts/container-image.md):

1. Run the personal command and `curl` `/health/ready` from the host.
2. Run `docker logs hoard` and check for the exposure warning.
3. Recreate the container with the same volume and check for `(existing)`.

## 6. Shutdown (US5)

```bash
<exe> & sleep 2; kill -TERM $!; wait $!; echo $?    # 0 within 10 s, "Hoard stopped."
```

The drain of in-flight requests is covered by the in-process test (research R12).
