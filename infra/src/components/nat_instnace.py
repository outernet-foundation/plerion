from __future__ import annotations

import pulumi_aws as aws
from pulumi import ComponentResource, ResourceOptions
from pulumi_aws.ec2 import get_route_table_output

from components.security_group import SecurityGroup
from components.vpc import Vpc

user_data = """
#!/bin/bash
set -eux

# Enable IPv4 forwarding permanently
echo 'net.ipv4.ip_forward = 1' > /etc/sysctl.d/99-nat.conf
sysctl --system

# Install and enable firewalld, then turn on NAT (MASQUERADE) permanently
dnf -y install firewalld
systemctl enable --now firewalld
firewall-cmd --permanent --add-masquerade
firewall-cmd --reload
"""


class NatInstance(ComponentResource):
    def __init__(self, resource_name: str, vpc: Vpc, *, opts: ResourceOptions | None = None) -> None:
        super().__init__("custom:NatInstance", resource_name, opts=opts)

        self._resource_name = resource_name
        self._child_opts = ResourceOptions.merge(opts, ResourceOptions(parent=self))

        # Security groups
        nat_sg = SecurityGroup(
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

        # AMI
        al2023_ami = aws.ec2.get_ami_output(
            most_recent=True,
            owners=["137112412989"],  # Amazon
            filters=[
                aws.ec2.GetAmiFilterArgs(name="name", values=["al2023-ami-*-kernel-6.1-arm64"]),
                aws.ec2.GetAmiFilterArgs(name="state", values=["available"]),
            ],
        )

        # EC2 Instance
        instance = aws.ec2.Instance(
            f"{resource_name}-instance",
            ami=al2023_ami.id,
            instance_type="t4g.nano",
            subnet_id=vpc.public_subnet_ids.apply(lambda ids: ids[0]),
            vpc_security_group_ids=[nat_sg.id],
            associate_public_ip_address=True,
            source_dest_check=False,
            user_data=user_data,
            metadata_options=aws.ec2.InstanceMetadataOptionsArgs(http_tokens="required"),
            opts=self._child_opts,
        )

        # Elastic IP
        eip = aws.ec2.Eip(f"{resource_name}-elastic-ip", domain="vpc", opts=self._child_opts)
        aws.ec2.EipAssociation(
            f"{resource_name}-elastic-ip-association",
            instance_id=instance.id,
            allocation_id=eip.id,
            opts=self._child_opts,
        )

        # Outputs
        self.instance_id = instance.id
        self.eip_allocation_id = eip.id
        self.security_group_id = nat_sg.id
        self.routes = vpc.private_subnet_ids.apply(
            lambda ids: [
                aws.ec2.Route(
                    f"{resource_name}-route-{i}",
                    aws.ec2.RouteArgs(
                        route_table_id=get_route_table_output(subnet_id=sid).id,
                        destination_cidr_block="0.0.0.0/0",
                        network_interface_id=instance.primary_network_interface_id,
                    ),
                    opts=self._child_opts,
                )
                for i, sid in enumerate(ids)
            ]
        )

        self.register_outputs({
            "instance_id": self.instance_id,
            "eip_allocation_id": self.eip_allocation_id,
            "security_group_id": self.security_group_id,
        })
