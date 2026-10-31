# Contract: Hoard host HTTP surface (spec 001)

| | |
|---|---|
| Listener | `http://localhost:8484` (`127.0.0.1` and `::1` when available). No non-loopback listener in any distribution. Not configurable in this spec; configurable binding is delivered by spec 002 |
| Auth | None (loopback only; there is no management surface) |

## `GET /health`

Liveness only (readiness is spec 002).

| Case | Status | Content-Type | Body |
|---|---|---|---|
| Host running | `200` | `text/plain` | `Healthy` |

## Any other path or method

| Case | Status |
|---|---|
| Any path other than `/health` | `404` |

## Non-goals

No version or diagnostic payload, no OpenAPI document, no static files. These arrive with
the specs that need them.
