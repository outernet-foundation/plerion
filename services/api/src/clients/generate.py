#!/usr/bin/env python3
import os
from pathlib import Path

from common.openapi import generate

# Prevents pydantic settings from running validation and piccolo from trying to connect to the database
os.environ["CODEGEN"] = "1"

from ..main import app

if __name__ == "__main__":
    generate(
        app, Path(__file__).resolve().parent, Path(__file__).resolve().parent / "unity"
    )
