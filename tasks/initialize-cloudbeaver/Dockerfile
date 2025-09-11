# Has psql + bash preinstalled
FROM docker.io/library/postgres:16-bookworm

# Copy init script
COPY entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

# (Optional) normalize line endings if you edit on Windows
RUN apt-get update && apt-get install -y --no-install-recommends dos2unix \
    && dos2unix /usr/local/bin/entrypoint.sh && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
