# syntax=docker/dockerfile:1
FROM debian:bookworm-slim

# Base tools & TLS so HTTPS works
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends \
      ca-certificates \
      curl \
      gnupg \
      dos2unix; \
    rm -rf /var/lib/apt/lists/*

# Install Caddy from Debian (stable, no extra repo needed)
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends caddy; \
    rm -rf /var/lib/apt/lists/*

# Install Tailscale (script adds its own apt source & keyring)
RUN set -eux; \
    curl -fsSL https://tailscale.com/install.sh | sh; \
    rm -rf /var/lib/apt/lists/*; \
    mkdir -p /var/lib/tailscale /run/tailscale

# Entrypoint
COPY entrypoint.sh /usr/local/bin/entrypoint.sh
RUN set -eux; \
    dos2unix /usr/local/bin/entrypoint.sh; \
    chmod +x /usr/local/bin/entrypoint.sh

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
