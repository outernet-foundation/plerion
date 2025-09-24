using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Plerion
{
    public static class CameraLocalization
    {
        enum CameraStatus
        {
            Stopped,
            Starting,
            Running
        }

        public static bool Enabled { get; private set; }
        public static Status Status { get; } = new Status();

        private static ICameraProvider _cameraProvider;
        private static CancellationTokenSource _cancellationTokenSource;

        public static void SetProvider(ICameraProvider cameraProvider)
        {
            if (Enabled)
                throw new Exception("Cannot set provider while localization is active. Call Disable() first.");

            _cameraProvider = cameraProvider;
        }

        public static void Start()
        {
            if (Enabled)
                throw new Exception("Camera Localization is already enabled.");

            Enabled = true;

            _cameraProvider.Start();
            _cancellationTokenSource = new CancellationTokenSource();

            ExecuteCameraLocalization(_cancellationTokenSource.Token).Forget();
        }

        public static void Stop()
        {
            Enabled = false;

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _cameraProvider.Stop();
        }

        private static async UniTask ExecuteCameraLocalization(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var cameraImage = await _cameraProvider.GetFrame();

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (!cameraImage.HasValue)
                        continue;

                    await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await VisualPositioningSystem.LocalizeFromCameraImage(
                        cameraImage.Value,
                        Camera.main.transform.position,
                        Camera.main.transform.rotation
                    );
                }
                catch (Exception exception)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    Log.Error(LogGroup.Localizer, "Exception thrown during localization", exception);
                }
            }
        }
    }
}
