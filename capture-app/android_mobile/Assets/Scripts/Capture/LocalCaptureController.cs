using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Plerion.Core;
using PlerionApiClient.Model;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public static class LocalCaptureController
{
    static ARCameraManager cameraManager;
    static ARAnchorManager anchorManager;
    static ARAnchor captureAnchor;
    static float captureIntervalSeconds;

    static Guid sessionId;
    static string sessionDirectory;
    static StreamWriter poseWriter;

    static bool first_frame = true;
    static float nextCaptureTime;

    static string recordingsRoot;

    static string SessionDir(string name) => Path.Combine(recordingsRoot, name);

    static string ZipPath(string name) => Path.Combine(recordingsRoot, $"{name}.zip");

    public static void Initialize()
    {
        recordingsRoot = Path.Combine(Application.persistentDataPath, "Captures");
        Directory.CreateDirectory(recordingsRoot);

        anchorManager = UnityEngine.Object.FindAnyObjectByType<ARAnchorManager>();
    }

    public static IEnumerable<Guid> GetCaptures()
    {
        if (!Directory.Exists(recordingsRoot))
            return Array.Empty<Guid>();

        return new DirectoryInfo(recordingsRoot).GetDirectories().Select(d => d.Name).Select(name => Guid.Parse(name));
    }

    public static async UniTask<Stream> GetCapture(Guid captureId) =>
        await TarUtils.CreateTarAsync(SessionDir(captureId.ToString()));

    public static async UniTask StartCapture(CancellationToken cancellationToken, float requestedCaptureIntervalSeconds)
    {
        captureIntervalSeconds = requestedCaptureIntervalSeconds;

        sessionId = Guid.NewGuid();
        sessionDirectory = SessionDir(sessionId.ToString());
        Directory.CreateDirectory(Path.Combine(sessionDirectory, "rig0"));
        Directory.CreateDirectory(Path.Combine(sessionDirectory, "rig0", "camera0"));

        poseWriter = new StreamWriter(Path.Combine(sessionDirectory, "rig0", "frames.csv")) { AutoFlush = true };
        poseWriter.WriteLine("timestamp,tx,ty,tz,qx,qy,qz,qw");

        await UniTask.WaitUntil(
            () => ARSession.state == ARSessionState.SessionTracking,
            cancellationToken: cancellationToken
        );

        var rotation = cameraManager.transform.rotation;
        rotation.x = 0;
        rotation.z = 0;
        captureAnchor = (
            await anchorManager.TryAddAnchorAsync(new Pose(cameraManager.transform.position, rotation))
        ).value;

        await UniTask.WaitUntil(
            () => captureAnchor.trackingState == TrackingState.Tracking,
            cancellationToken: cancellationToken
        );

        cameraManager.frameReceived += OnCameraFrameReceived;
    }

    public static void StopCapture()
    {
        poseWriter?.Dispose();
        poseWriter = null;

        if (cameraManager != null)
        {
            cameraManager.frameReceived -= OnCameraFrameReceived;
            cameraManager = null;
        }

        first_frame = true;
        nextCaptureTime = 0;

        System.IO.Compression.ZipFile.CreateFromDirectory(sessionDirectory, ZipPath(sessionId.ToString()));
    }

    public static bool CaptureExists(Guid id) => File.Exists(ZipPath(id.ToString()));

    public static void DeleteCapture(Guid id)
    {
        if (File.Exists(ZipPath(id.ToString())))
            File.Delete(ZipPath(id.ToString()));

        if (Directory.Exists(SessionDir(id.ToString())))
            Directory.Delete(SessionDir(id.ToString()), recursive: true);
    }

    static void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        // Don't capture if we lost tracking of the capture anchor
        if (captureAnchor.trackingState != TrackingState.Tracking)
        {
            Debug.LogWarning("Capture anchor lost tracking; skipping frame.");
            return;
        }

        // Throttle capture to the requested interval.
        if (Time.time < nextCaptureTime)
            return;
        nextCaptureTime = Time.time + captureIntervalSeconds;

        if (first_frame)
        {
            PinholeCameraConfig cameraConfig;
            if (!VisualPositioningSystem.CameraProvider.GetCameraConfig(out cameraConfig))
            {
                return;
            }

            // Write out capture session manifest
            File.WriteAllText(
                Path.Combine(sessionDirectory, "manifest.json"),
                new CaptureSessionManifest(
                    AxisConvention.UNITY,
                    new List<RigConfig>
                    {
                        new RigConfig(
                            "rig0",
                            new List<RigCameraConfig>
                            {
                                new RigCameraConfig(
                                    "camera0",
                                    true,
                                    new PlerionApiClient.Model.Quaternion(0, 0, 0, 1),
                                    new PlerionApiClient.Model.Vector3(0, 0, 0),
                                    new CameraConfig(cameraConfig)
                                ),
                            }
                        ),
                    }
                ).ToJson()
            );

            // Done with first frame setup
            first_frame = false;
        }

        VisualPositioningSystem
            .CameraProvider.GetFrame()
            .ContinueWith(async result =>
            {
                var bytes = result.Item1;
                var cameraPosition = result.Item2;
                var cameraRotation = result.Item3;

                long timestampMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var absoluteImagePath = Path.Combine(
                    sessionDirectory,
                    "rig0",
                    "camera0",
                    $"{timestampMilliseconds}.jpg"
                );
                Directory.CreateDirectory(Path.GetDirectoryName(absoluteImagePath)!);
                await File.WriteAllBytesAsync(absoluteImagePath, bytes);

                // Compute camera pose relative to the capture anchor
                var framePosition = captureAnchor.transform.InverseTransformPoint(cameraPosition);
                var frameRotation = UnityEngine.Quaternion.Inverse(captureAnchor.transform.rotation) * cameraRotation;

                // Write out pose
                poseWriter.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6},{7}",
                        timestampMilliseconds,
                        framePosition.x,
                        framePosition.y,
                        framePosition.z,
                        frameRotation.x,
                        frameRotation.y,
                        frameRotation.z,
                        frameRotation.w
                    )
                );
            })
            .Forget();
    }
}
