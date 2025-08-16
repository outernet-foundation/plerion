from __future__ import annotations

import json

from pulumi import ComponentResource, ResourceOptions
from pulumi_aws.ec2 import Eip, EipAssociation, Instance, Route, RouteArgs, get_route_table_output
from pulumi_aws.iam import InstanceProfile, Role, RolePolicyAttachment
from pulumi_aws.ssm import get_parameter_output

from components.security_group import SecurityGroup
from components.vpc import Vpc

# https://docs.aws.amazon.com/vpc/latest/userguide/work-with-nat-instances.html
user_data = """
#!/bin/bash
set -eux

echo 'net.ipv4.ip_forward=1' >/etc/sysctl.d/99-nat.conf
sysctl --system

yum -y install iptables-services
systemctl enable --now iptables

OUT_IF="$(ip route show default | awk '{print $5; exit}')"

# NAT rule
iptables -C -t nat POSTROUTING -o "$OUT_IF" -j MASQUERADE 2>/dev/null || \
  iptables -t nat -A POSTROUTING -o "$OUT_IF" -j MASQUERADE

# Reset FORWARD and add in correct order
iptables -F FORWARD
iptables -A FORWARD -m conntrack --ctstate RELATED,ESTABLISHED -j ACCEPT
iptables -A FORWARD -o "$OUT_IF" -j ACCEPT

# (optional) explicit final reject for visibility
# iptables -A FORWARD -j REJECT --reject-with icmp-host-prohibited

service iptables save
"""


class NatInstance(ComponentResource):
    def __init__(self, resource_name: str, vpc: Vpc, *, opts: ResourceOptions | None = None) -> None:
        super().__init__("custom:NatInstance", resource_name, opts=opts)

        self._resource_name = resource_name
        self._child_opts = ResourceOptions.merge(opts, ResourceOptions(parent=self))

        # Security groups
        security_group = SecurityGroup(
            f"{resource_name}-security-group",
            vpc=vpc,
            rules=[
                {
                    "cidr_name": "vpc",
                    "from_cidr": vpc.cidr_block,
                    "ports": [0],
                    "protocols": ["-1"],
                },  # Allow ingress from VPC over all protocols
                {
                    "cidr_name": "anywhere",
                    "to_cidr": "0.0.0.0/0",
                    "ports": [0],
                    "protocols": ["-1"],
                },  # Allow egress to anywhere over all protocols
            ],
            opts=self._child_opts,
        )

        # ROL
        role = Role(
            f"{resource_name}-role",
            assume_role_policy=json.dumps({
                "Version": "2012-10-17",
                "Statement": [
                    {"Effect": "Allow", "Action": "sts:AssumeRole", "Principal": {"Service": "ec2.amazonaws.com"}}
                ],
            }),
            opts=self._child_opts,
        )
        RolePolicyAttachment(
            f"{resource_name}-cwagent",
            role=role.name,
            policy_arn="arn:aws:iam::aws:policy/CloudWatchAgentServerPolicy",
            opts=self._child_opts,
        )
        RolePolicyAttachment(
            f"{resource_name}-ssm",
            role=role.name,
            policy_arn="arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore",
            opts=self._child_opts,
        )
        profile = InstanceProfile(f"{resource_name}-instance-profile", role=role.name, opts=self._child_opts)

        # EC2 Instance
        instance = Instance(
            f"{resource_name}-instance",
            ami=get_parameter_output(
                name="/aws/service/ami-amazon-linux-latest/amzn2-ami-kernel-5.10-hvm-arm64-gp2"
            ).value,
            iam_instance_profile=profile.name,
            instance_type="t4g.nano",
            subnet_id=vpc.public_subnet_ids.apply(lambda ids: ids[0]),
            vpc_security_group_ids=[security_group.id],
            associate_public_ip_address=True,
            source_dest_check=False,
            user_data=user_data,
            metadata_options={"http_tokens": "required"},
            opts=self._child_opts,
        )

        # Elastic IP
        eip = Eip(f"{resource_name}-elastic-ip", domain="vpc", opts=self._child_opts)
        EipAssociation(
            f"{resource_name}-elastic-ip-association",
            instance_id=instance.id,
            allocation_id=eip.id,
            opts=self._child_opts,
        )

        # Routes
        vpc.private_subnet_ids.apply(
            lambda ids: [
                Route(
                    f"{resource_name}-route-{i}",
                    RouteArgs(
                        route_table_id=get_route_table_output(subnet_id=sid).id,
                        destination_cidr_block="0.0.0.0/0",
                        network_interface_id=instance.primary_network_interface_id,
                    ),
                    opts=self._child_opts,
                )
                for i, sid in enumerate(ids)
            ]
        )

        # Outputs
        self.instance_id = instance.id
        self.eip_allocation_id = eip.id
        self.security_group_id = security_group.id

        self.register_outputs({
            "instance_id": self.instance_id,
            "eip_allocation_id": self.eip_allocation_id,
            "security_group_id": self.security_group_id,
        })
