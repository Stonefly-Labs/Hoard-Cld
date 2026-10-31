# Data model: Host, configuration and deployment profiles

No persisted data. The entities below exist in memory for one process lifetime.

## Option

The complete set of Hoard options in this spec (FR-002). Names match ignoring case (research R3).

| File key path | Environment | Command line | Default | Valid values | Secret |
|---|---|---|---|---|---|
| `listen.address` | `HOARD_LISTEN__ADDRESS` | `--listen:address` | `localhost` | `localhost`, or a canonical IPv4/IPv6 address (`ToString()` round-trips; no brackets, scope or IPv4-mapped form), including `0.0.0.0` and `::` (R4) | no |
| `listen.port` | `HOARD_LISTEN__PORT` | `--listen:port` | `8484` | integer 1–65535 (a JSON number or a string in the file) | no |
| `listen.tls.mode` | `HOARD_LISTEN__TLS__MODE` | `--listen:tls:mode` | unset | `https`, `proxy` (ignoring case) | no |
| `listen.tls.certificateFile` | `HOARD_LISTEN__TLS__CERTIFICATEFILE` | `--listen:tls:certificateFile` | unset | path to a regular PEM or PKCS#12 file, ≤ 1 MiB | no |
| `listen.tls.keyFile` | `HOARD_LISTEN__TLS__KEYFILE` | `--listen:tls:keyFile` | unset | path to a regular PEM key file, ≤ 1 MiB (PEM certificate only) | no |
| `listen.tls.password` | `HOARD_LISTEN__TLS__PASSWORD` | — (refused, see below) | unset | any non-empty string | **yes** |
| `data.path` | `HOARD_DATA__PATH` | `--data:path` | `./data` | any path (relative paths resolve against the working directory) | no |
| — (not a file key) | `HOARD_CONFIG` | `--config` | `hoard.json` if it exists in the working directory | path to a JSON file that must exist | no |

Relative certificate, key and config paths resolve against the working directory.

The password has no command-line form (spec FR-008). `--listen:tls:password` is not reported
as an unknown option. It gets its own error, which points to `HOARD_LISTEN__TLS__PASSWORD` or
the file, and the next token is consumed as its value and never echoed.

## Option value

`(option, raw value, source)`. Source is one of `default`, `file <absolute path>`,
`environment <NAME>` or `command line <--name>`. The effective value is the
highest-precedence one: command line > environment > file > default (FR-001).

## Validated options (`HoardOptions`)

The output of validation. The host is built only from this:

- `Listen`: the endpoint kind (`Localhost` | `Address(IPAddress)`), the port, and `IsLoopback`.
- `Tls`: `None` | `Proxy` | `Https(X509Certificate2 certificate)`.
- `DataDirectory`: the absolute path, and `Created` (whether this run created it).
- `Sources`: each effective option with its source, for the summary. The password appears
  only as `(set)`.
- `ConfigFile`: the absolute path read, or none.

## Validation rules (all collected in one pass, FR-006)

1. **Reading.**
   - An unknown name in any source is an error that names the source and the name, not
     the value.
   - The command line rejects positional arguments, `-x` and `/x` switches, missing
     values and repeated options.
   - The file rejects JSON that does not parse, a non-object root, arrays, `null`,
     booleans and case-duplicate keys.
   - An explicit `--config`/`HOARD_CONFIG` file must exist.
   - Empty values are malformed.
2. **Per option.** Each value must be in the "Valid values" column.
3. **Cross-option.**
   - A non-loopback address with no TLS mode is an error naming `https` and `proxy`
     (FR-027).
   - Certificate, key or password options without mode `https` are contradictory
     (FR-029).
   - Mode `https` requires a certificate.
   - A key or password together with a PKCS#12 certificate: the key is contradictory; the
     password is the PKCS#12 password.
4. **Preconditions.** These run only when the options they need are valid:
   - The data directory path must not be an existing file; this is reported with the
     other errors. It is created if missing (mode 0700 on Unix), and must pass the write check, only when
     rules 1–3 found nothing, so a refused start leaves no directory behind (research R6).
   - The certificate must load, have a private key and be within its validity period
     (research R5).

Any error → stderr report, exit 2, no port opened. Bind failure after validation → exit 1.

## Readiness state

```text
Starting ──ApplicationStarted──▶ Serving ──ApplicationStopping──▶ Stopping
```

- **Liveness** is healthy in any state where the pipeline answers.
- **Readiness** is healthy only in `Serving`, and only while the data-directory write check
  passes. A failed check makes readiness answer 503. When the directory becomes usable
  again, readiness answers 200 again. There is no latch.
