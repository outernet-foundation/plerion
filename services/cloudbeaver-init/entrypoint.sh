#!/usr/bin/env bash
set -euo pipefail

# --- Required environment variables ---
: "${POSTGRES_HOST}"
: "${POSTGRES_DB}"
: "${POSTGRES_USER}"
: "${POSTGRES_PASSWORD}"
: "${CB_ADMIN_NAME}"
: "${CB_ADMIN_PASSWORD}"

# --- Defaults if not set ---
: "${POSTGRES_PORT:=5432}"
: "${CLOUDBEAVER_DB_SCHEMA:=cloudbeaver}"

WORKSPACE="/opt/cloudbeaver/workspace"
MARKER="$WORKSPACE/.data/.admin.seed"
CONFIG_DIR="$WORKSPACE/GlobalConfiguration/.dbeaver"

# --- Compute hash of admin credentials ---
HASH="$(printf '%s:%s' "$CB_ADMIN_NAME" "$CB_ADMIN_PASSWORD" | sha256sum | awk '{print $1}')"
CURRENT="$(cat "$MARKER" 2>/dev/null || true)"

if [[ "$HASH" != "$CURRENT" ]]; then
  echo "🔄 Admin changed (or first run); FULL reset of CloudBeaver metadata…"

  export PGPASSWORD="${POSTGRES_PASSWORD}"

  # Drop CloudBeaver schema (safe even if not present)
  psql -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" \
       -v ON_ERROR_STOP=1 \
       -c "DROP SCHEMA IF EXISTS ${CLOUDBEAVER_DB_SCHEMA} CASCADE;"

  # Wipe EFS runtime/config and recreate dirs
  rm -rf "${WORKSPACE}/.data" "${CONFIG_DIR}"
  mkdir -p "${CONFIG_DIR}" "${WORKSPACE}/.data"

  # Seed provided connection so it shows up immediately
  cat > "${CONFIG_DIR}/provided-connections.json" <<EOF
{
  "folders": {},
  "connections": {
    "plerion": {
      "provider": "postgresql",
      "driver": "postgres-jdbc",
      "name": "Plerion",
      "save-password": true,
      "configuration": {
        "host": "${POSTGRES_HOST}",
        "port": "${POSTGRES_PORT}",
        "database": "${POSTGRES_DB}",
        "user": "${POSTGRES_USER}",
        "password": "${POSTGRES_PASSWORD}"
      }
    }
  }
}
EOF
  echo "📄 provided-connections.json written."

  # Update marker
  echo "$HASH" > "$MARKER"
else
  echo "✅ Admin credentials unchanged; no reset needed."
fi

echo "Init sidecar complete."
