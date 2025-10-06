#!/usr/bin/env bash
set -euo pipefail

cd /app/services/visual-localization-system/run-reconstruction

echo "Starting run-reconstruction"

if [[ "${DEBUG:-}" == "true" ]]; then
  echo "Debug mode enabled"
  DEBUG_WAIT_FLAG=()
  if [[ "${DEBUG_WAIT:-}" == "true" ]]; then
    echo "Waiting for debugger to attach"
    DEBUG_WAIT_FLAG+=( "--wait-for-client" )
  fi
  exec uv run --no-sync python -Xfrozen_modules=off -m debugpy --listen 0.0.0.0:5678 "${DEBUG_WAIT_FLAG[@]}" run-reconstruction
else
  exec uv run --no-sync run-reconstruction
fi