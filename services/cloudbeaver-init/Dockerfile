FROM alpine:latest

COPY cloudbeaver-init.sh /cloudbeaver-init.sh
RUN chmod +x /cloudbeaver-init.sh

ENTRYPOINT ["/cloudbeaver-init.sh"]