#if UNITY_ANDROID
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Plerion
{
    public static class ARFoundationCameraLocalizer
    {
        private static ARCameraManager cameraManager;
        private static bool processingFrame = false;

        public static void Start(ARCameraManager arCameraManager)
        {
            cameraManager = arCameraManager;
            cameraManager.frameReceived += OnFrameReceived;
        }

        public static void Stop()
        {
            cameraManager.frameReceived += OnFrameReceived;
        }

        private static void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (processingFrame) return;

            if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image) ||
                !cameraManager.TryGetIntrinsics(out XRCameraIntrinsics intrinsics))
            {
                return;
            }

            processingFrame = true;

            var pixelBuffer = image.GetPlane(0).data.ToArray();
            var width = image.width;
            var height = image.height;

            image.Dispose();

            float angle;
            switch (Screen.orientation)
            {
                case ScreenOrientation.Portrait:
                    angle = 90f;
                    break;
                case ScreenOrientation.LandscapeLeft:
                    angle = 180f;
                    break;
                case ScreenOrientation.LandscapeRight:
                    angle = 0f;
                    break;
                case ScreenOrientation.PortraitUpsideDown:
                    angle = -90f;
                    break;
                default:
                    angle = 0f;
                    break;
            }

            LocalizeWithImage(width, height, pixelBuffer, intrinsics.focalLength, intrinsics.principalPoint, angle).Forget();
        }

        private static async UniTask LocalizeWithImage(int width, int height, byte[] pixelBuffer, Vector3 focalLength, Vector3 principalPoint, float cameraOrientation)
        {
            await UniTask.SwitchToMainThread();
            await VisualPositioningSystem.LocalizeFromCameraImage(new CameraImage
            {
                imageWidth = width,
                imageHeight = height,
                pixelBuffer = pixelBuffer,
                focalLength = focalLength,
                principalPoint = principalPoint,
                cameraPosition = Camera.main.transform.position,
                cameraRotation = Camera.main.transform.rotation,
                cameraOrientation = Quaternion.Euler(0f, 0f, cameraOrientation),
            });

            processingFrame = false;
        }
    }
}
#endif