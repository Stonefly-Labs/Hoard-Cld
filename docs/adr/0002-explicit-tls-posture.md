# ADR-0002: Explicit TLS posture for network-exposed listeners

- **Status:** Accepted
- **Date:** 2026-10-31
- **Specs:** 002 — Host, configuration and deployment profiles; 010 — Admin authentication and management API foundation; 019 — Distribution, first run and upgrades

## Context

The charter's team profile asks for "explicit network exposure with authentication/TLS" (Charter §7),
and Architecture §8 says "team listeners require explicit TLS/auth posture". Product spec §8 allows
HTTPS "either natively or via a TLS-terminating reverse proxy" for management. No backlog spec
owned TLS: spec 002 made non-loopback binding an explicit choice with an anonymous-reads warning
but left the listener plain HTTP, and the admin token (spec 010) covers only the auth half. Without
a decision, a team exposure could quietly serve package traffic, and later the admin token, in
cleartext.

## Decision

1. **Declare it or refuse.** When Hoard listens on a non-loopback address it requires an explicit
   TLS mode. Without one, startup fails with a diagnostic naming both modes.
   - `https`: Hoard serves HTTPS only, from a configured certificate (PEM or PKCS#12/PFX, with a
     separate key file and a password where the format needs them). A missing, unreadable,
     password-mismatched, key-less or out-of-validity certificate fails startup; Hoard never falls
     back to plain HTTP. The certificate password is a secret and is never logged.
   - `proxy`: the operator declares that a TLS-terminating reverse proxy sits in front; Hoard
     serves plain HTTP on the configured address.
2. Loopback needs no mode and serves plain HTTP; either mode is accepted on loopback (e.g. a
   reverse proxy on the same host).
3. The official container image sets the all-interfaces address and mode `proxy` through Hoard's
   own options. Its documentation publishes the port to the host's loopback and explains that a
   network-exposed container needs a reverse proxy in front or mode `https` with a mounted
   certificate.
4. Spec 002 builds this. The admin token (spec 010) remains the authentication half of the team
   posture; spec 019's team guide documents both TLS routes.

## Open questions

- Correct advertised URLs behind a `proxy`-mode reverse proxy (a configured public base URL, as
  sketched in Product spec §10, or trusted forwarded headers): recorded as an open decision on
  spec 007, the first spec that advertises absolute URLs (NuGet service index). Without it, a
  service index behind a TLS proxy would advertise `http://` URLs. X-04 keeps the broader
  hardening of trusted-proxy header handling.

## Alternatives considered

- **Plain HTTP for MVP, reverse-proxy TLS documented, native TLS post-MVP.** Simplest, but the
  "explicit posture" would be documentation only; an operator could expose Hoard in cleartext
  without ever deciding to.
- **Native HTTPS with a warning when absent.** Offers TLS but still lets exposure happen silently
  in cleartext.
- **Native HTTPS required.** Breaks the container's Docker-port-publishing model and the common
  reverse-proxy deployment.
- **Proxy-only declaration.** Leaves teams without a proxy no way to get TLS.

## Consequences

- Spec 002 gains a TLS mode option and certificate options; the backlog's 002 acceptance focus and
  prompt are amended accordingly.
- `proxy` is a declaration Hoard cannot verify; the guarantee is that exposure is always a
  deliberate, logged TLS decision, not that a proxy exists.
- Not included: automatic certificates (ACME), certificate hot-reload (replacing a certificate
  needs a restart), client certificates, multiple listeners.
