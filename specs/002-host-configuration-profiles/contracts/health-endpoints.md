# Contract: health endpoints

This supersedes spec 001's `GET /health` ([001 contract](../../001-pipeline-skeleton/contracts/health-endpoint.md)).

| Request | Response |
|---|---|
| `GET` or `HEAD /health/live` | `200`, `text/plain`, body `Healthy`, whenever the process serves requests. No readiness condition is consulted |
| `GET` or `HEAD /health/ready` | `200` `Healthy` once startup completed, while the data-directory write check passes and before shutdown begins; otherwise `503` `Unhealthy` |
| Other methods on `/health/live` or `/health/ready` | `405` |
| `GET /health` | `404` (removed) |
| Any other path | `404` |

- Both endpoints are unauthenticated.
- Bodies never contain configuration values, paths, versions or check details.
- In `https` mode they are served only over HTTPS on the configured port.
- Later specs add readiness conditions to `/health/ready` (database, disk); liveness stays
  condition-free.
