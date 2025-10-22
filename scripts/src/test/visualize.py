#!/usr/bin/env python3
from __future__ import annotations

import json
from typing import Sequence, cast

from common.classes import PinholeIntrinsics, PointCloudPoint, Transform
from scipy.spatial.transform import Rotation


def generate_visualization(
    point_cloud: Sequence[PointCloudPoint],
    reconstruction_image_poses: Sequence[Transform],
    localization: Transform,
    intrinsics: PinholeIntrinsics,
):
    axes = dict(
        visible=True,
        showbackground=False,
        showgrid=True,
        gridcolor="#2a2f3a",
        showline=True,
        linewidth=1,
        tickmode="linear",
        tick0=0.0,
        dtick=1.0,
        color="#aab2bf",  # axis tick/line color for dark mode
    )

    localization_x, localization_y, localization_z = _frustum_coords(localization, intrinsics, size=0.6)

    image_poses_x_all: list[float | None] = []
    image_poses_y_all: list[float | None] = []
    image_poses_z_all: list[float | None] = []

    for pose in reconstruction_image_poses:
        x_coords, y_coords, z_coords = _frustum_coords(pose, intrinsics, size=0.45)
        image_poses_x_all += x_coords
        image_poses_y_all += y_coords
        image_poses_z_all += z_coords

    return f"""<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8" />
  <title>VLS Result</title>
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <script src="https://cdn.plot.ly/plotly-2.27.0.min.js"></script>
  <style>
    :root {{
      --bg: #0b0d10;
      --panel: #12151a;
      --text: #e6e9ee;
      --muted: #aab2bf;
      --grid: #2a2f3a;
      --accent: rgb(255, 200, 50);  /* localized camera */
      --accent2: rgb(120, 170, 255); /* reconstruction cameras */
    }}

    html, body {{
      height: 100%;
      margin: 0;
      background: var(--bg);
      color: var(--text);
      font-family: Arial, sans-serif;
    }}

    .info {{
      position: sticky;  /* stays at top while scrolling */
      top: 0;
      z-index: 10;
      padding: 16px 20px;
      background: var(--panel);
      border-bottom: 1px solid var(--grid);
    }}
    .info h2 {{
      margin: 0 0 8px 0;
      font-weight: 600;
    }}
    .info p {{
      margin: 4px 0;
      color: var(--muted);
    }}

    #plot {{
      width: 100vw;           /* span full width */
      height: 100vh;          /* JS will shrink this by header height */
      display: block;
    }}
  </style>
</head>
<body>
  <div class="info">
    <h2>Localization</h2>
    <p><strong>Position:</strong> [{localization["position"]["x"]:.4f}, {localization["position"]["y"]:.4f}, {
        localization["position"]["z"]:.4f}]</p>
    <p><strong>Quaternion:</strong> [{localization["rotation"]["w"]:.4f}, {localization["rotation"]["x"]:.4f}, {
        localization["rotation"]["y"]:.4f}, {localization["rotation"]["z"]:.4f}]</p>
    <p><strong>Points:</strong> {len(point_cloud)}</p>
    <p><strong>Reconstruction Cameras:</strong> {len(reconstruction_image_poses)}</p>
  </div>
  <div id="plot"></div>
  <script>
    var data = {
        json.dumps([
            {
                "type": "scatter3d",
                "mode": "markers",
                "x": [float(point["position"]["x"]) for point in point_cloud],
                "y": [float(point["position"]["y"]) for point in point_cloud],
                "z": [float(point["position"]["z"]) for point in point_cloud],
                "marker": {
                    "size": 1.8,
                    "color": [
                        f"rgb({int(point['color']['r'])},{int(point['color']['g'])},{int(point['color']['b'])})"
                        for point in point_cloud
                    ],
                    "opacity": 0.95,
                },
                "name": "Point Cloud",
                "showlegend": True,
            },
            _trace(
                x_coords=image_poses_x_all,
                y_coords=image_poses_y_all,
                z_coords=image_poses_z_all,
                color="rgb(50,50,150)",
                width=1,
                name="Reconstruction Cameras",
            ),
            _trace(
                x_coords=localization_x,
                y_coords=localization_y,
                z_coords=localization_z,
                color="rgb(50,220,70)",
                width=3,
                name="Localized Camera",
            ),
        ])
    };

    var layout = {
        json.dumps({
            "scene": {
                "xaxis": {**axes, "title": "X (m)"},
                "yaxis": {**axes, "title": "Y (m)"},
                "zaxis": {**axes, "title": "Z (m)"},
                "aspectmode": "data",
            },
            "paper_bgcolor": "#0b0d10",
            "plot_bgcolor": "#0b0d10",
            "font": {"color": "#e6e9ee"},
            "title": "Visual Localization Result",
            "showlegend": True,
            "legend": {"bgcolor": "#12151a", "bordercolor": "#2a2f3a"},
        })
    };

    function sizePlot() {{
      var header = document.querySelector('.info');
      var plot = document.getElementById('plot');
      var headerH = header ? header.offsetHeight : 0;
      var newH = window.innerHeight - headerH;
      if (newH < 200) newH = 200;
      plot.style.height = newH + 'px';
      if (window.Plotly) {{
        Plotly.relayout('plot', {{height: newH, width: window.innerWidth}});
      }}
    }}

    window.addEventListener('resize', sizePlot);
    document.addEventListener('DOMContentLoaded', function() {{
      Plotly.newPlot('plot', data, layout).then(sizePlot);
    }});
  </script>
</body>
</html>"""


def _trace(
    x_coords: list[float | None],
    y_coords: list[float | None],
    z_coords: list[float | None],
    color: str = "rgb(50,220,70)",
    width: int = 3,
    name: str = "Localized Camera",
):
    return {
        "type": "scatter3d",
        "mode": "lines",
        "x": x_coords,
        "y": y_coords,
        "z": z_coords,
        "line": {"color": color, "width": width},
        "name": name,
        "showlegend": True,
    }


def _frustum_coords(pose: Transform, intrinsics: PinholeIntrinsics, size: float):
    px = pose["position"]["x"]
    py = pose["position"]["y"]
    pz = pose["position"]["z"]

    R = cast(
        list[list[float]],
        Rotation.from_quat([pose["rotation"]["x"], pose["rotation"]["y"], pose["rotation"]["z"], pose["rotation"]["w"]])
        .as_matrix()
        .tolist(),
    )

    w, h = float(intrinsics["width"]), float(intrinsics["height"])
    fx, fy, cx, cy = intrinsics["fx"], intrinsics["fy"], intrinsics["cx"], intrinsics["cy"]

    # image corners in pixel coords
    corners_2d = [(0.0, 0.0), (w, 0.0), (w, h), (0.0, h)]

    # back-project to a small "depth" (size) in camera frame, then to world
    corners_3d: list[list[float]] = []
    for u, v in corners_2d:
        x = (u - cx) / fx * size
        y = (v - cy) / fy * size
        z = size
        xw = R[0][0] * x + R[0][1] * y + R[0][2] * z + px
        yw = R[1][0] * x + R[1][1] * y + R[1][2] * z + py
        zw = R[2][0] * x + R[2][1] * y + R[2][2] * z + pz
        corners_3d.append([xw, yw, zw])

    x_coords: list[float | None] = []
    y_coords: list[float | None] = []
    z_coords: list[float | None] = []

    # rays from camera center to each corner
    for cx3d, cy3d, cz3d in corners_3d:
        x_coords += [px, cx3d, None]
        y_coords += [py, cy3d, None]
        z_coords += [pz, cz3d, None]

    # rectangle loop on the far plane
    for i in range(4):
        j = (i + 1) % 4
        ax, ay, az = corners_3d[i]
        bx, by, bz = corners_3d[j]
        x_coords += [ax, bx, None]
        y_coords += [ay, by, None]
        z_coords += [az, bz, None]

    return x_coords, y_coords, z_coords
