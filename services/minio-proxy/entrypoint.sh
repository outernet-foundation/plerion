#!/usr/bin/env bash

# Generate Caddyfile from environment variables
cat > /etc/caddy/Caddyfile <<EOF
:9000 {
    reverse_proxy minio:9000 {
        header_up Host ${DEVICE_NAME}-minio.${DOMAIN}
    }
}
EOF

echo "Starting MinIO proxy with Caddy..."

# Start Caddy
exec caddy run --config /etc/caddy/Caddyfile --adapter caddyfile