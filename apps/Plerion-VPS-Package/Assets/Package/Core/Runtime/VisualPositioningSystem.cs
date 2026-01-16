using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlerionApiClient.Api;
using PlerionApiClient.Client;
using PlerionApiClient.Model;
using R3;
using Unity.Mathematics;
using UnityEngine;

namespace Plerion.Core
{
    public static class VisualPositioningSystem
    {
        private static Action<string> _logCallback;
        private static Action<string> _warnCallback;
        private static Action<string> _errorCallback;
        private static Dictionary<Guid, LocalizationMap> _maps = new Dictionary<Guid, LocalizationMap>();
        private static ICameraProvider _cameraProvider;
        private static IDisposable _localizationSubscription;
        private static double4x4 _unityFromEcefTransform = double4x4.identity;
        private static double4x4 _ecefFromUnityTransform = math.inverse(_unityFromEcefTransform);

        public static DefaultApi Api { get; private set; }
        public static Prefabs Prefabs { get; private set; }
        public static LocalizationMetrics MostRecentMetrics { get; private set; }
        public static double4x4 EcefToUnityWorldTransform => _unityFromEcefTransform;
        public static double4x4 UnityWorldToEcefTransform => _ecefFromUnityTransform;
        public static event Action OnEcefToUnityWorldTransformUpdated;

        internal static void LogDebug(string message) => _logCallback?.Invoke(message);

        internal static void LogWarn(string message) => _warnCallback?.Invoke(message);

        internal static void LogError(string message) => _errorCallback?.Invoke(message);

        public static void Initialize(
            ICameraProvider cameraProvider,
            string apiUrl,
            string authTokenUrl,
            string authAudience,
            Action<string> logCallback,
            Action<string> warnCallback,
            Action<string> errorCallback
        )
        {
            if (_cameraProvider != null)
                throw new InvalidOperationException("VisualPositioningSystem is already initialized");

            _logCallback = logCallback;
            _warnCallback = warnCallback;
            _errorCallback = errorCallback;

            Auth.Initialize(authTokenUrl, authAudience, logCallback, warnCallback, errorCallback);

            _cameraProvider = cameraProvider;
            Prefabs = Resources.Load<Prefabs>("PrefabReferences");
            Api = new DefaultApi(
                new HttpClient(new AuthHttpHandler() { InnerHandler = new HttpClientHandler() })
                {
                    BaseAddress = new Uri(apiUrl),
                },
                apiUrl
            );
        }

        public static async UniTask Login(string username, string password) => await Auth.Login(username, password);

        public static void AddLocalizationMap(Guid mapId)
        {
            if (_maps.ContainsKey(mapId))
                throw new InvalidOperationException($"Map {mapId} is already added");

            _maps[mapId] = GameObject.Instantiate(Prefabs.localizationMapPrefab, Vector3.zero, Quaternion.identity);
            _maps[mapId].SetVisible(_localizationSubscription != null);
            _maps[mapId].Initialize(mapId);
        }

        public static void RemoveLocalizationMap(Guid mapId)
        {
            if (!_maps.ContainsKey(mapId))
                throw new InvalidOperationException($"Map {mapId} is not added");

            GameObject.Destroy(_maps[mapId].gameObject);
            _maps.Remove(mapId);
        }

        public static void StartLocalizing(float intervalSeconds)
        {
            if (_localizationSubscription != null)
                throw new InvalidOperationException("VisualPositioningSystem is already localizing");

            foreach (var map in _maps.Values)
            {
                map.SetVisible(true);
            }

            _localizationSubscription = _cameraProvider
                // Get camera configuration asynchronously
                .CameraConfig()
                // Observe CameraFrames and emit a (PinholeCameraConfig, CameraFrame) tuple for each new CameraFrame
                .SelectMany(cameraConfig =>
                    _cameraProvider.Frames(intervalSeconds).Select(frame => (cameraConfig, frame))
                )
                // Localize this client using each new CameraFrame
                .SubscribeAwait(
                    async (data, cancellationToken) => await Localize(data.cameraConfig, data.frame, cancellationToken),
                    // Skip frames if they pile up
                    AwaitOperation.Drop
                );
        }

        public static void StopLocalizing()
        {
            if (_localizationSubscription == null)
                throw new InvalidOperationException("VisualPositioningSystem is not localizing");

            foreach (var map in _maps.Values)
            {
                map.SetVisible(false);
            }

            _localizationSubscription.Dispose();
            _localizationSubscription = null;
        }

        public static (Vector3 position, Quaternion rotation) EcefToUnityWorld(
            double3 ecefPosition,
            quaternion ecefRotation
        )
        {
            var (position, rotation) = LocationUtilities.UnityFromEcef(
                _unityFromEcefTransform,
                ecefPosition,
                ecefRotation
            );

            return (
                new Vector3((float)position.x, (float)position.y, (float)position.z),
                new Quaternion(rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w)
            );
        }

        public static (double3 position, quaternion rotation) UnityWorldToEcef(Vector3 position, Quaternion rotation) =>
            LocationUtilities.EcefFromUnity(
                _ecefFromUnityTransform,
                new double3(position.x, position.y, position.z),
                rotation
            );

        private static (Vector3 position, Quaternion rotation) GetCameraPose()
        {
            if (Camera.main == null)
                throw new InvalidOperationException("Main camera is not available");

            return (Camera.main.transform.position, Camera.main.transform.rotation);
        }

        private static async UniTask<Unit> Localize(
            PinholeCameraConfig cameraConfig,
            CameraFrame frame,
            CancellationToken cancellationToken
        )
        {
            // Switch to main thread to read _maps
            await UniTask.SwitchToMainThread();

            using var memoryStream = new MemoryStream(frame.ImageBytes);
            var mapIds = _maps.Keys.ToList();
            if (mapIds.Count == 0)
            {
                throw new InvalidOperationException("No localization maps loaded");
            }

            // Localize
            var localizationResults = await Api.LocalizeImageAsync(
                mapIds,
                cameraConfig,
                AxisConvention.UNITY,
                new FileParameter(memoryStream),
                cancellationToken
            );

            if (localizationResults.Count == 0)
            {
                throw new InvalidOperationException("Localization failed");
            }

            // TODO: Handle multiple results
            var localizationResult = localizationResults.FirstOrDefault();

            // Get the transform from the map to the camera (The inverse of the camera's pose in the map)
            var translationCameraFromMap = localizationResult.CameraFromMapTransform.Translation.ToDouble3();
            var rotationCameraFromMap = localizationResult
                .CameraFromMapTransform.Rotation.ToMathematicsQuaternion()
                .ToDouble3x3();

            // Get the transform from the map to the ECEF reference frame (the map's ECEF pose)
            var translationEcefFromMap = localizationResult.MapTransform.Translation.ToDouble3();
            var rotationEcefFromMap = localizationResult.MapTransform.Rotation.ToMathematicsQuaternion().ToDouble3x3();

            // Change the basis of the map's pose to Unity's conventions
            (translationEcefFromMap, rotationEcefFromMap) = LocationUtilities.ChangeBasisUnityFromEcef(
                translationEcefFromMap,
                rotationEcefFromMap
            );

            // Get the transform from the camera to Unity world (the camera's pose in the Unity world)
            var translationUnityWorldFromCamera = (float3)frame.CameraTranslationUnityWorldFromCamera;
            // TODO: Adjust unity rotation to account for phone orientation (portrait vs landscape)
            var rotationUnityWorldFromCamera = math.mul(
                ((quaternion)frame.CameraRotationUnityWorldFromCamera).ToDouble3x3(),
                quaternion.AxisAngle(new float3(0f, 0f, 1f), math.radians(0f)).ToDouble3x3()
            );

            // Constrain both camera rotations to be gravity-aligned
            // rotationCameraFromMap = rotationCameraFromMap.RemovePitchAndRoll();
            // rotationUnityWorldFromCamera = rotationUnityWorldFromCamera.RemovePitchAndRoll();

            // Compute the transform from the map to Unity world
            var rotationUnityFromMap = math.mul(rotationUnityWorldFromCamera, rotationCameraFromMap);
            var translationUnityFromMap =
                math.mul(rotationUnityWorldFromCamera, translationCameraFromMap) + translationUnityWorldFromCamera;
            var transformUnityFromMap = Double4x4.FromTranslationRotation(
                translationUnityFromMap,
                rotationUnityFromMap
            );

            await UniTask.SwitchToMainThread();

            _unityFromEcefTransform = math.mul(
                transformUnityFromMap,
                math.inverse(Double4x4.FromTranslationRotation(translationEcefFromMap, rotationEcefFromMap))
            );
            _ecefFromUnityTransform = math.inverse(_unityFromEcefTransform);

            MostRecentMetrics = localizationResult.Metrics;

            OnEcefToUnityWorldTransformUpdated?.Invoke();

            return Unit.Default;
        }

        // public static double3x3 RemovePitchAndRoll(this double3x3 rotation)
        // {
        //     float3 up = new float3(0f, 1f, 0f);
        //     float3 right = math.mul(rotation.ToQuaternion(), new float3(1f, 0f, 0f));
        //     float3 forward = math.normalize(math.cross(right, up));
        //     return quaternion.LookRotationSafe(forward, up).ToDouble3x3();
        // }
    }
}
