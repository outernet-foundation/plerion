from pulumi import Config
from pulumi_aws.ecs import Cluster
from pulumi_aws.lb import Listener, ListenerRule, LoadBalancer, TargetGroup
from pulumi_aws.route53 import Record
from pulumi_awsx.ecs import FargateService

from components.security_group import SecurityGroup
from components.vpc import Vpc


def create_tailscale_bridge(config: Config, vpc: Vpc, zone_id: str, domain: str, certificate_arn: str):
    people: list[str] = config.require_object("people")

    cluster = Cluster("tailscale-bridge-cluster")

    load_balancer_security_group = SecurityGroup("tailscale-bridge-load-balancer-security-group", vpc_id=vpc.id)
    tailscale_bridge_security_group = SecurityGroup("tailscale-bridge-security-group", vpc_id=vpc.id)

    # Allow http/https ingress to the load balancer from anywhere
    load_balancer_security_group.allow_ingress_cidr(cidr_name="anywhere", cidr="0.0.0.0/0", ports=[80], protocol="tcp")
    load_balancer_security_group.allow_ingress_cidr(cidr_name="anywhere", cidr="0.0.0.0/0", ports=[443], protocol="tcp")

    # Allow the load balancer to access the Tailscale bridge
    tailscale_bridge_security_group.allow_ingress_reciprocal(
        from_security_group=load_balancer_security_group, ports=[80]
    )

    # Allow egress to the VPC CIDR for DNS resolution
    tailscale_bridge_security_group.allow_egress_cidr(cidr_name="vpc", cidr=vpc.cidr_block, ports=[53])
    tailscale_bridge_security_group.allow_egress_cidr(cidr_name="vpc", cidr=vpc.cidr_block, ports=[53], protocol="udp")

    # Allow egress to the tailscale control plane
    tailscale_bridge_security_group.allow_egress_cidr(
        cidr_name="tailscale-control-plane", cidr="0.0.0.0/0", ports=[443]
    )

    # Allow egress to the tailscale DERP servers
    tailscale_bridge_security_group.allow_egress_cidr(
        cidr_name="tailscale-derp", cidr="0.0.0.0/0", ports=[41641], protocol="udp"
    )

    # Create a load balancer for the Tailscale bridge
    load_balancer = LoadBalancer(
        "tailscale-bridge-lb", security_groups=[load_balancer_security_group.id], subnets=vpc.public_subnet_ids
    )

    # Minimal HTTPS listener with a 404 fixed‑response default; real routing is
    # added via one ListenerRule per engineer later.
    https_listener = Listener(
        "tailscale-bridge-https-listener",
        load_balancer_arn=load_balancer.arn,
        port=443,
        protocol="HTTPS",
        certificate_arn=certificate_arn,
        default_actions=[
            {"type": "fixed-response", "fixed_response": {"content_type": "text/plain", "status_code": "404"}}
        ],
    )

    # Optional HTTP → HTTPS redirect (kept very simple).
    Listener(
        "tailscale-bridge-http-listener",
        load_balancer_arn=load_balancer.arn,
        port=80,
        protocol="HTTP",
        default_actions=[
            {"type": "redirect", "redirect": {"protocol": "HTTPS", "port": "443", "status_code": "HTTP_301"}}
        ],
    )

    local_services = {"cloudbeaver": 8978, "minio": 9000, "minioconsole": 9001, "api": 8000}

    for name in people:
        ip = config.require(f"{name}:ip")
        auth_key = config.require_secret(f"{name}:auth-key")

        target_group = TargetGroup(
            f"tailscale-bridge-{name}-target-group",
            port=80,
            protocol="HTTP",
            target_type="ip",
            vpc_id=load_balancer.vpc_id,
            health_check={"path": "/"},  # simple quick check
        )

        ListenerRule(
            f"rule-{name}",
            listener_arn=https_listener.arn,
            actions=[{"type": "forward", "target_group_arn": target_group.arn}],
            conditions=[{"host_header": {"values": [f"*.{name}.{domain}"]}}],
        )

        for local_service in local_services:
            Record(
                f"{local_service}-{name}",
                zone_id=zone_id,
                name=f"{local_service}.{name}.{domain}",
                type="A",
                aliases=[
                    {"name": load_balancer.dns_name, "zone_id": load_balancer.zone_id, "evaluate_target_health": False}
                ],
            )

        FargateService(
            f"tailscale-bridge-{name}-service",
            cluster=cluster.arn,
            desired_count=1,
            network_configuration={
                "subnets": vpc.private_subnet_ids,
                "security_groups": [tailscale_bridge_security_group.id],
            },
            task_definition_args={
                "containers": {
                    "tailscale": {
                        "name": "tailscale",
                        "image": "tailscale/tailscale:stable",
                        "command": [
                            "/bin/sh",
                            "-c",
                            "tailscaled --tun=userspace-networking & "
                            f"tailscale up --hostname=bridge-{name} --authkey={auth_key} --reset && "
                            "sleep infinity",
                        ],
                    },
                    "caddy": {
                        "name": "caddy",
                        "image": "caddy:2",
                        "port_mappings": [{"container_port": 80, "host_port": 80, "target_group": target_group}],
                        "command": [
                            "/bin/sh",
                            "-c",
                            "cat >/etc/caddy/Caddyfile <<'EOF'\n"
                            + "\n".join(
                                f"{sub}.{name}.{domain} {{ reverse_proxy {ip}:{port} }}"
                                for sub, port in local_services.items()
                            )
                            + "\nEOF\n"
                            + "caddy run --adapter caddyfile --config /etc/caddy/Caddyfile",
                        ],
                    },
                }
            },
        )
