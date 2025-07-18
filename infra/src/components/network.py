import pulumi_aws as aws


def create_security_groups() -> tuple[aws.ec2.SecurityGroup, aws.ec2.SecurityGroup]:
    default_vpc = aws.ec2.get_vpc(default=True)

    # SG for Lambda (outbound all)
    lambda_sg = aws.ec2.SecurityGroup(
        "lambdaSecurityGroup",
        vpc_id=default_vpc.id,
        egress=[
            aws.ec2.SecurityGroupEgressArgs(
                protocol="-1", from_port=0, to_port=0, cidr_blocks=["0.0.0.0/0"]
            )
        ],
    )

    # SG for Postgres: allow 5432 only from Lambda SG
    db_sg = aws.ec2.SecurityGroup(
        "postgresSecurityGroup",
        vpc_id=default_vpc.id,
        ingress=[
            aws.ec2.SecurityGroupIngressArgs(
                protocol="tcp",
                from_port=5432,
                to_port=5432,
                security_groups=[lambda_sg.id],
            )
        ],
        egress=[
            aws.ec2.SecurityGroupEgressArgs(
                protocol="-1", from_port=0, to_port=0, cidr_blocks=["0.0.0.0/0"]
            )
        ],
    )

    return db_sg, lambda_sg
