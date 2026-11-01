# Research: Host, configuration and deployment profiles

Decisions for spec 002. Each has a decision, why, and what else was considered. Items marked
**verify in implement** are framework behaviors the plan relies on; the first task touching
them proves them with a test, and the named fallback applies if the test disagrees.

## R1. Host builder: `WebApplication.CreateEmptyBuilder`

**Decision.** Build the host with `WebApplication.CreateEmptyBuilder(new() { Args = [] })`
and add only what Hoard uses: Kestrel core (`UseKestrelCore`), routing, health checks and the simple console logger.
Keep spec 001's `PreferHostingUrls(false)`. Hoard's own options (R2) are the only input to
the listener.

**Why.** The empty builder adds no configuration sources: no `appsettings*.json`, no
`ASPNETCORE_*`/`DOTNET_*` host variables, no framework command-line provider. FR-003 then
holds by construction rather than by overriding each source, and nothing outside Hoard's
option set can change log levels to hide the exposure warning (FR-010).

`UseKestrelHttpsConfiguration` serves config-driven HTTPS endpoints and default
certificates. It is added only if `UseHttps(X509Certificate2)` needs it under
`UseKestrelCore`; the thumbprint test (R5) guards either way. A regression test asserts that
the built app has no configuration sources and exactly one endpoint, so a move to the
fallback builder is caught.

**Verify in implement.** The empty builder still registers the console lifetime, so
Ctrl+C and SIGTERM stop the host (R8). The spec 001 hostile-environment and
hostile-appsettings smoke runs keep proving framework settings change nothing.
**Verified in implement:** `ConsoleLifetime` is registered and SIGTERM stops the host with exit 0.
The empty builder keeps in-memory and chained sources for its own host settings, but has no
environment, command-line or file provider; the regression test asserts exactly that.
**Fallback:** `CreateSlimBuilder(new() { Args = [] })` with `Configuration.Sources.Clear()`
and the spec 001 Kestrel lockdown kept.

**Alternatives.** `CreateBuilder(args)` with spec 001's lockdown: loads framework sources
that Hoard then has to neutralize one by one, and it would see Hoard's own `--` arguments.

## R2. Reading options: a small reader, not `Microsoft.Extensions.Configuration`

**Decision.** Hoard reads its three sources itself into one table of
`(option, value, source)` and validates that table (data-model.md):

- **Command line.** Accepts `--name value` and `--name=value` only.
  - After a known option, the next token is always its value, whatever it starts with
    (`--listen:tls:password --s3cret` consumes `--s3cret` as the refused password's value,
    so it is never parsed as an option or echoed). `--name=value` splits at the first `=`.
  - `--listen:tls:password` is refused (spec FR-008), and the error names the environment
    variable and the file as the places to set it.
  - A positional argument, a single-dash or `/` switch, a missing value, or the same
    option twice is an error.
  - Positional arguments and malformed switches are reported by position ("argument 3"),
    never by content, since the token may be a misplaced secret.
- **Environment.** Every variable whose name starts with `HOARD_` (ignoring case) is read.
  All other variables are ignored.
- **File.** Parsed with `System.Text.Json` (`JsonDocument`, comments allowed, trailing
  commas rejected). Parse errors are reported as Hoard's own text with line and position
  only, never the parser's message, which can echo file content. The root must be an object. A nested object is a key path; a leaf
  must be a string, or a number for `listen.port`. Arrays, `null`, booleans and keys
  that differ only in case are errors. A file that is empty or `{}` equals no file.

**Why.** The built-in command-line provider silently ignores some malformed input (a
trailing `--key` with no value, positional arguments) and accepts `/key`, which breaks
FR-004/FR-005. The built-in providers also merge sources before Hoard can tell which source
an unknown key came from, which FR-006 needs. The reader is small, has no dependencies and
is unit-tested exhaustively.

**Growth path.** None of this is built now; it is recorded so later specs extend the reader
rather than replace it.
- `OptionReader` takes the known-option table (`OptionNames`) as input. Spec 007 adds
  module-declared options to that table, and the unknown-option check stays in one place.
- When spec 005 needs a list (seed registries), key paths gain index segments
  (`registries.0.url` ↔ `HOARD_REGISTRIES__0__URL` ↔ `--registries:0:url`). Until then
  arrays are an error.
- If a provider needs `IConfiguration`/`IOptions` binding, the host can load the validated
  table into configuration with `AddInMemoryCollection`. Providers then see validated values
  only, never raw sources.
- `HoardOptions` is internal to the host. Providers in other assemblies (SQLite in 003,
  filesystem in 004) get values such as the absolute data-directory path from host
  composition.

**Alternatives.** Configuration providers plus per-provider key enumeration: still loses
the malformed-argument cases. `System.CommandLine`: a new dependency for eight options.

## R3. Option names and case

**Decision.** Canonical file key paths: `listen.address`, `listen.port`,
`listen.tls.mode`, `listen.tls.certificateFile`, `listen.tls.keyFile`,
`listen.tls.password`, `data.path`. Environment and command-line names derive mechanically (FR-008):
`HOARD_LISTEN__TLS__MODE`, `--listen:tls:mode`. The file location is `--config` /
`HOARD_CONFIG` only; `config` in the file is an unknown key. Names match ignoring case in
every source, as Windows environment variables do. Diagnostics and the summary print the
name in the form the source uses.

The `File` suffix keeps the key path from being mistaken for a passphrase. A path option's
value is echoed in errors, so a password typed there by mistake would otherwise be printed.

**Why.** Grouping the TLS options under `listen` keeps them with the one listener they
configure. `data.path` names the single data directory; later storage specs add their own
keys when they consume them (Spec Assumptions).

**Alternatives.** Flat names (`HOARD_TLS_MODE`): breaks the mechanical mapping chosen in
clarify. Product spec §10's `server.listen: host:port` sketch is illustrative only, and
the clarify answer chose separate address and port.

## R4. Listen address parsing and loopback classification

**Decision.**
- `localhost` → Kestrel `ListenLocalhost` (127.0.0.1 and ::1, as in spec 001). This is the
  default.
- An address is accepted only if `IPAddress.TryParse` succeeds and the parsed address's
  `ToString()` equals the input (ignoring case for IPv6). This canonical round-trip
  rejects short (`127.1`), integer, octal (`010.0.0.1`), hex, whitespace, bracketed and
  `%scope` forms, so Hoard listens exactly where the operator wrote.
- IPv4-mapped IPv6 (`::ffff:a.b.c.d`) is rejected with "use the IPv4 form".
- `0.0.0.0` → `IPAddress.Any`. `::` → `IPAddress.IPv6Any`, which Kestrel binds dual-mode.
  If the platform has no IPv6, the bind fails and startup reports it; there is no
  fallback to IPv4 (FR-007). `ListenAnyIP`, which falls back, is not used.
- Loopback means `localhost`, any address in 127.0.0.0/8, or `::1`. Hoard checks this
  itself rather than relying on `IPAddress.IsLoopback`; IPv4-mapped forms never reach
  classification.
- Host names are rejected (FR-011), so the exposure decision never depends on DNS.
- Port: decimal digits only, 1–65535; `0`, signs and whitespace are malformed.

## R5. TLS: certificate loading and HTTPS-only

**Decision.** Certificates are loaded and checked before the host is built:
- Certificate and key paths must be regular files of at most 1 MiB, so a FIFO or a device
  file cannot hang startup. The PKCS#12 loader keeps its default `Pkcs12LoaderLimits`.
- The format is detected by content. A file starting with `-----BEGIN` is PEM, read with
  `X509Certificate2.CreateFromPemFile(certificate, key)`, or
  `CreateFromEncryptedPemFile(certificate, password, key)` when a password is set. The key
  defaults to the certificate file itself. Anything else is PKCS#12, read with
  `X509CertificateLoader.LoadPkcs12FromFile(path, password)`.
- The certificate must have a private key, and the current time must be within
  `NotBefore`–`NotAfter`.
- A PEM-loaded key is ephemeral, and Windows `SslStream` rejects ephemeral keys. On Windows
  the loaded certificate is round-tripped through an in-memory PKCS#12 export, as Kestrel's
  own config loader does. The exported bytes are zeroed
  (`CryptographicOperations.ZeroMemory`) and the certificate is disposed on shutdown.
  After a crash, the key may remain in the user's key store; this is documented.
  **Verify in implement** with the binary smoke's `https` run on the win-x64 runner
  (release dry run, R11).
- Kestrel gets exactly one endpoint, with `UseHttps(cert)` in `https` mode, for both kinds:
  `Listen(address, port, o => o.UseHttps(cert))` and
  `ListenLocalhost(port, o => o.UseHttps(cert))`. No plain-HTTP endpoint exists in `https`
  mode, and a plain request to the port fails the handshake (SC-008). An in-process test
  asserts that the served certificate's thumbprint equals the configured one, so no
  development or default certificate can be used.
- Errors: every load failure (file missing, unreadable, wrong password, bad format)
  becomes "certificate could not be opened: <reason>", where the reason comes from Hoard,
  not from the exception text, so no value is echoed. Contradictions fail as configuration
  errors: certificate options without `https`, a key file with PKCS#12, or `https` without
  a certificate.
- The summary prints the certificate's subject and `NotAfter` (FR-021), never the password.

**Alternatives.** Kestrel's `Kestrel:Certificates` configuration: needs the framework
configuration that R1 removes, and its errors don't name Hoard's options.

## R6. Data directory and the write check

**Decision.** `Path.GetFullPath(value, workingDirectory)` once at startup. If the path is an
existing file, that is an error, reported with the others. The directory is created
(`Directory.CreateDirectory`, recording whether it existed) only when every reading,
per-option and cross-option check is clean, so a refused start never leaves a directory
behind. On Unix, a new
directory is created with mode 0700 (`UnixFileMode.UserRead | UserWrite | UserExecute`),
because later specs store cache contents, a database and token hashes there. An existing
directory's mode is not changed. The write check creates
`.hoard-write-check-<Path.GetRandomFileName()>` with `FileMode.CreateNew` (which refuses an
existing file or symlink) and `FileOptions.DeleteOnClose`, writes one byte and closes
it. Readiness (R7) runs the same check when requested. Concurrent
probes share one in-flight check (single-flight), and a check that takes longer than 2 s
reports Unhealthy, so a hung filesystem or a probe flood cannot pile up file operations
or hold requests open. Results are not cached between checks. Nothing else is created (FR-016).

## R7. Health endpoints

**Decision.** Built-in health checks:
- `MapHealthChecks("/health/live", predicate: _ => false)`: no checks, so it is healthy
  whenever the pipeline serves.
- `MapHealthChecks("/health/ready", tag "ready")` with two checks. `DataDirectoryCheck`
  runs the R6 write check. `StartupCheck` is healthy after `ApplicationStarted` and
  unhealthy once `ApplicationStopping` fires.
- The default response writer returns `Healthy`/`Unhealthy` as text/plain with 200/503,
  and no details (FR-019).
- Both endpoints are restricted to GET and HEAD; other methods get the framework's 405.
- `/health` gets no route, so it returns 404.

The endpoints live in `Features/Health/` as a vertical slice (CLAUDE.md). The slice owns its
own wiring (`AddHealth`, `MapHealth`), and `HoardApp` calls it. The readiness seam is the
built-in one: an `IHealthCheck` registered with the `ready` tag. Specs 003/004 add their
database and disk checks this way, without touching the slice. No Hoard readiness interface
is introduced.

## R8. Shutdown, signals and restart

**Decision.** `HostOptions.ShutdownTimeout = 5 s`, fixed in code, not an option. That is
inside Docker's 10 s default grace period (FR-022, SC-006), with margin for process exit.
The console lifetime handles SIGINT and SIGTERM. On `ApplicationStopping`, readiness turns
unhealthy and Kestrel stops accepting connections and drains in-flight requests up to the
timeout. After the host stops, Hoard logs `Hoard stopped` and returns exit code 0. A
signal during startup (before `StartAsync` finishes) cancels startup. That
`OperationCanceledException` is caught, Hoard logs that it stopped before serving, and
exits 0 without a stack trace. Restart needs nothing: this spec keeps no state (FR-023).

**Alternatives.** A `shutdownTimeout` option: no consumer yet (Constitution V).

## R9. Startup output, errors and secrets

**Decision.**
- Problems found before the host exists are written to stderr as one block: a heading
  with the count, then one line per problem as `<source> <name>: <problem>; <expected
  form>`. Exit codes: 2 for configuration and precondition problems (options, file, data
  directory, certificate); 1 for a listener that cannot bind (port in use, address not
  available) or an unexpected failure; 0 for a clean stop.
- Values are echoed only for non-secret known options (address, port, mode, paths). They
  are never echoed for unknown options (the value may be a misplaced secret) or for the
  password.
- After a successful bind, the summary is logged at Information through the
  `Hoard.Host` category. The exposure warning is one Warning entry. `Microsoft.*`
  categories log at Warning, so Kestrel's own "Now listening on" lines don't duplicate
  the summary.
- Unexpected exceptions print the exception type and a fixed Hoard-written message, never
  the exception message (framework, JSON and crypto messages can carry values or file
  content), and no stack trace. Exit 1.
- The summary lists the addresses actually bound (from the server's bound endpoints), so
  `localhost` without IPv6 shows only `127.0.0.1`.
- A file value overridden by the environment or command line is marked in the summary
  (`overrides hoard.json`). This is how an operator notices the container image's
  `HOARD_*` values winning over a mounted file.

**Why.** The summary and errors are the user interface of this spec (US1, US2). Withholding
values for unknown options closes the most likely secret-leak path (SC-007).

## R10. Container image

**Decision.**
- The image sets `ENV HOARD_LISTEN__ADDRESS=0.0.0.0 HOARD_LISTEN__TLS__MODE=proxy
  HOARD_DATA__PATH=/data` and `EXPOSE 8484`. Environment and command
  line rank at or above the image's environment, so both can override it (FR-012); a
  mounted `hoard.json` cannot.
- `/data` must exist in the image, owned by `$APP_UID`, so a new named volume inherits that
  ownership. The Dockerfile has no `RUN` steps (spec 001: no emulation for arm64), so the
  directory is copied from an empty stage with `COPY --chown=$APP_UID:$APP_UID --chmod=0700`. **Verify
  in implement** with the container smoke's named-volume run. **Fallback:** copy a
  checked-in empty `deploy/docker/data/` directory (with a `.gitkeep` excluded by
  `.dockerignore`).
- No `VOLUME` instruction. It would create an anonymous volume on every run without `-v`,
  and derived images could not remove it. The docs always mount a named volume, and the
  `--read-only` smoke runs mount one (or a `--tmpfs` owned by the app uid). Without a
  volume and with a writable layer, Hoard still runs, but its data does not outlive the
  container. Spec 019 owns the long-term volume contract.
- IPv4 `0.0.0.0` rather than `::`, because Docker networks are IPv4-only by default.

## R11. Smoke scripts

**Decision.** `smoke-binary.sh`:
- Probes `/health/live` and `/health/ready` and requires `/health` → 404.
- Keeps the clean, hostile-environment and hostile-appsettings runs with the loopback
  assertion.
- `hostile-command-line` (`--urls …`) now expects refusal: non-zero exit, no listener, and
  `--urls` named in the output (FR-004; see R13).
- Adds an `exposed` run (`--listen:address 0.0.0.0 --listen:tls:mode proxy`) that requires
  the exposure warning and a `0.0.0.0` listener on 8484 only.
- Adds a `no-tls-mode` run (`--listen:address 0.0.0.0`) that requires exit 2, no listener
  and both mode names in the message.
- Adds an `https` run with an `openssl`-generated PEM pair, which exists on all four
  runners, including Git Bash. `curl -k https://127.0.0.1:8484/health/ready` must return
  200, plain `http://` must get no HTTP status (curl exit 52 or 56), and the listener set
  must be exact. This is the only place the PEM path, and the Windows key re-export, run
  on win-x64 and osx-arm64.
- Adds a `bad-certificate` run (a certificate with a mismatched key from a second `openssl req`
  call): exit 2 and nothing listening. A back-dated (expired) certificate is not portable:
  `-not_after` needs OpenSSL 3.4+, and the runners ship OpenSSL 3.0 (Ubuntu 24.04), LibreSSL
  (macOS) and Git Bash's own build. Expiry is covered by the certificate loader unit tests.
- Every run compares the full `address:port` set exactly: {127.0.0.1:8484, [::1]:8484}
  by default, {0.0.0.0:8484} when exposed. An extra listener on another port fails the
  run.

`smoke-container.sh`:
- Same health paths.
- Default run: reachable from the bridge at `:8484/health/ready`, exposure warning in the
  logs, and the only listener is port 8484.
- Hostile-environment run: still only port 8484.
- Hostile command line: refused.
- Loopback override (`-e HOARD_LISTEN__ADDRESS=127.0.0.1`): loopback-only and refused from
  the bridge, which keeps spec 001's positive-control probe useful.
- Named-volume run: `docker stop` finishes in under 10 s with exit code 0 and `Hoard stopped.`
  logged, then recreate, and the summary says the data directory exists.
- `https` run: an `openssl` self-signed PEM mounted read-only. HTTPS answers from the
  bridge (`curl -k`); plain HTTP gets no normal response; the exposure warning is logged.
- Keeps the non-root checks and `--read-only --cap-drop ALL` inside the shared `smoke()`
  function, so every run, including the new volume and https runs, inherits them.
- Compares the full listener `address:port` set exactly, as the binary smoke does.

These run on every native runner in the release dry run, because `eng/**` and `deploy/**`
changes trigger it.

## R12. Test strategy

**Decision.** All in `tests/Hoard.Host.Tests`, test-first per story.
- **Unit:** option reader and validator, as pure functions over
  `(args, environment dictionary, file text)`: every source, precedence, unknown and
  malformed cases, address classification, and secret withholding. Certificate validation
  uses certificates generated in the test with `CertificateRequest`: PEM, PEM with
  separate and encrypted key, PFX, expired, not yet valid, no private key, wrong
  password. The data-directory check runs against temp directories (an unwritable
  directory is skipped on Windows and when running as root).
- **In-process:** the composed app (`HoardApp.Build(validatedOptions)`), on real Kestrel
  bound to a free loopback port. Covers health responses, 404s, HTTPS handshake and plain
  HTTP refused on the TLS port, readiness flipping on stop, and the drain test: a
  test-only slow endpoint is mapped on the built app, `StopAsync` is called with the
  request in flight, and the request completes. The drain test is the only way to get an
  in-flight request, since the product has only instant endpoints.
- **Process:** `dotnet Hoard.Host.dll` launched with a clean environment and a temp
  working directory. Covers exit codes and stderr for invalid config, port in use, the
  startup summary and warning text, SIGTERM → exit 0 within 10 s (skipped on Windows),
  restart reusing the data directory, and planted secrets absent from all output
  (SC-007). The secret cases include a password starting with `--`, `-` and `/`, a
  repeated password, a password of the wrong JSON type, a password with `proxy`, a
  misplaced value as a positional argument, and a non-Hoard environment variable.
- **Locating the host.** The test project resolves `Hoard.Host.dll`'s path at build time with
  MSBuild `GetTargetPath` and embeds it as `AssemblyMetadata`, as
  `Hoard.Architecture.Tests` does for Core. Tests run `dotnet <that path>`, so the host's own
  `runtimeconfig.json` and `deps.json` beside it are used.
- **Reaching host types.** Host types stay `internal`; `Hoard.Host.csproj` grants
  `InternalsVisibleTo` to `Hoard.Host.Tests`.
- **Version.** The summary reads `AssemblyInformationalVersionAttribute`. Local builds show
  the SDK default, so tests assert only that a version is present.
- `WebApplicationFactory` and `Microsoft.AspNetCore.Mvc.Testing` are dropped. They test
  through an in-memory server; this spec's behavior is the real listener.

## R13. Framework command-line arguments (resolved)

US1 scenario 4 originally said framework "environment variables or arguments" are ignored,
which contradicted FR-004 (every `--` argument must be a known option). At the plan gate the
maintainer chose FR-004, and the scenario was reworded:
- framework environment variables and settings files are ignored;
- framework command-line arguments such as `--urls` fail startup as unknown options.

In neither case does the listener widen.
