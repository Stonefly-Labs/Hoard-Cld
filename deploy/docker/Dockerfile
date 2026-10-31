# Hoard container image, built from the release's already-published, architecture-checked
# executables (no compilation, no RUN steps, so no emulation is needed for arm64).
#
#   docker buildx build -f deploy/docker/Dockerfile --platform linux/amd64,linux/arm64 \
#     --build-context bin=<dir with hoard-<version>-linux-x64 and -linux-arm64> .

FROM scratch AS bin-amd64
COPY --from=bin hoard-*-linux-x64 /hoard

FROM scratch AS bin-arm64
COPY --from=bin hoard-*-linux-arm64 /hoard

FROM bin-${TARGETARCH} AS selected

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled@sha256:4309626281bfe550c0f6bb4242c435cd5cdabed1d05a5bc5843f659336f53296

LABEL org.opencontainers.image.source="https://github.com/Stonefly-Labs/Hoard-Cld" \
      org.opencontainers.image.description="Hoard, a self-hosted pull-through package cache" \
      org.opencontainers.image.licenses="MIT"

# Root-owned and read-only to the app user.
COPY --from=selected --chmod=0555 /hoard /app/hoard

USER $APP_UID
ENTRYPOINT ["/app/hoard"]
