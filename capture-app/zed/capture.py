import os
from pathlib import Path
import threading
import time
import json
import csv
import numpy as np
import pyzed.sl as sl
from scipy.spatial.transform import Rotation

class CaptureThread(threading.Thread):
    def __init__(self, stop_event: threading.Event, output_directory: Path, capture_interval: float):
        super().__init__(daemon=True)
        self.stop_event = stop_event
        self.output_directory = output_directory
        self.capture_interval = capture_interval
        self._exception = None

    def exception(self):
        return self._exception
    
    def run(self):
        images_directory = self.output_directory / "images"
        images_directory.mkdir(parents=True, exist_ok=True)

        # start camera
        zed = sl.Camera()
        init = sl.InitParameters(
            camera_resolution=sl.RESOLUTION.HD720,
            coordinate_units =sl.UNIT.METER)
        if zed.open(init) != sl.ERROR_CODE.SUCCESS:
            raise RuntimeError("ZED open failed")

        # write calibration.json
        calibration_parameters = zed.get_camera_information().camera_configuration.calibration_parameters
        left_camera = calibration_parameters.left_cam
        right_camera = calibration_parameters.right_cam
        stereo_transform  = calibration_parameters.stereo_transform.m
        with open(os.path.join(self.output_directory, "calibration.json"), "w") as f:
            json.dump([
                {
                    "id": "left",
                    "translation": [0, 0, 0],
                    "rotation": [0, 0, 0, 1],
                    "model": "PINHOLE",
                    "width":  left_camera.image_size.width,
                    "height": left_camera.image_size.height,
                    "params": [
                        left_camera.fx,
                        left_camera.fy,
                        left_camera.cx,
                        left_camera.cy
                    ]
                },
                {
                    "id": "right",
                    "translation": stereo_transform[:3, 3].tolist(),
                    "rotation": Rotation.from_matrix(np.array(stereo_transform[:3, :3])).as_quat().tolist(),
                    "model": "PINHOLE",
                    "width":  right_camera.image_size.width,
                    "height": right_camera.image_size.height,
                    "params": [
                        right_camera.fx,
                        right_camera.fy,
                        right_camera.cx,
                        right_camera.cy
                    ]
                }
            ], f, indent=4)

        # enable tracking
        positionTrackingParameters = sl.PositionalTrackingParameters()
        positionTrackingParameters.enable_imu_fusion=True
        positionTrackingParameters.set_floor_as_origin=False
        zed.enable_positional_tracking(positionTrackingParameters)

        # open poses.csv        
        csv_file = open(self.output_directory / "poses.csv", "w", newline="")

        # begin capture loop
        try:
            csv_writer = csv.writer(csv_file)
            csv_writer.writerow(["timestamp","tx","ty","tz","qx","qy","qz","qw"])
            next_capture = time.time()
            while not self.stop_event.is_set():
                if time.time() < next_capture:
                    time.sleep(0.01)
                    continue

                while zed.grab() != sl.ERROR_CODE.SUCCESS:
                    time.sleep(0.003)
                    pass

                # write pose to disk
                pose = sl.Pose();  
                zed.get_position(pose, sl.REFERENCE_FRAME.WORLD)
                timestamp  = int(pose.timestamp.get_milliseconds())
                csv_writer.writerow([
                    timestamp,
                    *pose.get_translation(sl.Translation()).get(),
                    *pose.get_orientation(sl.Orientation()).get()
                ]);  
                csv_file.flush()

                # write images to disk
                left_image = sl.Mat()
                right_image = sl.Mat()
                zed.retrieve_image(left_image, sl.VIEW.LEFT);  
                zed.retrieve_image(right_image, sl.VIEW.RIGHT)
                left_image.write(str(images_directory/f"{timestamp}_left.png"));  
                right_image.write(str(images_directory/f"{timestamp}_right.png"))

                next_capture += self.capture_interval

        except KeyboardInterrupt:
            print("Capture interrupted by user.")

        except Exception as e:
            self._exception = e

        finally:
            csv_file.close()
            zed.disable_positional_tracking();
            zed.close()