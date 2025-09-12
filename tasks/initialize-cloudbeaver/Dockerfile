FROM docker.io/library/postgres:16-bookworm

# Copy the entrypoint and make it executable
COPY --chmod=0755 entrypoint.sh /usr/local/bin/entrypoint.sh

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
