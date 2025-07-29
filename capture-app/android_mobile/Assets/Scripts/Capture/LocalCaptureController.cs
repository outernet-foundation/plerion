using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public static class LocalCaptureController
{
    [Serializable]
    struct CameraCalibration
    {
        public string id;
        public float[] translation;
        public float[] rotation;
        public string model;
        public int width;
        public int height;
        public float[] @params;
    }

    [Serializable]
    class RigCalibration
    {
        public CameraCalibration[] cameras;

        public RigCalibration(params CameraCalibration[] entries)
        {
            cameras = entries;
        }
    }


    static ARCameraManager cameraManager;
    static float captureIntervalSeconds;

    static string sessionId;
    static string sessionDirectory;
    static StreamWriter poseWriter;
    static readonly object inputOutputLock = new();

    static bool calibrationWritten;
    static float nextCaptureTime;

    public static IEnumerable<string> GetCaptures()
    {
        string recordingsRoot = Path.Combine(Application.persistentDataPath, "Captures");
        if (!Directory.Exists(recordingsRoot)) return Array.Empty<string>();

        return new DirectoryInfo(recordingsRoot)
            .GetDirectories()
            .Select(captureDirectory => captureDirectory.Name);
    }

    public static async UniTask<byte[]> GetCapture(string captureName)
    {
        string recordingsRoot = Path.Combine(Application.persistentDataPath, "Captures");
        string captureDirectory = Path.Combine(recordingsRoot, captureName);
        if (!Directory.Exists(captureDirectory)) throw new DirectoryNotFoundException($"Capture directory not found: {captureDirectory}");

        string zipFilePath = Path.Combine(captureDirectory, $"{captureName}.zip");
        if (!File.Exists(zipFilePath)) throw new FileNotFoundException($"Zip file not found: {zipFilePath}");

        return await File.ReadAllBytesAsync(zipFilePath);
    }

    public static async UniTask StartCapture(
        CancellationToken cancellationToken,
        float requestedCaptureIntervalSeconds)
    {
        cameraManager = UnityEngine.Object.FindObjectOfType<ARCameraManager>();
        captureIntervalSeconds = requestedCaptureIntervalSeconds;

        Application.targetFrameRate = 30;

        await UniTask.WaitUntil(
            () => Permission.HasUserAuthorizedPermission(Permission.Camera),
            cancellationToken: cancellationToken);

        await UniTask.WaitUntil(
            () => ARSession.state == ARSessionState.SessionTracking,
            cancellationToken: cancellationToken);

        string recordingsRoot = Path.Combine(
            Application.persistentDataPath, "Captures");

        sessionId = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        sessionDirectory = Path.Combine(recordingsRoot, sessionId);
        Directory.CreateDirectory(Path.Combine(sessionDirectory, "images"));

        poseWriter = new StreamWriter(
            Path.Combine(sessionDirectory, "poses.csv"))
        {
            AutoFlush = true
        };
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

        calibrationWritten = false;
        nextCaptureTime = 0;

        // Create zip
        string zipFilePath = Path.Combine(sessionDirectory, $"{sessionId}.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(sessionDirectory, zipFilePath);
    }

    static void OnCameraFrameReceived(ARCameraFrameEventArgs _)
    {
        // Write calibration.json once
        if (!calibrationWritten && cameraManager.TryGetIntrinsics(out var intrinsics))
        {
            File.WriteAllText(
                Path.Combine(sessionDirectory, "calibration.json"),
                JsonUtility.ToJson(
                    new RigCalibration(new CameraCalibration
                    {
                        id = "mono",
                        translation = new[] { 0f, 0f, 0f },
                        rotation = new[] { 0f, 0f, 0f, 1f },
                        model = "PINHOLE",
                        width = intrinsics.resolution.x,
                        height = intrinsics.resolution.y,
                        @params = new[]
                        {
                            intrinsics.focalLength.x, intrinsics.focalLength.y,
                            intrinsics.principalPoint.x, intrinsics.principalPoint.y
                        }
                    }),
                    true
                )
            );

            calibrationWritten = true;
        }

        // Throttle capture to the requested interval.
        if (Time.time < nextCaptureTime) return;
        nextCaptureTime = Time.time + captureIntervalSeconds;

        // Acquire the latest CPU image.
        if (!cameraManager.TryAcquireLatestCpuImage(out var cpuImage)) return;

        long timestampMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string relativeImagePath = $"images/{timestampMilliseconds}_mono.png";
        string absoluteImagePath = Path.Combine(sessionDirectory, relativeImagePath);

        // Write pose row: timestamp, tx,ty,tz, qx,qy,qz,qw
        lock (inputOutputLock)
        {
            poseWriter.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6},{7}",
                timestampMilliseconds,
                cameraManager.transform.position.x,
                cameraManager.transform.position.y,
                cameraManager.transform.position.z,
                cameraManager.transform.rotation.x,
                cameraManager.transform.rotation.y,
                cameraManager.transform.rotation.z,
                cameraManager.transform.rotation.w));
        }

        // Save the PNG off-thread.
        SaveImageAsync(cpuImage, absoluteImagePath).Forget();
    }

    static async UniTask SaveImageAsync(
        XRCpuImage cpuImage,
        string absoluteImagePath)
    {
        var conversion = new XRCpuImage.ConversionParams(
            cpuImage,
            TextureFormat.RGBA32,
            XRCpuImage.Transformation.None);

        int byteCount = cpuImage.GetConvertedDataSize(conversion);
        using var pixelBuffer = new NativeArray<byte>(byteCount, Allocator.TempJob);
        cpuImage.Convert(conversion, pixelBuffer);
        cpuImage.Dispose();

        var texture = new Texture2D(
            conversion.outputDimensions.x,
            conversion.outputDimensions.y,
            TextureFormat.RGBA32,
            false);

        texture.LoadRawTextureData(pixelBuffer);
        texture.Apply(false, false);
        byte[] pngBytes = texture.EncodeToPNG();
        UnityEngine.Object.Destroy(texture);

        Directory.CreateDirectory(Path.GetDirectoryName(absoluteImagePath)!);
        await File.WriteAllBytesAsync(absoluteImagePath, pngBytes);
    }
}
