using System;
using Cysharp.Threading.Tasks;
using R3;

namespace Outernet.Client.Location
{
    static public class Localizer
    {
        enum CameraStatus
        {
            Stopped,
            Starting,
            Running
        }

        static private UniTask? cameraPoseEstimationTask;

        static public Status Status { get; } = new Status();
        static private CameraStatus cameraStatus = CameraStatus.Stopped;
        static private bool cameraCaptureRequested = false;

        static public void Initialize()
        {
            ImmersalNative.Initialize();

            ImmersalNative.SetInteger("LocalizationMaxPixels", 960 * 720);
            ImmersalNative.SetInteger("NumThreads", 1);

            // Settings.numThreads.Subscribe(value => ImmersalNative.SetInteger("NumThreads", value));
            // Settings.compression.Subscribe(value => ImmersalNative.SetInteger("ImageCompressionLevel", value));

            CameraCapture.Initialize(EstimateCameraPose);
        }

        public static void Start()
        {
            StartCameraCapture();
        }

        static public void Update()
        {
            if (Settings.localize &&
                cameraStatus == CameraStatus.Running &&
                cameraPoseEstimationTask == null)
            {
                cameraPoseEstimationTask = UniTask.Create(EstimateCameraPose);
            }
        }

        static public void StartCameraCapture()
        {
            cameraCaptureRequested = true;

            if (cameraStatus == CameraStatus.Running ||
                cameraStatus == CameraStatus.Starting)
            {
                return;
            }

            cameraStatus = CameraStatus.Starting;
            Log.Info(LogGroup.Localizer, "Starting camera capture");

            CameraCapture
                .Start()
                .ContinueWith(() =>
                {
                    cameraStatus = CameraStatus.Running;
                    Log.Info(LogGroup.Localizer, "Camera capture started");

                    if (!cameraCaptureRequested) StopCameraCapture();
                })
                .Forget();
        }

        static public void StopCameraCapture()
        {
            cameraCaptureRequested = false;

            if (cameraStatus == CameraStatus.Stopped ||
                cameraStatus == CameraStatus.Starting)
            {
                return;
            }

            CameraCapture.Stop();
            cameraStatus = CameraStatus.Stopped;
            Log.Info(LogGroup.Localizer, "Camera capture stopped");
        }

        static private async UniTask EstimateCameraPose()
        {
            try
            {
                var localization = await CameraCapture.CaptureCameraImageAndEstimatePose();
                Status.AddLocalization(localization);

                if (localization == null) return;

                Log.Info(
                    LogGroup.Localizer,
                    "Localized to map {Map} with confidence {Confidence}",
                    localization.Value.map.metadata.name,
                    localization.Value.confidence);

                LocalizedReferenceFrame.ApplyEstimate(localization.Value);
            }
            catch (Exception exception)
            {
                Log.Error(LogGroup.Localizer, exception, "Exception thrown during localization");
            }
            finally
            {
                cameraPoseEstimationTask = null;
            }
        }

        static private CameraPoseEstimate? EstimateCameraPose(CameraImage cameraImage)
        {
            if (cameraImage.pixelBuffer == IntPtr.Zero) return null;

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
                !MapManager.HasNativeHandle(localizeResult.mapId))
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
                map = MapManager.GetMapFromNativeHandle(localizeResult.mapId)
            };
        }
    }
}
