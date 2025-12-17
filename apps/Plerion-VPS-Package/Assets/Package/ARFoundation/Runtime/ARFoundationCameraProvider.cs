using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
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

        public ARFoundationCameraProvider(ARCameraManager cameraManager, bool manageCameraEnabledState = true)
        {
            this.cameraManager = cameraManager;
            this.manageCameraEnabledState = manageCameraEnabledState;

            if (manageCameraEnabledState)
                cameraManager.enabled = false;
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

        public async UniTask<PinholeCameraConfig> GetCameraConfig()
        {
            XRCameraIntrinsics intrinsics;
            while (!cameraManager.TryGetIntrinsics(out intrinsics))
                await UniTask.WaitForEndOfFrame();

            return new PinholeCameraConfig(
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
        }

        public UniTask<(byte[], Vector3, Quaternion)> GetFrameJPG() => GetFrameJPG(_cancellationTokenSource.Token);

        public async UniTask<(byte[], Vector3, Quaternion)> GetFrameJPG(CancellationToken cancellationToken = default)
        {
            bool frameReceived = false;
            Action<ARCameraFrameEventArgs> receivedFrame = args => frameReceived = true;
            cameraManager.frameReceived += receivedFrame;

            cancellationToken.Register(() => cameraManager.frameReceived -= receivedFrame);

            XRCpuImage cpuImage = default;

            while (!cancellationToken.IsCancellationRequested)
            {
                await UniTask.WaitUntil(() => frameReceived, cancellationToken: cancellationToken);
                await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);
                if (cameraManager.TryAcquireLatestCpuImage(out cpuImage))
                    break;
            }

            cancellationToken.ThrowIfCancellationRequested();

            cameraManager.frameReceived -= receivedFrame;
            var result = ConvertToJPG(cpuImage);
            cpuImage.Dispose();

            return (result, Camera.main.transform.position, Camera.main.transform.rotation);
        }

        private static byte[] ConvertToJPG(XRCpuImage cpuImage)
        {
            var conversion = new XRCpuImage.ConversionParams(cpuImage, TextureFormat.RGBA32);
            int byteCount = cpuImage.GetConvertedDataSize(conversion);
            using var pixelBuffer = new NativeArray<byte>(byteCount, Allocator.TempJob);
            cpuImage.Convert(conversion, pixelBuffer);

            var texture = new Texture2D(
                conversion.outputDimensions.x,
                conversion.outputDimensions.y,
                TextureFormat.RGBA32,
                false
            );

            texture.LoadRawTextureData(pixelBuffer);
            texture.Apply(false, false);
            byte[] jpgBytes = texture.EncodeToJPG();
            UnityEngine.Object.Destroy(texture);

            return jpgBytes;
        }
    }
}
