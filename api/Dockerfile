# Use the official uv image with Python 3.11
FROM ghcr.io/astral-sh/uv:python3.13-bookworm-slim

# Set environment variables for optimization
ENV UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy \
    PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1

# Create app directory
WORKDIR /app

# Create a non-root user for security
RUN groupadd -r app && useradd -r -g app app

# Copy workspace dependency files from root
COPY --chown=app:app pyproject.toml uv.lock ./

# Copy only the API code (not the entire workspace)
COPY --chown=app:app api/ ./api/

# Install dependencies without the project itself
RUN --mount=type=cache,target=/root/.cache/uv \
    uv sync --all-packages --locked --no-install-project

# Install the project itself
RUN --mount=type=cache,target=/root/.cache/uv \
    uv sync --locked

# Switch to non-root user
USER app

# Set working directory to where the API code is
WORKDIR /app/api

# Default command
CMD ["uv", "run", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]