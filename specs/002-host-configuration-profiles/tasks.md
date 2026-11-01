---
description: "Task list for spec 002 — Host, configuration and deployment profiles"
---

# Tasks: Host, configuration and deployment profiles

**Input**: Design documents from `specs/002-host-configuration-profiles/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: Required. Constitution VII (test-first) applies to the option reader and validator,
certificate checks, the data directory and health behavior. In each story, the test tasks come
first and MUST fail before the matching implementation task starts. Packaged artifacts are proven
by the smoke scripts, which also cover the refusal paths. Tests cover only what this spec changes.

**Organization**: grouped by user story in priority order (US1–US3 are P1, US4–US5 are P2).
US2 and US3 extend the reader and validator that US1 creates, so they run after it (see Dependencies).

**Out of scope** (spec): module registration or enable/disable (007), storage (003/004), the
management API and admin token (010). No option, interface or project beyond the eight options in
[data-model.md](data-model.md).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US5 from spec.md
- Paths are relative to the repository root. `Startup/` = `src/Hoard.Host/Startup/`; `Tests/` = `tests/Hoard.Host.Tests/`

---

## Phase 1: Setup (shared infrastructure)

**Purpose**: test-project wiring the plan needs (research R12).

- [ ] T001 Remove `Microsoft.AspNetCore.Mvc.Testing` from `tests/Hoard.Host.Tests/Hoard.Host.Tests.csproj` and from `Directory.Packages.props` (R12). Delete `tests/Hoard.Host.Tests/HealthEndpointTests.cs`; its cases are replaced in US1
- [ ] T002 [P] Grant `InternalsVisibleTo` `Hoard.Host.Tests` in `src/Hoard.Host/Hoard.Host.csproj`. Host types stay `internal` (R12)
- [ ] T003 [P] In `tests/Hoard.Host.Tests/Hoard.Host.Tests.csproj` (or a `HostPaths.targets` beside it, following `tests/Hoard.Architecture.Tests/CoreProjectPaths.targets`), resolve `Hoard.Host.dll` with MSBuild `GetTargetPath` and embed it as `AssemblyMetadata("HoardHostPath", …)` (R12)
- [ ] T004 [P] Create `Tests/Support/FreePort.cs` (a free loopback TCP port), `Tests/Support/TestCertificates.cs` (built in the test with `CertificateRequest`: self-signed PEM cert+key in one file, PEM with separate key file, PEM with encrypted key, PFX with password, expired, not yet valid, certificate without private key, all written to a temp directory) and `Tests/Support/HostProcess.cs` (runs `dotnet <HoardHostPath>` with an **empty** environment plus given variables, a temp working directory and given args; captures stdout/stderr and exit code; waits for a line or for exit; sends SIGTERM on Unix; kills on dispose)
- [ ] T005 Confirm `dotnet build Hoard.slnx` succeeds with zero warnings. `HealthEndpointTests.cs` is gone, so `dotnet test` on the host project may have no tests yet

**Checkpoint**: the test project builds without Mvc.Testing and can reach host internals and the host DLL.

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: the shared option vocabulary every story reads and validates. No behavior yet.

- [ ] T006 Create `Startup/OptionNames.cs`: the known-option table from [data-model.md](data-model.md), one entry per option with file key path, environment name, command-line name (none for the password), default and secret flag. Exactly these: `listen.address` (default `localhost`), `listen.port` (default `8484`), `listen.tls.mode` (unset), `listen.tls.certificateFile` (unset), `listen.tls.keyFile` (unset), `listen.tls.password` (unset, **secret**, no command-line form), `data.path` (default `./data`), and the config location `--config` / `HOARD_CONFIG` (not a file key). Names match ignoring case (R3). `OptionReader` takes this table as input (R2 growth path)
- [ ] T007 [P] Create `Startup/OptionSource.cs` (`default` | `file <absolute path>` | `environment <NAME>` | `command line <--name>`, each rendering the name in that source's form) and `Startup/ConfigProblem.cs` (source, option name in source form, what is wrong, expected form or fix; never holds a value for unknown or secret options) (data-model "Option value")
- [ ] T008 [P] Create `Startup/HoardOptions.cs`: `Listen` (kind `Localhost` | `Address(IPAddress)`, port, `IsLoopback`), `Tls` (`None` | `Proxy` | `Https(X509Certificate2)`), `DataDirectory` (absolute path, `Created`), `Sources` (effective option → value + source; password only as `(set)`), `ConfigFile` (absolute path or none) (data-model "Validated options")

**Checkpoint**: the option vocabulary exists; user stories can start.

---

## Phase 3: User Story 1 — First run with safe defaults (Priority: P1) 🎯 MVP

**Goal**: with no configuration, Hoard creates `./data`, listens on loopback 8484 only, prints the summary, and answers `/health/live` and `/health/ready`; framework settings change nothing.

**Independent test**: start the host in an empty temp directory with an empty environment; check the summary, the created data directory, the listener and both health endpoints.

### Tests for User Story 1 (write first; they MUST fail)

- [ ] T009 [P] [US1] `Tests/Startup/OptionReaderDefaultsTests.cs`: empty args, empty environment and no `hoard.json` give every default with source `default`; non-`HOARD_` variables (`ASPNETCORE_URLS`, `DOTNET_URLS`, `Kestrel__Endpoints__X__Url`, `PATH`) are ignored; `--urls http://0.0.0.0:1` is an unknown command-line option; a `hoard.json` present in the working directory is read and named; an empty file and `{}` equal no file (spec Edge Cases)
- [ ] T010 [P] [US1] `Tests/Startup/DataDirectoryTests.cs`: a relative path resolves against the given working directory to an absolute path; a missing directory and missing parents are created and `Created` is true; an existing directory gives `Created` false and its mode is not changed; on Unix a new directory has mode 0700; the write check creates `.hoard-write-check-*` with `CreateNew` and leaves nothing behind (FR-014, FR-016, R6)
- [ ] T011 [P] [US1] `Tests/Features/Health/HealthEndpointTests.cs` (in-process, real Kestrel on a `FreePort` loopback port, built with `HoardApp.Build(options)`): `GET` and `HEAD /health/live` → 200 `text/plain` `Healthy`; `/health/ready` → 200 `Healthy` after start; `GET /health` → 404; `GET /anything` → 404; `POST /health/live` and `POST /health/ready` → 405; bodies contain no path, version or option value (FR-017–FR-020, contracts/health-endpoints.md)
- [ ] T012 [P] [US1] `Tests/Startup/HoardAppTests.cs`: the built app has **no** configuration sources and exactly one Kestrel endpoint, so a move to a framework-configured builder is caught (R1); `localhost` binds 127.0.0.1 and, where available, ::1 on the configured port and nothing else
- [ ] T013 [P] [US1] `Tests/Process/FirstRunTests.cs` (`HostProcess`): in an empty temp directory with an empty environment, the host logs the summary (version present, `http://localhost:8484` with bound addresses, `<tmp>/data (created)`, each option with source `default`, `Config file: none`) and creates `<tmp>/data`; a second run reports `(existing)`; with `ASPNETCORE_URLS=http://0.0.0.0:<port>`, `Kestrel__Endpoints__X__Url` and an `appsettings.json` asking for `0.0.0.0` the listener is still loopback only; `--urls http://0.0.0.0:<port>` exits 2 naming `--urls` (US1 sc.1–4, FR-003, FR-004, FR-021). One test proves the default port 8484 (in a non-parallel test collection); the others pass a `FreePort` via `--listen:port` so they don't collide

### Implementation for User Story 1

- [ ] T014 [US1] Create `Startup/OptionReader.cs` for the happy path: command line `--name value` and `--name=value` (split at the first `=`); every `HOARD_*` variable ignoring case, all others ignored; the file from `--config`/`HOARD_CONFIG` or `./hoard.json` if it exists, parsed with `JsonDocument` (comments allowed, trailing commas rejected), nested objects as key paths, empty file or `{}` = no file; output `(option, raw value, source)` rows plus read problems. Unknown names in any source are problems naming the source and name, never the value (R2, R3)
- [ ] T015 [US1] Create `Startup/ListenAddress.cs` (default `localhost` only for now; US2 adds parsing) and `Startup/DataDirectory.cs`: `Path.GetFullPath(value, workingDirectory)`, `Directory.CreateDirectory` recording whether it existed, Unix mode 0700 for a new directory (`UnixFileMode.UserRead | UserWrite | UserExecute`), and `WriteCheck()` creating `.hoard-write-check-<Path.GetRandomFileName()>` with `FileMode.CreateNew` and `FileOptions.DeleteOnClose`, writing one byte (R6)
- [ ] T016 [US1] Create `Startup/OptionValidator.cs`: rows → `HoardOptions` or a list of `ConfigProblem`, applying precedence command line > environment > file > default (FR-001). Preconditions (data-directory creation) run only when rules 1–3 found nothing, so a refused start leaves no directory (data-model rule 4)
- [ ] T017 [P] [US1] Create `src/Hoard.Host/Features/Health/StartupCheck.cs` (`IHealthCheck`, healthy between `ApplicationStarted` and `ApplicationStopping`) and `src/Hoard.Host/Features/Health/HealthEndpoints.cs` (`AddHealth`: health checks with `StartupCheck` tagged `ready`; `MapHealth`: `MapHealthChecks("/health/live", predicate: _ => false)` and `MapHealthChecks("/health/ready", tag "ready")`, both restricted to GET and HEAD; no `/health` route) (R7)
- [ ] T018 [US1] Create `Startup/HoardApp.cs`: `WebApplication.CreateEmptyBuilder(new() { Args = [] })`, `UseKestrelCore`, routing, `PreferHostingUrls(false)`, simple console logger with `Microsoft.*` at Warning, exactly one endpoint (`ListenLocalhost(port)` for `localhost`, `Listen(ip, port)` for an address; never `ListenAnyIP`), then `AddHealth`/`MapHealth`. `Build(HoardOptions)` returns the app unstarted so tests can map extra endpoints (R1, R12)
- [ ] T019 [US1] Create `Startup/StartupReport.cs`: the stderr problem block (`Hoard could not start: N problems in its configuration.` then one `  - <source> <name>: <problem>; <fix>` line each) and the Information summary in the shape of contracts/configuration-and-startup.md, listing addresses actually bound from the server's bound endpoints and marking a value that overrides the file with `overrides hoard.json` (R9, FR-021)
- [ ] T020 [US1] Create `Startup/HoardStartup.cs` (`RunAsync(args, environment, workingDirectory)` → exit code: read → validate → on problems write the report and return 2 → build → start → log summary → wait for shutdown → `Hoard stopped.` → 0) and reduce `src/Hoard.Host/Program.cs` to calling it with `Environment.GetCommandLineArgs()[1..]`, the process environment and the working directory (R8, R9)
- [ ] T021 [US1] Verify R1's risk: confirm `CreateEmptyBuilder` registers the console lifetime so Ctrl+C/SIGTERM stops the host (a quick `HostProcess` SIGTERM check; the full test lands in US5 T051). If it does not, switch to the R1 fallback (`CreateSlimBuilder(new() { Args = [] })` with `Configuration.Sources.Clear()` and the spec 001 Kestrel lockdown) and record the switch in `research.md` R1
- [ ] T022 [US1] Update `eng/smoke/smoke-binary.sh`: probe `/health/live` and `/health/ready`, require `GET /health` → 404; keep the clean, hostile-environment and hostile-appsettings runs; change the `hostile-command-line` run (`--urls …`) to expect a non-zero exit, no listener and `--urls` named in the output; compare the full listener `address:port` set exactly ({127.0.0.1:8484, [::1]:8484} where IPv6 exists) so an extra listener on another port fails (R11, FR-025)

**Checkpoint**: T009–T013 pass; `dotnet test --solution Hoard.slnx` green; `smoke-binary.sh` passes locally.

---

## Phase 4: User Story 2 — Invalid configuration fails fast and clearly (Priority: P1)

**Goal**: every malformed, unknown or unusable input is reported in one run with source, name and fix; exit 2 (exit 1 for a bind failure); no port opened, no fallback, no value echoed for unknown options.

**Independent test**: one process start per invalid input; check exit code, empty listener set and the message.

### Tests for User Story 2 (write first; they MUST fail)

- [ ] T023 [P] [US2] `Tests/Startup/OptionReaderErrorTests.cs`, command line: unknown `--listen:prot` (named, value not echoed); positional argument, `-x` and `/x` switches reported by position ("argument N"), never by content; a known option with no value; the same option twice; after a known option the next token is its value even if it starts with `--`, `-` or `/` (R2)
- [ ] T024 [P] [US2] `Tests/Startup/OptionReaderFileTests.cs`, file: unparseable JSON (`{ "listen": `) reported with line and position only, never the parser message or file content; non-object root; arrays, `null` and booleans; keys differing only in case; unknown key (`config`, `storage`, `listen.prot`); `listen.port` as a JSON number and as a string both accepted; an explicit `--config missing.json` and `HOARD_CONFIG=missing.json` fail naming the file; an automatically found malformed `hoard.json` fails like a named one (FR-005, FR-008, spec Edge Cases)
- [ ] T025 [P] [US2] `Tests/Startup/OptionReaderEnvironmentTests.cs`: `HOARD_LISTEN__PROT` is an unknown option naming the variable and not echoing its value; `hoard_listen__port` matches `listen.port` ignoring case; an empty `HOARD_DATA__PATH` is malformed, not "use the default" (R3, spec Edge Cases)
- [ ] T026 [P] [US2] `Tests/Startup/ListenAddressTests.cs`: accepts `localhost`, `127.0.0.1`, `10.1.2.3`, `0.0.0.0`, `::`, `::1`, `fe80::1`; rejects (with expected form) empty, `127.1`, `2130706433`, `010.0.0.1`, `0x7f.0.0.1`, ` 127.0.0.1`, `[::1]`, `fe80::1%eth0`, `10.0.0.300`, `example.com`, and `::ffff:127.0.0.1` with "use the IPv4 form"; canonical round-trip (`ToString()` equals input, ignoring case for IPv6) (FR-011, R4)
- [ ] T027 [P] [US2] `Tests/Startup/OptionValidatorTests.cs`, per-option and collection: port `0`, `65536`, `-1`, `+80`, ` 80`, `abc`, empty all malformed with "integer 1–65535"; empty `data.path` malformed; several problems from different sources are all reported in one result (US2 sc.3); a `data.path` that is an existing file is reported with the other problems; no data directory is created when any problem exists (FR-006, FR-007, data-model rules 1–4)
- [ ] T028 [P] [US2] `Tests/Startup/DataDirectoryErrorTests.cs`: an existing but unwritable directory fails the write check naming the path (skipped on Windows and when running as root); a parent that cannot be created fails naming the path; the write check refuses a pre-existing `.hoard-write-check-*` symlink rather than following it (US2 sc.4, R6)
- [ ] T029 [P] [US2] `Tests/Process/InvalidConfigurationTests.cs` (`HostProcess`): for `--listen:prot 9000`, `HOARD_LISTEN__PORT=abc`, `--listen:address example.com`, a malformed `hoard.json`, `--config missing.json`, an unwritable data directory, and the three-problem run from quickstart §3: exit code 2, stderr has the report heading and names each option/path and source, no stack trace, nothing listening on the port, and no data directory created; a second instance on a port already in use exits 1 naming `127.0.0.1:<port>` (US2 sc.1–6, SC-002)

### Implementation for User Story 2

- [ ] T030 [US2] Extend `Startup/OptionReader.cs` with the command-line error rules from T023 (value-after-option consumption, positional and switch errors by position, missing value, repeats) and the file rules from T024 (Hoard-written parse message with line/position, root/array/null/boolean/case-duplicate errors, explicit file must exist, files ≤ 1 MiB regular files)
- [ ] T031 [US2] Extend `Startup/ListenAddress.cs` with parsing and loopback classification per R4: `localhost`, or `IPAddress.TryParse` with canonical round-trip; reject IPv4-mapped; loopback = `localhost`, 127.0.0.0/8 or `::1`, checked by Hoard (not `IPAddress.IsLoopback`); `0.0.0.0` → `IPAddress.Any`, `::` → `IPAddress.IPv6Any`
- [ ] T032 [US2] Extend `Startup/OptionValidator.cs` with per-option rules (port digits only 1–65535; empty values malformed) and the data-directory preconditions from T027/T028, collecting every problem before returning
- [ ] T033 [US2] In `Startup/HoardStartup.cs`, map a Kestrel bind failure (`IOException` with `AddressInUseException` or address-not-available) to exit 1 with `Hoard could not start: <address>:<port> is already in use (…sources…).`; any other unexpected exception prints only its type and a fixed Hoard message, no stack trace, exit 1 (R9, contracts exit codes)

**Checkpoint**: T023–T029 pass; US1 tests still pass.

---

## Phase 5: User Story 3 — Explicit network exposure with a warning (Priority: P1)

**Goal**: a non-loopback address needs TLS mode `https` (certificate, HTTPS only) or `proxy`; otherwise refused. Exposure logs the anonymous-reads warning. Precedence is visible in the summary. Covers US2 sc.7 (certificate failures) and SC-007 (secrets).

**Independent test**: non-loopback with no mode (refused), with `proxy`, and with `https`; loopback with and without a mode; precedence across three sources.

### Tests for User Story 3 (write first; they MUST fail)

- [ ] T034 [P] [US3] `Tests/Startup/TlsRulesTests.cs` (validator, cross-option): non-loopback without a mode fails naming `https` and `proxy` (FR-027); loopback (`localhost`, `127.0.0.1`, `::1`) without a mode → plain HTTP, with `https` or `proxy` accepted; mode values `https`/`HTTPS`/`proxy` accepted, `tls`/empty malformed; certificate, key or password without mode `https` fail as contradictory (FR-029); `https` without a certificate fails; a key file with a PKCS#12 certificate is contradictory (data-model rule 3)
- [ ] T035 [P] [US3] `Tests/Startup/CertificateLoaderTests.cs` (with `TestCertificates`): PEM cert+key in one file, PEM with separate `keyFile`, PEM with encrypted key + password, and PFX + password load with a private key; missing file, directory, FIFO (Unix) and > 1 MiB file fail as not a regular file / too large; wrong password, a certificate without private key, a PEM certificate with a key that does not match it, expired and not-yet-valid each fail with `certificate could not be opened: <Hoard-written reason>` naming the option; no failure message contains the password or the exception text (FR-028, R5, US2 sc.7)
- [ ] T036 [P] [US3] `Tests/Startup/PrecedenceTests.cs`: each option set in two and in three sources with different values, including the config file location (`--config` over `HOARD_CONFIG`), takes the command-line value over the environment over the file; the summary names the effective source and marks `overrides hoard.json` (US3 sc.5, SC-003)
- [ ] T037 [P] [US3] `Tests/Features/Health/HttpsEndpointTests.cs` (in-process, real Kestrel): in `https` mode on a free loopback port, `GET /health/ready` over HTTPS → 200 and the served certificate's thumbprint equals the configured one (no development/default certificate); a plain-HTTP request to the same port gets no normal HTTP response (SC-008, R5)
- [ ] T038 [P] [US3] `Tests/Startup/StartupReportTests.cs`: the exposure warning (contracts text) is produced exactly when the address is not loopback, with the `proxy` sentence only in `proxy` mode, and never for `localhost`, `127.0.0.1` or `::1`; the `https` summary line shows subject and expiry; the password renders only as `listen.tls.password=(set) (<source>)` (FR-010, FR-021, SC-004)
- [ ] T039 [P] [US3] `Tests/Process/ExposureTests.cs` (`HostProcess`): `--listen:address 0.0.0.0` exits 2 naming both modes with nothing listening; `--listen:address 0.0.0.0 --listen:tls:mode proxy` (and the same via `HOARD_*` and via `hoard.json`) serves on `0.0.0.0:<port>` only and logs the warning once; a loopback run logs no warning; `--listen:tls:mode proxy --listen:tls:certificateFile cert.pem` exits 2 as contradictory (US3 sc.1–4)
- [ ] T040 [P] [US3] `Tests/Process/SecretTests.cs` (`HostProcess`): plant a unique secret and assert it never appears in stdout or stderr for: `--listen:tls:password <s>` (exit 2 pointing to `HOARD_LISTEN__TLS__PASSWORD` and the file), `--listen:tls:password=<s>`, passwords starting with `--`, `-` and `/`, the password option repeated, a password of the wrong JSON type in the file, a password set with mode `proxy`, a wrong PFX password, the secret as a positional argument, as the value of an unknown option, and in a non-`HOARD_` environment variable; also no output contains the full environment or command line (FR-008, FR-024, SC-007)

### Implementation for User Story 3

- [ ] T041 [US3] Extend `Startup/OptionValidator.cs` with the TLS cross-option rules from T034 and `Startup/OptionReader.cs` with the dedicated `--listen:tls:password` refusal (consumes the next token as its value, never echoes it, names `HOARD_LISTEN__TLS__PASSWORD` and the file)
- [ ] T042 [US3] Create `Startup/CertificateLoader.cs`: regular file ≤ 1 MiB checks for certificate and key; format by content (`-----BEGIN` → `X509Certificate2.CreateFromPemFile(cert, key)` or `CreateFromEncryptedPemFile(cert, password, key)`, key defaulting to the certificate file; otherwise `X509CertificateLoader.LoadPkcs12FromFile(path, password)` with default `Pkcs12LoaderLimits`); require a private key and `NotBefore ≤ now ≤ NotAfter`; Hoard-written reasons only. On Windows round-trip a PEM-loaded certificate through an in-memory PKCS#12 export, zeroing the bytes with `CryptographicOperations.ZeroMemory`; dispose the certificate on shutdown (R5). Run it as a precondition in `OptionValidator`
- [ ] T043 [US3] In `Startup/HoardApp.cs`, add `UseHttps(cert)` to the single endpoint (`Listen(address, port, o => o.UseHttps(cert))` / `ListenLocalhost(port, o => o.UseHttps(cert))`) in `https` mode; add `UseKestrelHttpsConfiguration` only if `UseHttps` requires it under `UseKestrelCore` (R1, R5)
- [ ] T044 [US3] In `Startup/StartupReport.cs` and `Startup/HoardStartup.cs`, log the exposure Warning once after the summary when not loopback (with the `proxy` sentence in `proxy` mode), and the `https` summary line with subject and `NotAfter` (FR-010, FR-021)
- [ ] T045 [US3] Extend `eng/smoke/smoke-binary.sh` (R11): an `exposed` run (`--listen:address 0.0.0.0 --listen:tls:mode proxy`) requiring the warning and exactly {0.0.0.0:8484}; a `no-tls-mode` run (`--listen:address 0.0.0.0`) requiring exit 2, no listener and both mode names; an `https` run with an `openssl`-generated PEM pair requiring `curl -k https://127.0.0.1:8484/health/ready` → 200, plain `http://` → no HTTP status (curl exit 52 or 56) and the exact loopback listener set; a `bad-certificate` run requiring exit 2 and nothing listening, using a certificate paired with a key from a second `openssl req` call (mismatched key: portable to every runner's `openssl`, unlike back-dated validity, which needs `-not_after` from OpenSSL 3.4 or an `openssl ca` setup; expiry is covered by T035). Must work on Linux, macOS and Git Bash on Windows

**Checkpoint**: T034–T040 pass; all earlier tests pass; `smoke-binary.sh` passes locally including the https run.

---

## Phase 6: User Story 4 — Container reachable through port publishing (Priority: P2)

**Goal**: the image binds `0.0.0.0` with `proxy` and `/data` through `HOARD_*`, is reachable via `-p 127.0.0.1:8484:8484`, persists data in a named volume, and stays non-root.

**Independent test**: `eng/smoke/smoke-container.sh <image>`.

### Tests for User Story 4 (write first; they MUST fail against the spec 001 image)

- [ ] T046 [US4] Rework `eng/smoke/smoke-container.sh` (R11, contracts/container-image.md): same health paths; default run reachable from a neighbouring container on the bridge at `:8484/health/ready` with the exposure warning in `docker logs` and port 8484 as the only listener; hostile framework environment run still only 8484; `--urls` argument refused; `-e HOARD_LISTEN__ADDRESS=127.0.0.1` loopback only and refused from the bridge (positive control); named-volume run: `docker stop` completes in under 10 s and `docker inspect` reports exit code 0 with `Hoard stopped.` in the logs, then remove, recreate with the same volume, summary says `(existing)` (US5 sc.3, FR-022, SC-006); `https` run with an `openssl` self-signed PEM mounted read-only: HTTPS answers from the bridge with `curl -k`, plain HTTP gets no normal response, and the exposure warning is logged (SC-004). Keep the non-root image and `/proc/1/status` uid checks and `--read-only --cap-drop ALL` inside the shared `smoke()` function so every run inherits them; mount a named volume or a `--tmpfs` owned by the app uid at `/data` for `--read-only` runs; compare the full listener `address:port` set exactly

### Implementation for User Story 4

- [ ] T047 [US4] Update `deploy/docker/Dockerfile` (R10): `ENV HOARD_LISTEN__ADDRESS=0.0.0.0 HOARD_LISTEN__TLS__MODE=proxy HOARD_DATA__PATH=/data`, `EXPOSE 8484`, and `/data` copied from an empty `scratch` stage with `COPY --chown=$APP_UID:$APP_UID --chmod=0700`. No `RUN` steps and no `VOLUME`. Update the header comment. If the named-volume smoke run shows the ownership does not carry over, apply the R10 fallback (a checked-in empty `deploy/docker/data/` with `.gitkeep` excluded by `.dockerignore`) and record it in `research.md` R10
- [ ] T048 [US4] Build the image locally as in `docs/DEVELOPMENT.md` and run `eng/smoke/smoke-container.sh` until every run passes

**Checkpoint**: container smoke passes locally on the host architecture.

---

## Phase 7: User Story 5 — Liveness, readiness, restart and clean shutdown (Priority: P2)

**Goal**: readiness reflects startup, the data directory and shutdown; liveness has no conditions; a stop signal drains in-flight requests within 5 s and exits 0; restarts reuse state.

**Independent test**: probe both endpoints while the data directory breaks and during stop; SIGTERM with a request in flight; restart.

### Tests for User Story 5 (write first; they MUST fail)

- [ ] T049 [P] [US5] `Tests/Features/Health/DataDirectoryCheckTests.cs`: readiness 503 `Unhealthy` while the data directory is unwritable or removed, liveness still 200; readiness back to 200 once it is usable again (no latch); concurrent probes share one in-flight check (single-flight); a check exceeding 2 s reports Unhealthy (inject a slow write check); results are not cached between probes (US5 sc.2, FR-018, R6)
- [ ] T050 [P] [US5] `Tests/Features/Health/ShutdownTests.cs` (in-process): readiness turns 503 once `ApplicationStopping` fires; a test-only slow endpoint mapped on the built app completes when `StopAsync` is called with it in flight; new connections are refused after stop begins; `HostOptions.ShutdownTimeout` is 5 s (US5 sc.3, FR-022, R8)
- [ ] T051 [P] [US5] `Tests/Process/LifecycleTests.cs` (`HostProcess`, SIGTERM cases skipped on Windows): SIGTERM → `Hoard is stopping.`, `Hoard stopped.`, exit 0 within 10 s; SIGTERM sent before the summary appears → exit 0, no stack trace; clean stop then restart with the same config → same address, data directory `(existing)`; kill -9 then restart → starts normally (US5 sc.3–5, SC-006, FR-023)

### Implementation for User Story 5

- [ ] T052 [US5] Create `src/Hoard.Host/Features/Health/DataDirectoryCheck.cs` (`IHealthCheck`, tag `ready`) running `DataDirectory.WriteCheck()` with single-flight sharing and a 2 s timeout, no caching; register it in `HealthEndpoints.AddHealth` (R6, R7)
- [ ] T053 [US5] In `Startup/HoardApp.cs` set `HostOptions.ShutdownTimeout = TimeSpan.FromSeconds(5)` (fixed, not an option); in `Startup/HoardStartup.cs` log `Hoard is stopping.` on `ApplicationStopping`, `Hoard stopped.` after the host stops, and catch the startup-cancellation `OperationCanceledException` to log that Hoard stopped before serving and return 0 without a stack trace (R8)

**Checkpoint**: T049–T051 pass; full suite green.

---

## Phase 8: Polish & cross-cutting concerns

- [ ] T054 [P] Update `README.md` (plan "Documentation changes"): release-status "working" section lists configuration sources and precedence, the options, loopback default, explicit exposure with the two TLS modes, `/health/live` and `/health/ready`, and the container run command; remove "the container is unreachable from the host"; "Try the alpha" shows `docker run -d --name hoard -p 127.0.0.1:8484:8484 -v hoard-data:/data …`, `curl …/health/ready`, the exposure warning, the `https` and reverse-proxy routes, that only `-e HOARD_*` variables and command-line arguments override the image's listen and data settings (a mounted `hoard.json` does not, because the file ranks below the environment; FR-012), and that Docker port publishing bypasses host firewalls such as ufw; a short options table linking to `docs/DEVELOPMENT.md`. Keep the `release-status` markers intact (`eng/release/status-notes.sh` reads them)
- [ ] T055 [P] Update `docs/DEVELOPMENT.md`: the options table with all three name forms and defaults, precedence, exit codes 0/1/2, health endpoints, secret handling (password only from environment or a mode-0600 file, command-line form refused, `-e` values visible to `docker inspect`, Windows key-store note after a crash), which sources override the container image's values (environment and command line, not `hoard.json`; FR-012), that clean shutdown on a stop signal is tested on Linux (process tests, container smoke) and Ctrl+C on Windows is not automatically tested, what each smoke script now proves, and local HTTPS testing with `openssl`
- [ ] T056 [P] Update the smoke-script descriptions in `CLAUDE.md`'s Commands section (binary: loopback by default, refuses unsafe exposure; image: reachable on 8484 with `proxy` declared, non-root, no extra ports). Check `docs/RELEASING.md` and change it only if it describes the smoke checks' content
- [ ] T057 Search the repo for stale references to `GET /health`, "unreachable from the host" and `Mvc.Testing` outside `specs/001-*` and fix any in shipped docs or scripts
- [ ] T058 Run `dotnet build Hoard.slnx` (zero warnings), `dotnet test --solution Hoard.slnx`, `eng/smoke/smoke-binary.sh <exe>` and `eng/smoke/smoke-container.sh <image>` and record the actual results
- [ ] T059 Walk through [quickstart.md](quickstart.md) §2–§6 by hand against the local executable and image; fix any mismatch between behavior and the contracts
- [ ] T060 Push and confirm PR #6 CI is green (`build-test`, `smoke`) and the `release.yml` dry run passes on all native runners, including the binary smoke's `https` run on win-x64 and osx-arm64 (R5 Windows PEM re-export verification)

---

## Dependencies & execution order

- **Setup (T001–T005)** → **Foundational (T006–T008)** → **US1** → **US2** → **US3** → **US4** and **US5** → **Polish**.
- US2 and US3 extend `OptionReader`, `OptionValidator` and `ListenAddress` created in US1, so they run in order after US1.
- US4 needs US3 (the image declares `proxy`, which only exists after T041; the https smoke run needs T042–T043).
- US5 needs US1's host and health slice; it is independent of US3/US4 and can run in parallel with US4.
- T021 (lifetime check) blocks T053; if the R1 fallback is taken, T012's "no configuration sources" assertion becomes "sources cleared".
- T060 needs every other task.

### Parallel opportunities

- Setup: T002, T003, T004 together.
- Foundational: T007 and T008 after T006.
- Every story's test tasks marked [P] are separate files and can be written together, e.g. US1: T009–T013; US2: T023–T029; US3: T034–T040; US5: T049–T051.
- After US3: Phase 6 (US4) and Phase 7 (US5) in parallel.
- Polish docs T054–T056 together.

## Implementation strategy

1. **MVP = Setup + Foundational + US1**: a host that starts on loopback with defaults, the new health paths and framework settings ignored. The smoke binary passes. Stop and validate.
2. **US2**: fail-fast diagnostics. **US3**: exposure and TLS. These complete the P1 security baseline; do not push a CI-gated release dry run with exposure until US3 is done.
3. **US4** (container) and **US5** (lifecycle) in either order.
4. **Polish**: docs describe shipped behavior, full local and CI verification.

Commit after each phase checkpoint with tests green.
