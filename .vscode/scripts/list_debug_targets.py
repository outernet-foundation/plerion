#!/usr/bin/env python3
import json, os, subprocess, sys

service_filter = sys.argv[1] if len(sys.argv) > 1 else None

fmt = '{{json .ID}} {{json .Names}} {{json .Labels}}'
ps = subprocess.check_output(
    ["bash", "-lc", f"docker ps --filter label=plerion.debug=1 --format '{fmt}'"],
    text=True,
)

lines = []
for raw in ps.splitlines():
    cid_str, name_str, labels_str = raw.split(" ", 2)
    cid   = json.loads(cid_str)
    name  = json.loads(name_str)
    labels = json.loads(labels_str)

    svc = labels.get("plerion.service", "")
    if service_filter and svc != service_filter:
        continue

    # inspect to find the host port bound to container port 5678/tcp
    insp = subprocess.check_output(["docker", "inspect", cid], text=True)
    info = json.loads(insp)[0]
    ports = (info.get("NetworkSettings", {}).get("Ports") or {}).get("5678/tcp") or []
    host_port = ports[0]["HostPort"] if ports else None
    if not host_port:
        continue

    label = f"{name} ({svc}) : {host_port}"
    value = host_port   # this is what the QuickPick returns
    lines.append(f"{label}|{value}")

print("\n".join(lines))
