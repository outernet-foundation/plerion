#!/usr/bin/env python3

import argparse
import json
import subprocess
import sys
import tempfile
from datetime import datetime, timedelta
from typing import Any, Dict, List

import boto3
from mypy_boto3_ce.client import CostExplorerClient


def main():
    parser = argparse.ArgumentParser(description="AWS Cost Explorer to Graph")
    parser.add_argument("--days", "-d", type=int, default=30, help="Number of days back to fetch (default: 30)")
    parser.add_argument(
        "--granularity",
        "-g",
        choices=["HOURLY", "DAILY", "MONTHLY"],
        default="DAILY",
        help="Data granularity (default: DAILY, HOURLY only available for last 14 days)",
    )
    parser.add_argument(
        "--group-by",
        "-gb",
        choices=["SERVICE", "USAGE_TYPE", "OPERATION"],
        default="SERVICE",
        help="Group data by (default: SERVICE)",
    )
    parser.add_argument("--service-filter", "-s", type=str, help='Filter by specific service (e.g., "EC2-Other")')
    parser.add_argument("--output", "-o", type=str, default="costs", help="Output filename prefix (default: costs)")
    parser.add_argument("--width", type=int, default=800, help="Chart width (default: 800)")
    parser.add_argument("--height", type=int, default=400, help="Chart height (default: 400)")
    parser.add_argument(
        "--enable-resource-ids",
        action="store_true",
        help="Enable resource-level data (WARNING: costs $0.01 per API call)",
    )
    parser.add_argument(
        "--usage-type-filter", type=str, help='Filter by specific usage type (e.g., "NatGateway-Hours")'
    )
    parser.add_argument("--region-filter", type=str, help='Filter by specific region (e.g., "us-east-1")')
    parser.add_argument("--json-only", action="store_true", help="Only output JSON data, skip graph generation")

    args = parser.parse_args()

    # Calculate date range and validate for hourly
    if args.granularity == "HOURLY":
        # Hourly requires timestamp format and max 14 days
        if args.days > 14:
            print("⚠️  WARNING: Hourly data only available for last 14 days. Limiting to 14 days.")
            args.days = 14

        end_datetime = datetime.now()
        start_datetime = end_datetime - timedelta(days=args.days)
        end_date = end_datetime.strftime("%Y-%m-%dT%H:%M:%SZ")
        start_date = start_datetime.strftime("%Y-%m-%dT%H:%M:%SZ")
    else:
        # Daily and Monthly use date format
        end_date = datetime.now().strftime("%Y-%m-%d")
        start_date = (datetime.now() - timedelta(days=args.days)).strftime("%Y-%m-%d")

    print(f"Fetching AWS costs from {start_date} to {end_date}...")
    print(f"Grouping by: {args.group_by}")
    if args.service_filter:
        print(f"Filtering service: {args.service_filter}")

    # Initialize AWS Cost Explorer client
    ce_client: CostExplorerClient = boto3.client("ce")  # type: ignore

    # Build the API call parameters
    call_params = {
        "TimePeriod": {"Start": start_date, "End": end_date},
        "Granularity": args.granularity,  # type: ignore
        "Metrics": ["BlendedCost"],
        "GroupBy": [{"Type": "DIMENSION", "Key": args.group_by}],
    }

    # Add filters if specified
    if args.service_filter or args.usage_type_filter or args.region_filter:
        filter_conditions: List[Dict[str, Any]] = []

        if args.service_filter:
            filter_conditions.append({"Dimensions": {"Key": "SERVICE", "Values": [args.service_filter]}})

        if args.usage_type_filter:
            filter_conditions.append({"Dimensions": {"Key": "USAGE_TYPE", "Values": [args.usage_type_filter]}})

        if args.region_filter:
            filter_conditions.append({"Dimensions": {"Key": "REGION", "Values": [args.region_filter]}})

        # Use AND logic if multiple filters
        if len(filter_conditions) == 1:
            call_params["Filter"] = filter_conditions[0]
        else:
            call_params["Filter"] = {"And": filter_conditions}

    # Fetch cost data
    if args.enable_resource_ids:
        print("📋 Fetching resource-level data...")
        response = ce_client.get_cost_and_usage(**call_params)  # type: ignore
    else:
        response = ce_client.get_cost_and_usage(**call_params)  # type: ignore

    # Transform data for vega-lite
    output: List[Dict[str, Any]] = []
    daily_totals: Dict[str, float] = {}

    for result in response["ResultsByTime"]:
        date = result.get("TimePeriod", {}).get("Start", "")
        daily_total = 0.0

        for group in result.get("Groups", []):
            category = group.get("Keys", ["Unknown"])[0] if group.get("Keys") else "Unknown"
            # Clean category name to avoid JSON issues
            category = category.replace('"', "").replace("\n", " ").strip()
            cost_data = group.get("Metrics", {}).get("BlendedCost", {})
            try:
                cost = float(cost_data.get("Amount", "0"))
            except (ValueError, TypeError):
                cost = 0.0
            if cost > 0:
                output.append({"date": date, "category": category, "cost": cost})
                daily_total += cost

        # Store daily total
        if daily_total > 0:
            daily_totals[date] = daily_total

    # Add total line to output
    for date, total_cost in daily_totals.items():
        output.append({"date": date, "category": "📊 TOTAL", "cost": total_cost})

    # Calculate estimated monthly cost from most recent period
    estimated_monthly = 0.0
    if daily_totals:
        most_recent_date = max(daily_totals.keys())
        most_recent_cost = daily_totals[most_recent_date]

        if args.granularity == "HOURLY":
            # For hourly: multiply by 24 hours, then by 30.44 days
            estimated_monthly = most_recent_cost * 24 * 30.44
            print(f"💰 Most recent hourly cost: ${most_recent_cost:.4f}")
        elif args.granularity == "DAILY":
            # For daily: multiply by 30.44 days
            estimated_monthly = most_recent_cost * 30.44
            print(f"💰 Most recent daily cost: ${most_recent_cost:.2f}")
        else:  # MONTHLY
            estimated_monthly = most_recent_cost
            print(f"💰 Most recent monthly cost: ${most_recent_cost:.2f}")

        print(f"📈 Estimated monthly cost: ${estimated_monthly:.2f}")

    # Save raw data as JSON
    json_filename = f"{args.output}_data.json"
    with open(json_filename, "w") as f:
        json.dump(output, f, indent=2)
    print(f"✅ Raw data saved to {json_filename}")

    # If json-only mode, exit here
    if args.json_only:
        print("JSON-only mode: skipping graph generation")
        return

    # Determine chart title and labels based on granularity and grouping
    granularity_labels = {"HOURLY": "Hourly", "DAILY": "Daily", "MONTHLY": "Monthly"}

    title_map = {
        "SERVICE": f"AWS {granularity_labels[args.granularity]} Costs by Service",
        "USAGE_TYPE": f"AWS {granularity_labels[args.granularity]} Costs by Usage Type",
        "OPERATION": f"AWS {granularity_labels[args.granularity]} Costs by Operation",
    }

    label_map = {"SERVICE": "AWS Service", "USAGE_TYPE": "Usage Type", "OPERATION": "API Operation"}

    chart_title = title_map.get(args.group_by, f"AWS {granularity_labels[args.granularity]} Costs")
    if args.service_filter:
        chart_title += f" ({args.service_filter})"

    color_label = label_map.get(args.group_by, "Category")

    # Vega-Lite specification
    vega_spec = {
        "data": {"values": output},
        "mark": {"type": "line", "point": True, "strokeWidth": {"expr": "datum.category === '📊 TOTAL' ? 3 : 1.5"}},
        "encoding": {
            "x": {"field": "date", "type": "temporal", "title": "Date"},
            "y": {"field": "cost", "type": "quantitative", "title": "Cost ($)"},
            "color": {"field": "category", "type": "nominal", "title": color_label},
            "strokeDash": {"condition": {"test": "datum.category === '📊 TOTAL'", "value": [0]}, "value": [5, 5]},
        },
        "width": args.width,
        "height": args.height,
        "title": {
            "text": chart_title,
            "subtitle": f"Estimated Monthly: ${estimated_monthly:.2f}" if estimated_monthly > 0 else None,
        },
        "config": {
            "legend": {
                "titleFontSize": 12,
                "labelFontSize": 10,
                "symbolStrokeWidth": 2,
                "padding": 15,
                "offset": 30,
                "labelLimit": 0,  # Remove label length limit entirely
                "symbolLimit": 0,  # Remove symbol limit
                "titleLimit": 0,  # Remove title limit
                "columnPadding": 20,  # Add space between legend columns
                "rowPadding": 3,  # Add space between legend rows
            },
            "view": {
                "continuousWidth": args.width + 250,
                "continuousHeight": args.height,
            },  # Even more width for legend
        },
    }

    # Generate graph
    with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False) as f:
        json.dump(vega_spec, f, ensure_ascii=True)
        f.flush()
        try:
            subprocess.run(
                ["C:/Users/Tyler/scoop/apps/nodejs/current/bin/vl2svg.cmd", f.name, f"{args.output}.svg"], check=True
            )
            print(f"✅ Graph saved as {args.output}.svg")
        except subprocess.CalledProcessError:
            print("❌ Error generating graph. Make sure vl2svg is installed.")
            sys.exit(1)
        except FileNotFoundError:
            print("❌ vl2svg not found. Install with: npm install -g vega-lite vega-cli")
            sys.exit(1)


if __name__ == "__main__":
    main()
