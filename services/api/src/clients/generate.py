#!/usr/bin/env python3
import os
from pathlib import Path

from common.openapi import generate

os.environ["DONT_IMPORT_DB"] = "1"  # Prevents Piccolo from importing the DB config
from ..main import app

if __name__ == "__main__":
    generate(app, Path(__file__).resolve().parent)
