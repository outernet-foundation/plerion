using System;
using System.Threading;
using Cysharp.Threading.Tasks;

using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

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

        public UniTask<byte[]> GetFrameJPG()
            => GetFrameJPG(_cancellationTokenSource.Token);

        public async UniTask<byte[]> GetFrameJPG(CancellationToken cancellationToken = default)
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
            var result = await ConvertToJPG(cpuImage, flipped: true);
            cpuImage.Dispose();

            return result;
        }

        private static async UniTask<byte[]> ConvertToJPG(XRCpuImage cpuImage, bool flipped = false)
        {
            var conversion = new XRCpuImage.ConversionParams(
                cpuImage,
                TextureFormat.RGBA32,
                // Mirror the image on the X axis to match the display orientation
                flipped ? XRCpuImage.Transformation.MirrorX : XRCpuImage.Transformation.None
            );

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