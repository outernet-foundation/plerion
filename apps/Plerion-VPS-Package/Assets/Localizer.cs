using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Plerion.VPS
{
    static public class Localizer
    {
        enum CameraStatus
        {
            Stopped,
            Starting,
            Running
        }

        static public Status Status { get; } = new Status();
        static private CameraStatus cameraStatus = CameraStatus.Stopped;
        static private CancellationTokenSource _cancellationTokenSource;

        static public void Initialize()
        {
            CameraCapture.Initialize(EstimateCameraPose);
        }

        static public void Terminate()
        {
            StopCameraCapture();
        }

        static public void StartCameraCapture()
        {
            if (cameraStatus == CameraStatus.Running ||
                cameraStatus == CameraStatus.Starting)
            {
                return;
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            EstimateCameraPose(_cancellationTokenSource.Token).Forget();
        }

        static public void StopCameraCapture()
        {
            if (cameraStatus == CameraStatus.Stopped)
                return;

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        static private async UniTask EstimateCameraPose(CancellationToken cancellationToken = default)
        {
            cameraStatus = CameraStatus.Starting;
            Log.Info(LogGroup.Localizer, "Starting camera capture");

            await CameraCapture.Start();
            await UniTask.SwitchToMainThread();

            cameraStatus = CameraStatus.Running;
            Log.Info(LogGroup.Localizer, "Camera capture started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var localization = await CameraCapture.CaptureCameraImageAndEstimatePose();
                    await UniTask.SwitchToMainThread();
                    Status.AddLocalization(localization);

                    if (localization == null)
                        return;

                    Log.Info(LogGroup.Localizer, $"Localized to map {localization.Value.map.Name} with confidence {localization.Value.confidence}");
                    LocalizedReferenceFrame.ApplyEstimate(localization.Value);
                }
                catch (Exception exception)
                {
                    Log.Error(LogGroup.Localizer, "Exception thrown during localization", exception);
                }
            }

            CameraCapture.Stop();
            cameraStatus = CameraStatus.Stopped;
            Log.Info(LogGroup.Localizer, "Camera capture stopped");
        }

        static private CameraPoseEstimate? EstimateCameraPose(CameraImage cameraImage)
        {
            if (cameraImage.pixelBuffer == IntPtr.Zero)
                return null;

            var cameraRotation = cameraImage.cameraRotation;
            cameraRotation *= cameraImage.cameraOrientation;
            cameraRotation.SwitchHandedness();

            var localizeResult = ImmersalNative.Localize(
                0,
                IntPtr.Zero,
                cameraImage.imageWidth,
                cameraImage.imageHeight,
                new UnityEngine.Vector4(
                    cameraImage.focalLength.x,
                    cameraImage.focalLength.y,
                    cameraImage.principalPoint.x,
                    cameraImage.principalPoint.y
                ),
                cameraImage.pixelBuffer,
                1,
                0,
                cameraRotation).AsTask().Result;

            if (localizeResult.mapId == -1 ||
                // This is an edge case I only just discovered, which can happen because we don't
                // immediately add the native handle to the native handle array upon loading the map
                // into Immersal (which is what we shoudl be doing). Hacking around it for now.
                !MapManager.TryGetMap(localizeResult.mapId, out var map))
            {
                return null;
            }

            var estimatedRotation = localizeResult.rotation;
            var estimatedPosition = localizeResult.position;
            estimatedRotation *= cameraImage.cameraOrientation;
            estimatedPosition.SwitchHandedness();
            estimatedRotation.SwitchHandedness();

            return new CameraPoseEstimate
            {
                cameraPosition = cameraImage.cameraPosition,
                cameraRotation = cameraImage.cameraRotation,
                estimatedCameraPosition = estimatedPosition,
                estimatedCameraRotation = estimatedRotation,
                confidence = localizeResult.confidence,
                map = map
            };
        }
    }
}
