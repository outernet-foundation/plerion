using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using PinholeCameraConfig = PlerionApiClient.Model.PinholeCameraConfig;

namespace Plerion.VPS.ARFoundation
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
                mirroring: PinholeCameraConfig.MirroringEnum.X, // On (at least) ARFoundation, the image is mirrored along X
                rotation: PinholeCameraConfig.RotationEnum._90CCW, // On (at least) ARFoundation, the image is rotated 90 CCW
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
            var result = ConvertToJPG(cpuImage, flipped: true);
            cpuImage.Dispose();

            return (result, Camera.main.transform.position, Camera.main.transform.rotation);
        }

        private static byte[] ConvertToJPG(XRCpuImage cpuImage, bool flipped = false)
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
