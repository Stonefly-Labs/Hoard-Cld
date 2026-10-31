# Feature Specification: Host, configuration and deployment profiles

**Feature Branch**: `feature/002-host-configuration-profiles`

**Created**: 2026-10-31

**Status**: Draft

**Input**: User description: "As a developer or operator, I want to start Hoard as a single process or container with a persistent data directory and get clear feedback about whether it started correctly. Configuration comes from command line over environment over file. Specify safe defaults: loopback only unless binding to other addresses is explicitly configured, with a clear warning that package reads are then anonymous; the official container image explicitly binds all interfaces inside the container so Docker port publishing works, and its documentation publishes the port to the host's loopback by default. Malformed or unknown options fail fast with actionable diagnostics and never fall back to insecure behavior. Distinguish liveness from readiness, define restart semantics and clean shutdown, and never log secrets. Include first-run, restart, invalid-config, explicit network-exposure and container scenarios. Out of scope for this spec: module registration or enable/disable machinery (no modules exist yet; it arrives with the first real module), storage and the management API."

## Clarifications

### Session 2026-10-31

- Q: What format should Hoard's configuration file use, and should Hoard look for a file automatically when none is named? → A: JSON; Hoard reads `hoard.json` from the working directory if it exists, or a file named explicitly with `--config`.
- Q: How should Hoard's options be named in environment variables, on the command line and in the file, and how are non-Hoard settings handled? → A: One mechanical mapping — file `{"listen": {"port": 8484}}` ↔ env `HOARD_LISTEN__PORT` ↔ CLI `--listen:port 8484`; every `HOARD_*` variable, every `--` argument and every file key must be a known option or startup fails; other environment variables are not Hoard's and are ignored.
- Q: Where should the native executable keep its data by default when no data directory is configured? → A: `./data`, relative to the working directory, resolved to an absolute path at startup.
- Q: What paths should the liveness and readiness endpoints use, and what happens to spec 001's `GET /health`? → A: `/health/live` and `/health/ready`; `/health` is removed and returns 404.
- Q: How should a team deployment get TLS, given this spec ships plain HTTP? → A: Plain HTTP for MVP; team TLS comes from a TLS-terminating reverse proxy that the docs explain; native TLS is post-MVP. To be recorded in an ADR aligning Architecture §8 and Charter §7.

## User Scenarios & Testing *(mandatory)*

Actors: a **personal user** (runs Hoard on their own machine, as an executable or container), and
an **operator** (runs Hoard for a team and deliberately exposes it on the network). Both read
Hoard's startup output and probe its health endpoints; a container orchestrator or supervisor
probes them too.

### User Story 1 - First run with safe defaults (Priority: P1)

A personal user downloads the executable and starts it with no configuration at all. Hoard
starts, creates its data directory, listens only on loopback, and prints a short startup summary:
version, the address it listens on, the data directory, and which configuration sources it used.
The health endpoints report that it is alive and ready.

**Why this priority**: This is the default experience for every new user and the security
baseline (Constitution III: loopback by default). Everything else builds on a host that starts
predictably.

**Independent Test**: Start the executable in an empty working directory with no configuration;
check the startup summary, the created data directory, the listening sockets, and the liveness
and readiness responses.

**Acceptance Scenarios**:

1. **Given** no configuration file, no Hoard environment variables and no arguments, **When**
   Hoard starts, **Then** it listens on loopback port 8484 only and on no other interface.
2. **Given** the data directory does not exist, **When** Hoard starts, **Then** it creates it and
   the startup summary states its full path and that it was created.
3. **Given** Hoard has started, **When** the liveness and readiness endpoints are requested,
   **Then** both report healthy.
4. **Given** environment variables or arguments meant for the underlying web framework (not
   Hoard's own options) ask for wider bindings, **When** Hoard starts, **Then** they are ignored
   and Hoard still listens on loopback only, as it did in spec 001.

---

### User Story 2 - Invalid configuration fails fast and clearly (Priority: P1)

An operator makes a mistake: a misspelled option, a port that is not a number, an address that
does not parse, a data directory that cannot be written. Hoard refuses to start, exits with a
non-zero code, and prints a message naming each problem, where it came from (file, environment
variable or command-line argument) and what a valid value looks like. It never starts on a
fallback such as "all interfaces" or a different directory.

**Why this priority**: Silent fallback is how a proxy ends up exposed or writing data where no
one expects it. The docs require invalid options to fail startup (Product spec §10, Architecture
§5 step 4: no silent fallback).

**Independent Test**: Start Hoard once per kind of invalid input and check the exit code, that no
port was opened, and that the message names the option, its source and the fix.

**Acceptance Scenarios**:

1. **Given** an unknown option in any source (e.g. a misspelled listen option), **When** Hoard
   starts, **Then** it exits non-zero without opening a port and names the unknown option and its
   source.
2. **Given** a malformed value (non-numeric or out-of-range port, unparseable address, malformed
   configuration file), **When** Hoard starts, **Then** it exits non-zero, names the option and
   source, and states the expected form.
3. **Given** several problems at once, **When** Hoard starts, **Then** all of them are reported in
   one run, not one per attempt.
4. **Given** the configured data directory exists but is not writable, or its path is a file,
   **When** Hoard starts, **Then** it exits non-zero naming the path and the problem.
5. **Given** a configuration file path is given explicitly and the file does not exist, **When**
   Hoard starts, **Then** it exits non-zero naming the missing file.
6. **Given** the configured port is already in use, **When** Hoard starts, **Then** it exits
   non-zero with a message naming the address and port.

---

### User Story 3 - Explicit network exposure with a warning (Priority: P1)

An operator wants teammates to reach Hoard and configures it to listen on all interfaces (or a
specific non-loopback address). Hoard does so, and its startup output carries a prominent warning
that it is reachable from the network and that package reads are anonymous.

**Why this priority**: The team profile depends on deliberate exposure, and the docs require the
warning (Product spec §8; backlog 002 acceptance focus).

**Independent Test**: Start Hoard with a non-loopback listen address; confirm it is reachable on
that address and the warning appears exactly when the address is not loopback.

**Acceptance Scenarios**:

1. **Given** a listen address of all interfaces, set through any of the three configuration
   sources, **When** Hoard starts, **Then** it listens on that address and logs a warning that it
   is network-exposed and package reads are anonymous.
2. **Given** a specific non-loopback address, **When** Hoard starts, **Then** the same warning is
   logged.
3. **Given** a loopback address (IPv4 or IPv6), **When** Hoard starts, **Then** no exposure
   warning is logged.
4. **Given** the same option set in the file, the environment and on the command line with
   different values, **When** Hoard starts, **Then** the command-line value wins over the
   environment, which wins over the file, and the startup summary shows the effective value and
   its source.

---

### User Story 4 - Container runs and is reachable through port publishing (Priority: P2)

A personal user runs the official image with a persistent volume and publishes the port to their
machine's loopback (`-p 127.0.0.1:8484:8484`), as the documentation shows. Hoard is reachable
from their machine at `http://127.0.0.1:8484`. Inside the container Hoard listens on all
interfaces, and the startup output carries the exposure warning, which the documentation explains.

**Why this priority**: The container is a primary distribution (Charter §7). Spec 001's image is
not reachable from the host; this makes it usable.

**Independent Test**: Run the image with the documented command and a named volume; request the
health endpoints from the host; restart the container and confirm the data directory persisted.

**Acceptance Scenarios**:

1. **Given** the official image, **When** it starts without extra configuration, **Then** Hoard
   listens on all interfaces inside the container on port 8484 and logs the exposure warning.
2. **Given** the documented run command publishing to the host's loopback, **When** the user
   requests the health endpoints from the host, **Then** they answer.
3. **Given** a volume mounted at the image's data directory, **When** the container is removed
   and recreated with the same volume, **Then** Hoard reuses the existing data directory.
4. **Given** the container's listen address is overridden with Hoard's own option (e.g. back to
   loopback), **When** it starts, **Then** the override wins, since the image's binding is an
   ordinary configured value.
5. **Given** the image, **When** it runs, **Then** Hoard runs as a non-root user (unchanged from
   spec 001) and the data directory is writable by that user.

---

### User Story 5 - Liveness, readiness, restart and clean shutdown (Priority: P2)

A supervisor or orchestrator probes Hoard to decide whether to restart it (liveness) and whether
to send it traffic (readiness). When asked to stop, Hoard stops accepting new requests, lets
in-flight requests finish within a bounded time, and exits cleanly. Restarting it with the same
configuration and data directory brings it back to the same state.

**Why this priority**: Operators and container platforms need the two signals to be distinct and
honest (Architecture §5 steps 6 and 8). The distinction is small now but every later spec adds
readiness conditions.

**Independent Test**: Probe both endpoints during startup, steady state and shutdown; send a stop
signal with a request in flight; restart and compare.

**Acceptance Scenarios**:

1. **Given** Hoard is running, **When** liveness is requested, **Then** it answers healthy without
   depending on any readiness condition.
2. **Given** Hoard is running and its data directory is usable, **When** readiness is requested,
   **Then** it answers ready; **Given** the data directory stops being usable after startup,
   **Then** readiness answers not ready while liveness still answers healthy.
3. **Given** Hoard receives a stop signal (Ctrl+C, SIGTERM, `docker stop`), **When** it shuts
   down, **Then** readiness reports not ready, new requests are refused, in-flight requests
   complete within the shutdown timeout, it logs that it stopped, and it exits with code 0.
4. **Given** Hoard was stopped cleanly, **When** it starts again with the same configuration and
   data directory, **Then** it starts without error, reports the data directory as existing (not
   created), and answers on the same address.
5. **Given** Hoard was killed abruptly, **When** it starts again, **Then** it starts normally;
   nothing in this spec leaves state that needs recovery.

---

### Edge Cases

- The data directory is given as a relative path: it is resolved once at startup, and the
  summary shows the absolute path, so a restart from a different working directory is visibly
  different rather than silently using a different directory.
- The data directory's parent does not exist: Hoard creates the full path, or fails with the
  path named if it cannot.
- An empty value for an option (e.g. empty listen address) is malformed, not "use the default".
- A port of 0 (pick any free port) is rejected: Hoard's address must be predictable for clients.
- A host name instead of an IP address as the listen address (other than `localhost`) is
  rejected with the expected form, so the exposure decision never depends on name resolution.
- `localhost` as the listen address means loopback only and gets no warning.
- A configuration file that is valid but empty is equivalent to no file.
- A malformed `hoard.json` found automatically in the working directory fails startup like a
  named one; it is never skipped.
- Options for later specs (storage, registries, management) are unknown in this spec and fail
  startup like any other unknown option; each later spec adds its options when it consumes them.
- Two Hoard processes started with the same port: the second fails per US2 scenario 6. Two
  processes sharing one data directory on different ports are not prevented in this spec, which
  stores nothing there yet.
- A stop signal arrives during startup: Hoard exits without serving and without a crash trace.
- A configuration value that holds a secret would never be echoed back; no option in this spec
  is a secret, and diagnostics never print the full environment or command line.

## Requirements *(mandatory)*

### Functional Requirements

**Configuration**

- **FR-001**: Hoard MUST read its options from three sources with precedence command line >
  environment > configuration file (Product spec §10), and MUST show each effective option's
  source in the startup summary.
- **FR-002**: The options in this spec are only those with a consumer here: the listen endpoint
  (address and port, named per FR-008), the data
  directory, and the configuration file location. No other Hoard
  option exists yet (Constitution V).
- **FR-003**: Only Hoard's own options MAY change the listener. Framework-level settings from any
  source (e.g. web-framework URL or endpoint variables) MUST NOT add, widen or change a listener,
  preserving spec 001's guarantee.
- **FR-004**: Unknown Hoard options MUST fail startup: any `HOARD_*` environment variable, any
  `--` command-line argument and any configuration file key that is not an option defined in this
  spec. Environment variables without the `HOARD_` prefix are not Hoard options and are ignored;
  per FR-003 they can never change the listener.
- **FR-005**: Malformed values, a malformed configuration file, or an explicitly named but missing
  configuration file MUST fail startup.
- **FR-006**: Every startup failure MUST exit with a non-zero code before opening any port, and
  MUST report every detected problem with the option name, the source, and the expected form or
  fix, in plain language rather than only a stack trace.
- **FR-007**: Hoard MUST NOT fall back to a different listen address, port or data directory when
  the configured one is invalid or unusable (Architecture §5 step 4).
- **FR-008**: The configuration file is JSON. When no file is named, Hoard reads `hoard.json`
  from the working directory if it exists and runs without a file if it does not. A file named
  explicitly (`--config <path>`) replaces that lookup and MUST exist (FR-005). An automatically
  found `hoard.json` is validated exactly like a named one, and the startup summary names the file
  read, or states that none was read. Option names map mechanically across the three sources: a
  nested file key (e.g. `{"listen": {"port": 8484}}`) is the environment variable `HOARD_` plus
  the key path joined by `__` (`HOARD_LISTEN__PORT`) and the command-line argument `--` plus the
  path joined by `:` (`--listen:port 8484`). The file location option itself is `--config` /
  `HOARD_CONFIG`.

**Binding and profiles**

- **FR-009**: With no listen address configured, Hoard MUST listen on loopback only, port 8484
  (Constitution III; Product spec §8).
- **FR-010**: Hoard MUST listen on a non-loopback address only when one is explicitly configured,
  and MUST then log a warning at startup that it is network-exposed and that package reads are
  anonymous (Product spec §8). The warning MUST NOT appear for loopback addresses.
- **FR-011**: The listen address MUST be an IP address (IPv4 or IPv6), an all-interfaces
  address (`0.0.0.0` for IPv4, `::` for IPv6 and IPv4 where the platform supports dual-stack), or
  `localhost`; any other value is malformed.
- **FR-012**: The official container image MUST configure Hoard to listen on all interfaces inside
  the container using Hoard's own listen option, so host exposure is governed by Docker port
  publishing (Product spec §8). An operator MUST be able to override it with any configuration
  source that ranks at or above the one the image uses, and the documentation MUST say which
  sources those are.
- **FR-013**: Documentation for the container MUST publish the port to the host's loopback by
  default (`-p 127.0.0.1:8484:8484`) and explain that publishing without an address exposes Hoard
  on every host interface.

**Data directory**

- **FR-014**: Hoard MUST use a single configurable data directory, creating it (and missing
  parents) on first run and reusing it on later runs. Its default location for the executable is
  `./data` relative to the working directory, resolved once to an absolute path at startup (see
  Edge Cases).
- **FR-015**: The container image MUST use a fixed data directory intended for a mounted volume,
  owned by and writable for the image's non-root user, and the documentation MUST show mounting a
  volume there.
- **FR-016**: At startup Hoard MUST verify the data directory can be written, and fail startup
  (FR-006) if not. It MUST NOT create anything in the data directory beyond what this check needs;
  contents arrive with storage specs 003 and 004.

**Health**

- **FR-017**: Hoard MUST expose a liveness endpoint that reports healthy whenever the process can
  serve requests, independent of readiness conditions.
- **FR-018**: Hoard MUST expose a readiness endpoint that reports ready only after startup has
  completed and while the data directory is usable, and reports not ready once shutdown begins.
  The data-directory check runs when readiness is requested (a cheap write test, no background
  job). Later specs add their own readiness conditions (Product spec §12: disk and database
  readiness).
- **FR-019**: Liveness is `GET /health/live` and readiness is `GET /health/ready`, both
  unauthenticated. Spec 001's `GET /health` is removed and returns 404 like any other unknown
  path. Health responses MUST NOT reveal configuration values, paths or versions.
- **FR-020**: Every other path MUST still return 404 (no other endpoints in this spec).

**Startup feedback, shutdown and logging**

- **FR-021**: On successful start Hoard MUST log a concise summary: version, effective listen
  address and port, absolute data directory and whether it was created, and the configuration
  sources used (including the file path, if any).
- **FR-022**: On a stop signal Hoard MUST report not ready, stop accepting new requests, allow
  in-flight requests to complete within a bounded shutdown timeout, log that it stopped, and exit
  with code 0. The timeout MUST fit inside a container platform's default stop grace period.
- **FR-023**: A restart with the same configuration and data directory MUST produce the same
  listen address and data directory; nothing in this spec requires recovery after an abrupt kill.
- **FR-024**: Hoard MUST NEVER log secrets (Constitution III): diagnostics and the summary MUST NOT
  print the full environment, the full command line, or the raw configuration file, and only
  report the values of options defined in FR-002.

**Verification and documentation**

- **FR-025**: The native-executable smoke test MUST keep proving loopback-only by default and
  under hostile framework settings (FR-003), and MUST add an explicit-exposure run showing the
  warning. The container smoke test MUST change from "unreachable from a neighbouring container"
  to "reachable through the bridge on 8484 with the exposure warning logged", MUST keep the
  non-root checks, and MUST keep proving that framework settings add no listener or port beyond
  the configured one.
- **FR-026**: The README, `docs/DEVELOPMENT.md`, the smoke-script descriptions in `CLAUDE.md`'s
  Commands section, and the release notes' working/planned section
  MUST describe the shipped options, defaults, precedence, health endpoints, the container run
  command with loopback port publishing and a volume, and the exposure warning; the README MUST
  stop saying the container is unreachable from the host.

### Key Entities

- **Hoard options**: listen address, listen port, data directory, configuration file location;
  each with an effective value and the source it came from.
- **Data directory**: one persistent directory per Hoard instance; empty in this spec apart from
  the write check.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With no configuration, the executable and the container (inside) each start and
  answer liveness and readiness within 30 seconds, and the executable listens on loopback only:
  verified on every PR by the smoke tests.
- **SC-002**: Each invalid-input case in US2 (unknown option per source, malformed value per kind,
  malformed file, missing named file, unusable data directory, port in use) exits non-zero
  without opening a port, and its message names the option or path and the source, in 100% of
  cases covered by tests.
- **SC-003**: Across all tested precedence combinations (each option set in two or three sources),
  the effective value always comes from the highest-precedence source.
- **SC-004**: The exposure warning appears in 100% of tested non-loopback runs and 0% of loopback
  runs.
- **SC-005**: A user following the README's container instructions reaches Hoard's health
  endpoint from their machine on the first attempt, with the port published to loopback only.
- **SC-006**: On a stop signal Hoard exits with code 0 within 10 seconds while an in-flight
  request completes, and a restart reuses the same data directory.
- **SC-007**: No test run's captured output contains a value planted as a secret in the
  environment or on the command line.

## Assumptions

- Hoard speaks plain HTTP, and does so for the whole MVP (Clarifications 2026-10-31). A team
  deployment that needs HTTPS puts a TLS-terminating reverse proxy in front of Hoard; native TLS
  is post-MVP. This decision is recorded in an ADR that aligns Architecture §8 ("team listeners
  require explicit TLS/auth posture") and Charter §7; the admin token (spec 010) covers the auth
  half. This spec's documentation only states that Hoard serves plain HTTP; reverse-proxy
  guidance belongs with the network-exposure docs once management exists.
- One listen address and one port per instance; listening on several addresses at once is not
  needed for this spec's acceptance.
- No management surface exists, so network exposure does not yet require the admin token; the
  token and management protection arrive with spec 010.
- The exposure warning is logged inside the container too; the documentation explains that host
  exposure is decided by Docker port publishing.
- Options for later specs (module selection 007, storage 003/004, registries 005, management
  010) are added by those specs; until then they are unknown options.
- Out of scope, owned by later specs: module registration and enable/disable (007), metadata and
  artifact storage (003, 004), the management API and admin token (010).
