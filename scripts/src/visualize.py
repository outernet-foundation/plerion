# #!/usr/bin/env python3
# """
# visualize_scaled_axes.py

# Load a COLMAP/pycolmap reconstruction and show it in Plotly
# with Y = up/down, zero-planes at X=0, Y=0, Z=0, and 1 m tick spacing.
# """

# from pathlib import Path

# import numpy as np
# import pycolmap
# from hloc.utils import viz_3d

# localization_result = {
#     "position": [0.041614053474793036, 3.2161027111783107, -0.16734516180773723],
#     "orientation": [0.9892949937944427, -0.13756641504709868, -0.038327257759797596, 0.030031949935976042],
# }


# def main(model_folder: Path):
#     # Load the reconstruction
#     reconstruction = pycolmap.Reconstruction(str(model_folder))

#     fig = viz_3d.init_figure()
#     viz_3d.plot_reconstruction(fig, reconstruction, color="rgba(255,0,0,0.6)", name="metric_sfm", points_rgb=True)
#     # bb_min, bb_max = reconstruction.compute_bounding_box(0.001, 0.999)
#     axes_kwargs = dict(
#         visible=True,
#         showbackground=False,
#         showgrid=True,
#         gridcolor="gray",
#         showline=True,
#         linewidth=1,
#         tickmode="linear",
#         tick0=0.0,
#         dtick=1.0,
#         tickfont=dict(size=10),
#     )
#     # fig.update_layout(
#     #     scene=dict(
#     #         xaxis={**axes_kwargs, "range": [float(bb_min[0]), float(bb_max[0])]},
#     #         yaxis={**axes_kwargs, "range": [float(bb_min[1]), float(bb_max[1])]},
#     #         zaxis={**axes_kwargs, "range": [float(bb_min[2]), float(bb_max[2])]},
#     #         aspectmode="data",
#     #     )
#     # )

#     fx, fy, cx, cy = 800.0, 800.0, 640.0, 360.0  # <-- put your values here
#     K = np.array([[fx, 0.0, cx], [0.0, fy, cy], [0.0, 0.0, 1.0]], dtype=np.float64)
#     # C_world = np.array([0.25, 1.80, -0.35], dtype=np.float64)  # <-- your (x,y,z)
#     C_world = np.array(localization_result["position"], dtype=np.float64)  # <-- your (x,y,z)

#     def quat_to_R(qw: float, qx: float, qy: float, qz: float) -> np.ndarray:
#         s = 2.0
#         xx, yy, zz = qx * qx, qy * qy, qz * qz
#         xy, xz, yz = qx * qy, qx * qz, qy * qz
#         wx, wy, wz = qw * qx, qw * qy, qw * qz
#         return np.array(
#             [
#                 [1 - s * (yy + zz), s * (xy - wz), s * (xz + wy)],
#                 [s * (xy + wz), 1 - s * (xx + zz), s * (yz - wx)],
#                 [s * (xz - wy), s * (yz + wx), 1 - s * (xx + yy)],
#             ],
#             dtype=np.float64,
#         )

#     R_wc = quat_to_R(*localization_result["orientation"])

#     viz_3d.plot_camera(
#         fig,
#         R=R_wc,  # world-from-camera rotation
#         t=C_world,  # camera center in world
#         K=K,
#         color="rgb(50, 220, 70)",
#         name="LOCALIZED",
#         legendgroup="LOCALIZED",
#         fill=True,
#         size=1.2,  # frustum size multiplier (optional)
#         text="Localized pose",
#     )

#     fig.show()


# if __name__ == "__main__":
#     # if len(sys.argv) != 2:
#     #     print(f"Usage: {sys.argv[0]} <path_to_pycolmap_model_folder>")
#     #     sys.exit(1)
#     # main(Path(sys.argv[1]))
#     main(Path("C:\\Users\\Tyler\\Desktop\\sfm_model\\sfm_model\\0"))

#     input("Press Enter to exit and close the visualization…")
