#!/usr/bin/env bash
set -euo pipefail

cd /app/services/api

UVICORN_ARGS=( "src.main:app" "--host" "0.0.0.0" "--port" "8000" )

if [[ "${RELOAD:-}" == "true" ]]; then
  UVICORN_ARGS+=( "--reload" "--reload-dir" "/app/services/api" "--reload-dir" "/app/common" "--reload-include" "**/*.py" )
fi

if [[ "${DEBUG:-}" == "true" ]]; then
  echo "Debug mode enabled"
  DEBUG_WAIT_FLAG=()
  if [[ "${DEBUG_WAIT:-}" == "true" ]]; then
    echo "Waiting for debugger to attach"
    DEBUG_WAIT_FLAG+=( "--wait-for-client" )
  fi
  exec uv run --no-sync python -Xfrozen_modules=off -m debugpy --listen 0.0.0.0:5678 "${DEBUG_WAIT_FLAG[@]}" -m uvicorn "${UVICORN_ARGS[@]}"
else
  echo "Debug mode disabled"
  exec uv run --no-sync uvicorn "${UVICORN_ARGS[@]}"
fi
