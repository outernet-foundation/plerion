from typing import List

from pulumi_aws.ec2 import SecurityGroupEgressArgs

ALLOW_ALL_EGRESS: List[SecurityGroupEgressArgs] = [
    SecurityGroupEgressArgs(
        protocol="-1",
        from_port=0,
        to_port=0,
        cidr_blocks=["0.0.0.0/0"],
    )
]
