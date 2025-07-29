# services/automigrate/Dockerfile.migrator
FROM python:3.12-slim

# 1) Build a venv in /opt/venv and install ALL the CLIs you need
ENV VENV_DIR=/opt/venv
RUN python -m venv $VENV_DIR \
 && $VENV_DIR/bin/pip install --upgrade pip \
 # install Piccolo CLI, the uv wrapper, and watchdog (which provides watchmedo)
 && $VENV_DIR/bin/pip install --no-cache-dir piccolo "watchdog[watchmedo]"

# 2) Put the venv’s bin/ on your PATH
ENV PATH="$VENV_DIR/bin:$PATH"

# 3) Copy your code so migrations exist at build time
WORKDIR /workspace/app
COPY api/ /workspace/app/

# 4) (No CMD here; you’re using `command:` with sh -c in docker-compose.yml)
