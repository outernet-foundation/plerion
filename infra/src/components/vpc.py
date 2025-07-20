import pulumi_aws.ec2 as ec2
import pulumi_awsx as awsx

from util import ALLOW_ALL_EGRESS


def create_vpc() -> tuple[
    awsx.ec2.Vpc,
    ec2.SecurityGroup,
    ec2.SecurityGroup,
]:
    # Create a VPC with default subnet layout (public + private in each AZ)
    vpc = awsx.ec2.Vpc(
        "main-vpc",
        nat_gateways={
            "strategy": awsx.ec2.NatGatewayStrategy.SINGLE,
        },
    )

    # Security group for Lambda functions: no inbound, all outbound
    lambda_sg = ec2.SecurityGroup(
        "lambda-sg",
        vpc_id=vpc.vpc_id,
        egress=ALLOW_ALL_EGRESS,
    )

    # Security group for RDS: allow Lambda SG on port 5432, all outbound
    db_sg = ec2.SecurityGroup(
        "db-sg",
        vpc_id=vpc.vpc_id,
        ingress=[
            {
                "protocol": "tcp",
                "from_port": 5432,
                "to_port": 5432,
                "security_groups": [lambda_sg.id],
            }
        ],
        egress=ALLOW_ALL_EGRESS,
    )

    return (
        vpc,
        lambda_sg,
        db_sg,
    )
