import json

import pulumi_aws as aws
from pulumi import Output


def create_gateway(lambda_function: aws.lambda_.Function) -> Output[str]:
    api = aws.apigatewayv2.Api(resource_name="httpApi", protocol_type="HTTP")

    integration = aws.apigatewayv2.Integration(
        resource_name="lambdaProxyIntegration",
        api_id=api.id,
        integration_type="AWS_PROXY",
        integration_uri=lambda_function.invoke_arn,
        payload_format_version="2.0",
    )

    aws.apigatewayv2.Route(
        resource_name="catchAllRoute",
        api_id=api.id,
        route_key="$default",
        target=integration.id.apply(lambda iid: f"integrations/{iid}"),
    )

    log_group = aws.cloudwatch.LogGroup("httpApiLogs", retention_in_days=7)

    aws.apigatewayv2.Stage(
        resource_name="defaultStage",
        api_id=api.id,
        name="$default",
        auto_deploy=True,
        access_log_settings=aws.apigatewayv2.StageAccessLogSettingsArgs(
            destination_arn=log_group.arn,
            format=json.dumps({
                "requestId": "$context.requestId",
                "routeKey": "$context.routeKey",
                "status": "$context.status",
                "errorMessage": "$context.error.message",
                "integrationError": "$context.integration.error",
                "latency": "$context.integrationLatency",
            }),
        ),
    )

    # ← NEW: allow API Gateway to invoke the Lambda
    aws.lambda_.Permission(
        resource_name="apiGatewayPermission",
        action="lambda:InvokeFunction",
        function=lambda_function.name,
        principal="apigateway.amazonaws.com",
        source_arn=api.execution_arn.apply(lambda arn: f"{arn}/*/*"),
    )

    return api.api_endpoint
