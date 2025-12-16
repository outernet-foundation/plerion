using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlerionApiClient.Model;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public static class LocalCaptureController
{
    static ARCameraManager cameraManager;
    static ARAnchorManager anchorManager;
    static ARAnchor captureAnchor;
    static float captureIntervalSeconds;
    static XRCameraConfiguration? bestConfig = null;

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
        var root = recordingsRoot;
        Debug.Log($"Capture root: {root}");
        Directory.CreateDirectory(recordingsRoot); // ensure root exists

        cameraManager = UnityEngine.Object.FindAnyObjectByType<ARCameraManager>();
        anchorManager = UnityEngine.Object.FindAnyObjectByType<ARAnchorManager>();
        captureIntervalSeconds = requestedCaptureIntervalSeconds;

        await UniTask.WaitUntil(
            () => Permission.HasUserAuthorizedPermission(Permission.Camera),
            cancellationToken: cancellationToken
        );

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

        sessionId = Guid.NewGuid();
        sessionDirectory = SessionDir(sessionId.ToString());
        Directory.CreateDirectory(Path.Combine(sessionDirectory, "rig0"));
        Directory.CreateDirectory(Path.Combine(sessionDirectory, "rig0", "camera0"));

        poseWriter = new StreamWriter(Path.Combine(sessionDirectory, "rig0", "frames.csv")) { AutoFlush = true };
        poseWriter.WriteLine("timestamp,tx,ty,tz,qx,qy,qz,qw");

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

        // Put the zip *next to* the sessionDirectory
        string zipFilePath = ZipPath(sessionId.ToString());
        System.IO.Compression.ZipFile.CreateFromDirectory(sessionDirectory, zipFilePath);
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
            // Wait until we get intrinsics and they match the selected config
            if (
                !cameraManager.TryGetIntrinsics(out var intrinsics)
                || intrinsics.resolution.x != bestConfig?.width
                || intrinsics.resolution.y != bestConfig?.height
            )
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
                                    new CameraConfig(
                                        new PinholeCameraConfig(
                                            model: PinholeCameraConfig.ModelEnum.PINHOLE,
                                            mirroring: PinholeCameraConfig.MirroringEnum.X, // On (at least) AndroidMobile, the image is mirrored along X
                                            rotation: PinholeCameraConfig.RotationEnum._90CCW, // On (at least) AndroidMobile, the image is rotated 90 CCW
                                            width: intrinsics.resolution.x,
                                            height: intrinsics.resolution.y,
                                            fx: intrinsics.focalLength.x,
                                            fy: intrinsics.focalLength.y,
                                            cx: intrinsics.principalPoint.x,
                                            cy: intrinsics.principalPoint.y
                                        )
                                    )
                                ),
                            }
                        ),
                    }
                ).ToJson()
            );

            // Done with first frame setup
            first_frame = false;
        }

        // Acquire the latest CPU image.
        if (!cameraManager.TryAcquireLatestCpuImage(out var cpuImage))
            return;

        long timestampMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Compute camera pose relative to the capture anchor
        var framePosition = captureAnchor.transform.InverseTransformPoint(cameraManager.transform.position);
        var frameRotation =
            UnityEngine.Quaternion.Inverse(captureAnchor.transform.rotation) * cameraManager.transform.rotation;

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

        // Save jpeg
        SaveImageAsync(cpuImage, Path.Combine(sessionDirectory, "rig0", "camera0", $"{timestampMilliseconds}.jpg"))
            .Forget();
    }

    static float3x3 basisOpenCV = new float3x3(new float3(1f, 0f, 0f), new float3(0f, -1f, 0f), new float3(0f, 0f, 1f));
    static float3x3 basisUnity = float3x3.identity;
    static float3x3 basisChangeUnityFromOpenCV = math.mul(math.transpose(basisUnity), basisOpenCV);
    static float3x3 basisChangeOpenCVFromUnity = math.transpose(basisChangeUnityFromOpenCV);

    public static (float3x3, float3) UnityFromOpenCV(float3x3 rotation, float3 translation) =>
        (
            math.mul(basisChangeUnityFromOpenCV, math.mul(rotation, basisChangeOpenCVFromUnity)),
            math.mul(basisChangeUnityFromOpenCV, translation)
        );

    public static (float3x3, float3) OpenCVFromUnity(float3x3 rotation, float3 translation) =>
        (
            math.mul(basisChangeOpenCVFromUnity, math.mul(rotation, basisChangeUnityFromOpenCV)),
            math.mul(basisChangeOpenCVFromUnity, translation)
        );

    static async UniTask SaveImageAsync(XRCpuImage cpuImage, string absoluteImagePath)
    {
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

        Directory.CreateDirectory(Path.GetDirectoryName(absoluteImagePath)!);
        await File.WriteAllBytesAsync(absoluteImagePath, jpgBytes);
    }
}
