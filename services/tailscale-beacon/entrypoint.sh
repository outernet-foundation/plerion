#!/usr/bin/env bash
set -euo pipefail

# Required env vars
: "${TS_AUTHKEY?}"
: "${CADDYFILE?}"

# Start tailscale daemon
nohup tailscaled \
  --state=/var/lib/tailscale/tailscaled.state \
  --socket=/run/tailscale/tailscaled.sock \
  --tun=userspace-networking \
  >/var/log/tailscaled.log 2>&1 &

# Start tailscale node and wait for it to connect
tailscale --socket=/run/tailscale/tailscaled.sock up \
  --authkey="${TS_AUTHKEY}" \
  --hostname=beacon \
  --accept-dns=true \
  --timeout=30s

# Start Caddy
echo "${CADDYFILE}" > /etc/caddy/Caddyfile
exec caddy run --config /etc/caddy/Caddyfile
