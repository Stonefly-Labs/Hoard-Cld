# Contract: configuration, startup output and exit codes

The operator-facing surface of the `hoard` executable and image. Option names and rules are in
[data-model.md](../data-model.md).

## Sources and precedence

1. Command line: `--name value` or `--name=value`.
2. Environment: `HOARD_*` variables. Other variables are ignored.
3. File: `--config <path>` / `HOARD_CONFIG`, otherwise `./hoard.json` if it exists.
4. Built-in defaults.

Framework settings (`ASPNETCORE_*`, `DOTNET_*`, `Kestrel__*`, `appsettings*.json`) are never
read. A framework argument such as `--urls` is an unknown option. After a known option, the
next argument is always its value, even if it starts with `-`, `--` or `/`.

**Secret handling.**
- The certificate password is best set through `HOARD_LISTEN__TLS__PASSWORD` or the file.
- On the command line it is visible in process listings and shell history.
- In a container, the environment is visible to `docker inspect`.
- A `hoard.json` that holds a password should be readable only by its owner (mode 0600).

Example file:

```json
{
  "listen": { "address": "0.0.0.0", "port": 8484, "tls": { "mode": "proxy" } },
  "data": { "path": "/var/lib/hoard" }
}
```

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Stopped cleanly after a stop signal, including a signal received during startup |
| 1 | The listener could not bind (port in use, address not available), or an unexpected failure |
| 2 | Invalid configuration or failed precondition (options, file, data directory, certificate); nothing was opened |

## Startup failure report (stderr)

The wording is illustrative; the shape is the contract.

```text
Hoard could not start: 2 problems in its configuration.
  - command line --listen:prot: unknown option. Known options: --config, --listen:address, --listen:port, --listen:tls:mode, --listen:tls:certificateFile, --listen:tls:keyFile, --listen:tls:password, --data:path
  - environment HOARD_LISTEN__ADDRESS: '0.0.0.0' is not loopback, so a TLS mode is required. Set HOARD_LISTEN__TLS__MODE (or --listen:tls:mode) to 'https' with a certificate, or to 'proxy' if a TLS-terminating reverse proxy is in front.
```

Each problem has these parts:

- the source;
- the option name, in that source's form;
- what is wrong;
- the expected form or the fix.

Values appear only for non-secret known options. They never appear for unknown options,
for positional arguments or malformed switches (reported by position, e.g. "argument 3"),
or for `listen.tls.password`. JSON parse errors give the line and position only. An
unexpected failure prints the exception type and a fixed message, never the exception
message. There is no stack trace.

Port in use (exit 1):

```text
Hoard could not start: 127.0.0.1:8484 is already in use (listen.address from default, listen.port from default).
```

## Startup summary (log, Information)

```text
Hoard 0.0.2-alpha started.
  Listening:      http://0.0.0.0:8484 (TLS mode proxy)
  Data directory: /data (existing)
  Options:        listen.address=0.0.0.0 (environment HOARD_LISTEN__ADDRESS), listen.port=8484 (default), listen.tls.mode=proxy (environment HOARD_LISTEN__TLS__MODE), data.path=/data (environment HOARD_DATA__PATH)
  Config file:    none
```

The summary covers:

- **Listening.** In `https` mode it reads `https://<address>:<port> (TLS mode https,
  certificate CN=hoard.example, expires 2027-10-31)`. For `localhost` it reads
  `http://localhost:8484 (bound: 127.0.0.1:8484, [::1]:8484)`, listing the addresses actually
  bound.
- **Data directory.** It shows `(created)` on a first run.
- **Options.** It lists only options that are set or defaulted. The password appears as
  `listen.tls.password=(set) (<source>)`. A value that overrides one in the file is marked
  `overrides hoard.json`.

## Exposure warning (log, Warning)

Logged once, after the summary, exactly when the address is not loopback:

```text
Hoard is listening on 0.0.0.0, which is reachable from the network. Package reads are anonymous: anyone who can reach this address can use the cache.
```

In `proxy` mode it adds: `Hoard serves plain HTTP here and relies on a TLS-terminating
reverse proxy in front of it.` Inside the container, the docs explain that host exposure is
decided by Docker port publishing.

## Shutdown (log)

`Hoard is stopping.` on the stop signal; `Hoard stopped.` before exit 0. In-flight requests
get up to 5 seconds.
