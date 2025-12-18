using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using PinholeCameraConfig = PlerionApiClient.Model.PinholeCameraConfig;

namespace Plerion.Core.ARFoundation
{
    public class ARFoundationCameraProvider : ICameraProvider
    {
        public ARCameraManager cameraManager;
        public bool manageCameraEnabledState;
        private CancellationTokenSource _cancellationTokenSource;
        static XRCameraConfiguration? bestConfig = null;

        public ARFoundationCameraProvider(ARCameraManager cameraManager, bool manageCameraEnabledState = true)
        {
            this.cameraManager = cameraManager;
            this.manageCameraEnabledState = manageCameraEnabledState;

            if (manageCameraEnabledState)
                cameraManager.enabled = false;
        }

        public void Initialize()
        {
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                Permission.RequestUserPermission(Permission.Camera);

            foreach (var config in cameraManager.GetConfigurations(Allocator.Temp))
            {
                if (
                    bestConfig == null
                    || (config.width * config.height) > (bestConfig.Value.width * bestConfig.Value.height)
                )
                {
                    bestConfig = config;
                }
            }
            if (bestConfig.HasValue)
            {
                cameraManager.currentConfiguration = bestConfig;
                Debug.Log(
                    $"Selected camera configuration: {bestConfig.Value.width}x{bestConfig.Value.height} @ {bestConfig.Value.framerate}fps"
                );
            }
            else
            {
                Debug.LogWarning("No camera configurations available.");
            }
        }

        public void Start()
        {
            if (manageCameraEnabledState)
                cameraManager.enabled = true;

            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Stop()
        {
            if (manageCameraEnabledState)
                cameraManager.enabled = false;

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        public bool GetCameraConfig(out PinholeCameraConfig cameraConfig)
        {
            if (
                !cameraManager.TryGetIntrinsics(out XRCameraIntrinsics intrinsics)
                || intrinsics.resolution.x != bestConfig?.width
                || intrinsics.resolution.y != bestConfig?.height
            )
            {
                cameraConfig = null;
                return false;
            }

            cameraConfig = new PinholeCameraConfig(
                model: PinholeCameraConfig.ModelEnum.PINHOLE,
                // ARFoundation on Android Mobile returns images in LEFT_TOP orientation (EXIF/TIFF Orientation=5):
                //  - 0th row is the visual left edge
                //  - 0th column is the visual top edge
                // To display canonically (TOP_LEFT), apply a transpose (swap X/Y), e.g.:
                //  - rotate 90° CW, then flip left↔right, OR
                //  - flip top↔bottom, then rotate 90° CW
                orientation: PinholeCameraConfig.OrientationEnum.LEFTTOP,
                width: intrinsics.resolution.x,
                height: intrinsics.resolution.y,
                fx: intrinsics.focalLength.x,
                fy: intrinsics.focalLength.y,
                cx: intrinsics.principalPoint.x,
                cy: intrinsics.principalPoint.y
            );

            return true;
        }

        public async UniTask<(byte[], Vector3, Quaternion)> GetFrame()
        {
            if (!cameraManager.TryAcquireLatestCpuImage(out var cpuImage))
                return (null, default, default);

            var conversion = new XRCpuImage.ConversionParams(cpuImage, TextureFormat.RGBA32);

            int byteCount = cpuImage.GetConvertedDataSize(conversion);
            byte[] pixelBuffer = default;
            XRCpuImage.AsyncConversionStatus conversionStatus = XRCpuImage.AsyncConversionStatus.Pending;

            cpuImage.ConvertAsync(
                conversion,
                (status, _, result) =>
                {
                    conversionStatus = status;
                    pixelBuffer = result.ToArray();
                }
            );

            await UniTask.WaitUntil(() =>
                conversionStatus == XRCpuImage.AsyncConversionStatus.Ready
                || conversionStatus == XRCpuImage.AsyncConversionStatus.Failed
                || conversionStatus == XRCpuImage.AsyncConversionStatus.Disposed
            );

            if (conversionStatus == XRCpuImage.AsyncConversionStatus.Disposed)
                throw new Exception("Conversion disposed unexpectedly");

            if (conversionStatus == XRCpuImage.AsyncConversionStatus.Failed)
                throw new Exception("XRCpuImage conversion failed");

            uint width = (uint)cpuImage.width;
            uint height = (uint)cpuImage.height;
            cpuImage.Dispose();

            var jpgBytes = await UniTask.RunOnThreadPool(() =>
                ImageConversion.EncodeArrayToJPG(pixelBuffer, GraphicsFormat.R8G8B8A8_SRGB, width, height)
            );

            return (jpgBytes, Camera.main.transform.position, Camera.main.transform.rotation);
        }
    }
}
