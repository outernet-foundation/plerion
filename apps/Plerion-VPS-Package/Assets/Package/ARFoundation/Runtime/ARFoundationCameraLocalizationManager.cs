using System;
using System.Threading;
using Cysharp.Threading.Tasks;

using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

using UnityEngine.Android;

namespace Plerion.VPS.ARFoundation
{
    public class ARFoundationCameraLocalizationManager : ICameraLocalizationManager
    {
        public bool isRunning { get; private set; }

        public ARCameraManager cameraManager;
        public bool manageCameraEnabledState;

        private bool _localizing;
        private CancellationTokenSource _cancellationTokenSource;

        public ARFoundationCameraLocalizationManager(ARCameraManager cameraManager, bool manageCameraEnabledState = true)
        {
            this.cameraManager = cameraManager;
            this.manageCameraEnabledState = manageCameraEnabledState;

            if (manageCameraEnabledState)
                cameraManager.enabled = false;
        }

        public void Start()
        {
            if (isRunning)
                return;

            isRunning = true;

            if (manageCameraEnabledState)
                cameraManager.enabled = true;

            _cancellationTokenSource = new CancellationTokenSource();
            StartInternal(_cancellationTokenSource.Token).Forget();
        }

        public void Stop()
        {
            if (!isRunning)
                return;

            isRunning = false;

            if (manageCameraEnabledState)
                cameraManager.enabled = false;

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            cameraManager.frameReceived -= HandleFrameReceived;
            _localizing = false;
        }

        private async UniTask StartInternal(CancellationToken cancellationToken = default)
        {
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                Permission.RequestUserPermission(Permission.Camera);
#endif

            await UniTask.WaitUntil(() => VisualPositioningSystem.LocalizationSessionActive, cancellationToken: cancellationToken);
            await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);

            if (!cameraManager.TryGetIntrinsics(out var intrinsics))
                throw new Exception("Camera intrinsics unavailable");

            await VisualPositioningSystem.SetLocalizationSessionCameraIntrinsicsAsync(new CameraIntrinsics()
            {
                resolution = intrinsics.resolution,
                focalLength = intrinsics.focalLength,
                // principlePoint = intrinsics.principalPoint
                principalPoint = new Vector2(
                    (intrinsics.resolution.x - 1) - intrinsics.principalPoint.x,
                    intrinsics.principalPoint.y
                )
            });

            cameraManager.frameReceived += HandleFrameReceived;
        }

        private void HandleFrameReceived(ARCameraFrameEventArgs args)
        {
            if (_localizing)
                return;

            if (!cameraManager.TryAcquireLatestCpuImage(out var cpuImage))
                return;

            LocalizeWithFrame(cpuImage, false, _cancellationTokenSource.Token).Forget();
        }

        private async UniTask LocalizeWithFrame(XRCpuImage frame, bool flipped = false, CancellationToken cancellationToken = default)
        {
            _localizing = true;

            cancellationToken.ThrowIfCancellationRequested();

            var conversion = new XRCpuImage.ConversionParams(
                frame,
                TextureFormat.RGBA32,
                // Mirror the image on the X axis to match the display orientation
                flipped ? XRCpuImage.Transformation.MirrorX : XRCpuImage.Transformation.None
            );

            int byteCount = frame.GetConvertedDataSize(conversion);
            using var pixelBuffer = new NativeArray<byte>(byteCount, Allocator.TempJob);
            frame.Convert(conversion, pixelBuffer);

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

            frame.Dispose();

            await VisualPositioningSystem.LocalizeFromCameraImage(
                jpgBytes,
                Camera.main.transform.position,
                Camera.main.transform.rotation
            );

            await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);

            _localizing = false;
        }
    }
}