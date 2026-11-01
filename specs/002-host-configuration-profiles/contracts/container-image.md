# Contract: container image

`ghcr.io/stonefly-labs/hoard:<version>`. This changes spec 001's loopback-only image.

| Property | Value |
|---|---|
| User | `$APP_UID` (non-root), unchanged from spec 001 |
| Environment | `HOARD_LISTEN__ADDRESS=0.0.0.0`, `HOARD_LISTEN__TLS__MODE=proxy`, `HOARD_DATA__PATH=/data` |
| Port | `8484` (`EXPOSE 8484`) |
| Data | `/data`, owned by `$APP_UID`, mode 0700. No `VOLUME` declaration; mount a named volume there |
| Entrypoint | `/app/hoard`. Arguments after the image name are Hoard options |

**Overrides.** `-e HOARD_…` and command-line arguments override the image's values. A
mounted `hoard.json` does not, because the file ranks below the environment.

## Documented run commands

Personal use (loopback on the host):

```bash
docker run -d --name hoard -p 127.0.0.1:8484:8484 -v hoard-data:/data ghcr.io/stonefly-labs/hoard:<version>
curl http://127.0.0.1:8484/health/ready
```

The docs state:
- `-p 8484:8484` without an address publishes on every host interface, and Docker's port
  publishing bypasses host firewalls such as ufw.
- `-e` values are visible to `docker inspect`, so the certificate password is better kept
  in a mounted `hoard.json` with mode 0600. The image's own `HOARD_*` values still win
  over that file for the options they set.
A network-exposed container needs a TLS-terminating reverse proxy in front, or HTTPS mode
with a mounted certificate:

```bash
docker run -d -p 8484:8484 -v hoard-data:/data -v /etc/hoard/tls:/tls:ro \
  -e HOARD_LISTEN__TLS__MODE=https \
  -e HOARD_LISTEN__TLS__CERTIFICATEFILE=/tls/cert.pem -e HOARD_LISTEN__TLS__KEYFILE=/tls/key.pem \
  ghcr.io/stonefly-labs/hoard:<version>
```

## Smoke guarantees (`eng/smoke/smoke-container.sh`)

- **Non-root:** checked from the image config and from the running process's uids.
- **Default run:** `/health/ready` answers 200 from the bridge, the exposure warning is
  logged, and the only listening socket is port 8484.
- **Hostile framework environment:** still only port 8484.
- **Framework `--urls` argument:** startup refused.
- **`HOARD_LISTEN__ADDRESS=127.0.0.1`:** loopback only, connection refused from the bridge.
- **Named volume:** survives container recreation, and the summary reports `(existing)`.
- **`https` with a mounted PEM:** HTTPS answers from the bridge, and plain HTTP gets no
  normal response.
- **Every run:** non-root checks, and an exact listener `address:port` set.
