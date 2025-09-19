#if !UNITY_EDITOR && OUTERNET_ANDROID_MOBILE
using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using static Plerion.VPS.Assert;

namespace Plerion.VPS
{
    public static class CameraCapture
    {
        private static ARCameraManager cameraManager;
        private static Func<CameraImage, CameraPoseEstimate?> onImageCaptured;
        static TaskCompletionSource<CameraPoseEstimate?> captureTaskSource = null;
        static bool processingFrame = false;

        public static void Initialize(Func<CameraImage, CameraPoseEstimate?> onImageCaptured)
        {
            CameraCapture.onImageCaptured = onImageCaptured;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public static async UniTask Start()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            cameraManager = UnityEngine.Object.FindObjectOfType<ARCameraManager>();

            cameraManager.frameReceived += OnFrameReceived;
        }

        public static void Stop()
        {
            cameraManager.frameReceived += OnFrameReceived;
        }

        public static async UniTask<CameraPoseEstimate?> CaptureCameraImageAndEstimatePose()
        {
            ASSERT(captureTaskSource == null, "CaptureCamera is already in progress");

            captureTaskSource = new TaskCompletionSource<CameraPoseEstimate?>();
            var cameraPoseEstimate = await captureTaskSource.Task;
            captureTaskSource = null;

            return cameraPoseEstimate;
        }

        private static void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (captureTaskSource == null) return;
            if (processingFrame) return;

            if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image) ||
                !cameraManager.TryGetIntrinsics(out XRCameraIntrinsics intrinsics))
            {
                return;
            }

            processingFrame = true;

            IntPtr planeData;
            unsafe
            {
                planeData = (IntPtr)image.GetPlane(0).data.GetUnsafePtr();
            }

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

            var capturedImage = new CameraImage
            {
                imageWidth = image.width,
                imageHeight = image.height,
                pixelBuffer = planeData,
                focalLength = intrinsics.focalLength,
                principalPoint = intrinsics.principalPoint,
                cameraPosition = Camera.main.transform.position,
                cameraRotation = Camera.main.transform.rotation,
                cameraOrientation = Quaternion.Euler(0f, 0f, angle),
            };

            // C# 11
            async UniTask EsimatePose()
            {
                await UniTask.SwitchToThreadPool();
                var localization = onImageCaptured(capturedImage);
                await UniTask.SwitchToMainThread();

                image.Dispose();
                processingFrame = false;
                captureTaskSource.SetResult(localization);
            }

            UniTask
                .Create(EsimatePose)
                .Forget();
        }
    }
}
#endif