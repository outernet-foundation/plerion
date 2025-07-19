import pulumi
import pulumi_aws as aws
from pulumi import Config


def create_storage(config: Config) -> aws.s3.Bucket:
    bucket_name: str = config.require("s3BucketName")
    bucket = aws.s3.Bucket(
        resource_name="capturesBucket",
        bucket=bucket_name,
        acl="private",
        force_destroy=True,
    )

    pulumi.export("capturesBucketName", bucket.bucket)
    return bucket
