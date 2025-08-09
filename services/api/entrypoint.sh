#!/usr/bin/env bash
set -euo pipefail

uv run uvicorn src.main:app --host 0.0.0.0 --port 8000