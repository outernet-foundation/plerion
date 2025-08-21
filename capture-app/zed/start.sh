#!/usr/bin/env bash
rm -rf .venv
uv run uvicorn src.main:app --host 0.0.0.0 --port 9000