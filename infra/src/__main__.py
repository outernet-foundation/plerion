import pulumi
from pulumi import Config

from components.api_gateway import create_http_api
from components.database import create_database
from components.lambdas import create_api_lambda
from components.network import create_security_groups
from components.storage import create_captures_bucket
from utils import get_default_subnet_ids

# Stack config (region comes from pulumi config aws:region)
config = Config()

# 1. S3 bucket (captures)
captures_bucket = create_captures_bucket(config)

# 2. Security groups


db_sg, lambda_sg = create_security_groups()

# 2. Postgres database
postgres_instance, connection_string = create_database(config, db_sg)

subnet_ids = get_default_subnet_ids()

# 3. Lambda (container image)
api_lambda = create_api_lambda(
    config,
    environment_vars={
        # Pulumi Inputs are OK here; secrets stay secret
        "POSTGRES_DSN": connection_string,
        "CAPTURES_BUCKET": captures_bucket.bucket,
    },
    s3_bucket_arn=captures_bucket.arn,
    vpc_subnet_ids=subnet_ids,
    vpc_security_group_ids=[lambda_sg.id],
)

# 4. API Gateway → Lambda proxy
api_endpoint_output = create_http_api(api_lambda)

# 5. Stack outputs
pulumi.export("apiUrl", api_endpoint_output)
pulumi.export("capturesBucketId", captures_bucket.id)
pulumi.export("dbEndpointAddress", postgres_instance.address)
