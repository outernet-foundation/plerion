from core.classes import Quaternion, Vector3
from core.rig import RigCameraConfig, RigConfig, Transform, transform_intrinsics
from numpy import array, float64
from pycolmap import Camera as ColmapCamera
from pycolmap import RigConfig as pycolmapRigConfig
from pycolmap import RigConfigCamera as pycolmapRigConfigCamera
from pycolmap import Rigid3d, Rotation3d
from scipy.spatial.transform import Rotation


class Rig:
    def __init__(self, rig_config: RigConfig, frames_csv: str):
        ref_sensors = [camera for camera in rig_config.cameras if camera.ref_sensor]
        if len(ref_sensors) != 1:
            raise ValueError(f"Rig {rig_config.id} must have exactly one reference sensor")
        ref_sensor = ref_sensors[0]
        if ref_sensor.rotation != Quaternion(w=1.0, x=0.0, y=0.0, z=0.0):
            raise ValueError(f"Reference sensor {ref_sensor.id} in rig {rig_config.id} must have identity rotation")
        if ref_sensor.translation != Vector3(x=0.0, y=0.0, z=0.0):
            raise ValueError(f"Reference sensor {ref_sensor.id} in rig {rig_config.id} must have zero translation")

        self.cameras: dict[str, tuple[RigCameraConfig, ColmapCamera]] = {}
        rig_camera_configs: list[pycolmapRigConfigCamera] = []
        for camera in rig_config.cameras:
            self.cameras[camera.id] = (
                camera,
                ColmapCamera(
                    width=camera.camera_config.width,
                    height=camera.camera_config.height,
                    model="PINHOLE",
                    params=transform_intrinsics(camera.camera_config),
                ),
            )

            rig_camera_configs.append(
                pycolmapRigConfigCamera(
                    image_prefix=f"{rig_config.id}/{camera.id}/",
                    ref_sensor=camera.ref_sensor or False,
                    cam_from_rig=Rigid3d(
                        rotation=Rotation3d(
                            Rotation.from_quat([
                                camera.rotation.x,
                                camera.rotation.y,
                                camera.rotation.z,
                                camera.rotation.w,
                            ]).as_matrix()
                        ),
                        translation=array(
                            [camera.translation.x, camera.translation.y, camera.translation.z], dtype=float64
                        ).reshape(3, 1),
                    ),
                )
            )

        self.colmap_rig_config = pycolmapRigConfig(cameras=rig_camera_configs)

        self.frame_poses: dict[str, Transform] = {}
        for frame in frames_csv.splitlines()[1:]:  # Skip header
            frame_id, tx, ty, tz, qx, qy, qz, qw = frame.strip().split(",")
            rotation_world_from_rig = Rotation.from_quat([float(qx), float(qy), float(qz), float(qw)])
            translation_world_from_rig = array([float(tx), float(ty), float(tz)], dtype=float)
            self.frame_poses[frame_id] = Transform(
                rotation=rotation_world_from_rig.as_matrix(), translation=translation_world_from_rig
            )
