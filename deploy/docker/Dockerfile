# Hoard container image, built from the release's already-published, architecture-checked
# executables (no compilation, no RUN steps, so no emulation is needed for arm64).
#
# Hoard listens on all interfaces inside the container with TLS mode proxy declared, so Docker
# port publishing decides host exposure; data lives in /data. All three come from HOARD_*
# variables, which `docker run -e` and command-line arguments override.
#
#   docker buildx build -f deploy/docker/Dockerfile --platform linux/amd64,linux/arm64 \
#     --build-context bin=<dir with hoard-<version>-linux-x64 and -linux-arm64> .

FROM scratch AS bin-amd64
COPY --from=bin hoard-*-linux-x64 /hoard

FROM scratch AS bin-arm64
COPY --from=bin hoard-*-linux-arm64 /hoard

FROM bin-${TARGETARCH} AS selected

# Nothing; copied below as /data so the directory exists without a RUN step.
FROM scratch AS empty

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled@sha256:4309626281bfe550c0f6bb4242c435cd5cdabed1d05a5bc5843f659336f53296

LABEL org.opencontainers.image.source="https://github.com/Stonefly-Labs/Hoard-Cld" \
      org.opencontainers.image.description="Hoard, a self-hosted pull-through package cache" \
      org.opencontainers.image.licenses="MIT"

# Root-owned and read-only to the app user.
COPY --from=selected --chmod=0555 /hoard /app/hoard

# Owned by the app user, so a new named volume mounted here inherits that ownership. No VOLUME:
# it would create an anonymous volume on every run that derived images could not remove.
COPY --from=empty --chown=$APP_UID:$APP_UID --chmod=0700 / /data

ENV HOARD_LISTEN__ADDRESS=0.0.0.0 \
    HOARD_LISTEN__TLS__MODE=proxy \
    HOARD_DATA__PATH=/data

EXPOSE 8484
USER $APP_UID
ENTRYPOINT ["/app/hoard"]
