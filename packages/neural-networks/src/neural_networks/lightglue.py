from typing import Any

from lightglue import LightGlue, SuperPoint  # type: ignore


def load_superpoint(
    nms_radius: float | None = None,
    keypoint_threshold: float | None = None,
    max_num_keypoints: int | None = None,
    remove_borders: int | None = None,
    device: str = "cpu",
):
    conf: dict[str, Any] = {}
    if nms_radius is not None:
        conf["nms_radius"] = nms_radius
    if keypoint_threshold is not None:
        conf["keypoint_threshold"] = keypoint_threshold
    if max_num_keypoints is not None:
        conf["max_num_keypoints"] = max_num_keypoints
    if remove_borders is not None:
        conf["remove_borders"] = remove_borders

    return SuperPoint(**conf).eval().to(device)


def load_lightglue(device: str = "cpu"):
    # TODO: add comment about why width_confidence and depth_confidence are set to -1
    return LightGlue(features="superpoint", width_confidence=-1, depth_confidence=-1).eval().to(device)
