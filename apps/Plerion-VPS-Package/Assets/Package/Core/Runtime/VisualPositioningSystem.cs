using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Plerion.VPS;
using PlerionApiClient.Api;
using PlerionApiClient.Client;
using PlerionApiClient.Model;
using Unity.Mathematics;
using UnityEngine;
using Camera = PlerionApiClient.Model.Camera;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace Plerion.VPS
{
    public static class VisualPositioningSystem
    {
        class MapData
        {
            public bool loaded;
            public LocalizationMapRead map;
            public ReconstructionVisualizer reconstructionVisualizer;
        }

        private static ICameraProvider _cameraProvider = null;
        private static Dictionary<Guid, MapData> _maps = new Dictionary<Guid, MapData>();
        private static DefaultApi api;
        private static CancellationTokenSource startSessionTokenSource = new CancellationTokenSource();
        private static Task startSessionTask;
        private static Guid localizationSessionId = Guid.Empty;
        private static double4x4 unityFromEcefTransform = double4x4.identity;
        private static double4x4 ecefFromUnityTransform = math.inverse(unityFromEcefTransform);
        private static Action<string> _logCallback;
        private static Action<string> _warnCallback;
        private static Action<string> _errorCallback;
        private static Action<string, Exception> _logExceptionCallback;

        public static Prefabs Prefabs { get; private set; }
        public static bool LocalizationSessionActive => localizationSessionId != Guid.Empty;
        public static double4x4 EcefToUnityWorldTransform => unityFromEcefTransform;
        public static double4x4 UnityWorldToEcefTransform => ecefFromUnityTransform;
        public static event Action OnEcefToUnityWorldTransformUpdated;

        internal static void LogDebug(string message) => _logCallback?.Invoke(message);

        internal static void LogWarn(string message) => _warnCallback?.Invoke(message);

        internal static void LogError(string message) => _errorCallback?.Invoke(message);

        internal static void LogException(string message, Exception exception = null) =>
            _logExceptionCallback?.Invoke(message, exception);

        public static void Initialize(
            ICameraProvider cameraProvider,
            string apiUrl,
            string authUrl,
            string authClient,
            Action<string> logCallback,
            Action<string> warnCallback,
            Action<string> errorCallback,
            Action<string, Exception> logException
        )
        {
            _cameraProvider = cameraProvider;
            _logCallback = logCallback;
            _warnCallback = warnCallback;
            _errorCallback = errorCallback;
            _logExceptionCallback = logException;

            Prefabs = Resources.Load<Prefabs>("PrefabReferences");

            Auth.Initialize(authUrl, authClient, logCallback, warnCallback, errorCallback);

            api = new DefaultApi(
                new HttpClient(new AuthHttpHandler() { InnerHandler = new HttpClientHandler() })
                {
                    BaseAddress = new Uri(apiUrl),
                },
                apiUrl
            );
        }

        public static async UniTask Login(string username, string password) => await Auth.Login(username, password);

        public static UniTask StartLocalizationSession()
        {
            if (localizationSessionId != Guid.Empty)
                return UniTask.CompletedTask;

            if (startSessionTask != null && !startSessionTask.IsCompleted)
                return startSessionTask.AsUniTask();

            startSessionTokenSource?.Dispose();
            startSessionTokenSource = new CancellationTokenSource();
            startSessionTask = StartSessionInternal(startSessionTokenSource.Token);

            return startSessionTask.AsUniTask();
        }

        private static async Task StartSessionInternal(CancellationToken cancellationToken = default)
        {
            LocalizationSessionRead session = default;

            try
            {
                session = await api.CreateLocalizationSessionAsync(cancellationToken);

                while (true)
                {
                    var status = await api.GetLocalizationSessionStatusAsync(session.Id, cancellationToken);

                    if (status == "\"failed\"" || status == "\"exited\"")
                        throw new Exception("Session failed to start.");

                    if (status == "\"ready\"")
                        break;

                    await UniTask.WaitForSeconds(1, cancellationToken: cancellationToken);
                }

                var cameraConfig = await _cameraProvider.GetCameraConfig();
                await api.SetLocalizationSessionCameraIntrinsicsAsync(
                    session.Id,
                    new Camera(cameraConfig),
                    cancellationToken
                );

                await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);
            }
            catch (Exception exc)
            {
                if (session != null)
                    api.DeleteLocalizationSessionAsync(session.Id).AsUniTask().Forget();

                throw exc;
            }

            localizationSessionId = session.Id;
        }

        public static void StopLocalizationSession()
        {
            startSessionTokenSource?.Cancel();
            startSessionTokenSource?.Dispose();
            startSessionTokenSource = null;

            if (localizationSessionId != Guid.Empty)
            {
                api.DeleteLocalizationSessionAsync(localizationSessionId).AsUniTask().Forget();
                localizationSessionId = Guid.Empty;
            }
        }

        public static UniTask AddLocalizationMaps(IEnumerable<Guid> maps) => AddLocalizationMaps(maps.ToList());

        public static UniTask AddLocalizationMaps(params Guid[] maps) => AddLocalizationMaps(maps.ToList());

        public static async UniTask AddLocalizationMaps(List<Guid> maps)
        {
            var tasks = new List<UniTask>();

            foreach (var mapId in maps)
            {
                if (_maps.ContainsKey(mapId))
                    throw new Exception($"Map {mapId} is already added");
            }

            // Create task to load all maps into localization session
            tasks.Add(api.LoadLocalizationMapsAsync(localizationSessionId, maps).AsUniTask());

            foreach (var mapId in maps)
            {
                _maps[mapId] = new MapData()
                {
                    reconstructionVisualizer = GameObject.Instantiate(
                        Prefabs.mapRendererPrefab,
                        Vector3.zero,
                        Quaternion.identity
                    ),
                };

                // Create task to fetch metadata
                tasks.Add(
                    api.GetLocalizationMapAsync(mapId)
                        .AsUniTask()
                        .ContinueWith(async map =>
                        {
                            _maps[mapId].loaded = false;
                            _maps[mapId].map = map;
                            _maps[mapId]
                                .reconstructionVisualizer.GetComponent<Anchor>()
                                .SetEcefTransform(
                                    new double3(map.PositionX, map.PositionY, map.PositionZ),
                                    new quaternion(
                                        (float)map.RotationX,
                                        (float)map.RotationY,
                                        (float)map.RotationZ,
                                        (float)map.RotationW
                                    )
                                );

                            await _maps[mapId].reconstructionVisualizer.Load(api, _maps[mapId].map.ReconstructionId);
                        })
                );
            }

            // Run all tasks in parallel and wait for them to complete
            await UniTask.WhenAll(tasks);

            // Mark maps as loaded
            foreach (var mapId in maps)
            {
                _maps[mapId].loaded = true;
            }
        }

        public static async UniTask RemoveLocalizationMap(Guid map)
        {
            if (!_maps.ContainsKey(map))
                throw new Exception($"Map {map} has not been added");
            if (!_maps[map].loaded)
                throw new Exception($"Map {map} has not finished loading");

            var mapData = _maps[map];
            GameObject.Destroy(mapData.reconstructionVisualizer.gameObject);
            _maps.Remove(map);

            await api.UnloadMapAsync(localizationSessionId, map);
        }

        public static bool Enabled { get; private set; } = false;
        static CancellationTokenSource _cancellationTokenSource;

        public static void StartLocalizing()
        {
            if (Enabled)
                return;

            if (_cameraProvider == null)
                throw new Exception("Camera provider must be set before calling Start.");

            Enabled = true;

            _cameraProvider.Start();

            _cancellationTokenSource = new CancellationTokenSource();
            ExecuteCameraLocalization(_cancellationTokenSource.Token).Forget();
        }

        public static void StopLocalizing()
        {
            Enabled = false;

            _cameraProvider.Stop();

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private static async UniTask ExecuteCameraLocalization(CancellationToken cancellationToken = default)
        {
            int lastFrameCount = Time.frameCount;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var (cameraImage, cameraPosition, cameraRotation) = await _cameraProvider.GetFrameJPG();

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);

                    if (cameraImage != null)
                    {
                        await LocalizeFromCameraImage(cameraImage, cameraPosition, cameraRotation);
                    }

                    // if this process doesn't take at least one frame,
                    // this loop will become infinite and block the main thread
                    if (Time.frameCount == lastFrameCount)
                        await UniTask.WaitForEndOfFrame();

                    lastFrameCount = Time.frameCount;
                }
                catch (Exception exception)
                {
                    if (exception is TaskCanceledException)
                        break;

                    LogException("Exception thrown during localization", exception);
                }
            }
        }

        public static async UniTask LocalizeFromCameraImage(
            byte[] image,
            Vector3 cameraTranslationUnityWorldFromCamera,
            Quaternion cameraRotationUnityWorldFromCamera
        )
        {
            if (image == null)
                return;

            var localizationResults = await api.LocalizeImageAsync(
                localizationSessionId,
                AxisConvention.UNITY,
                new FileParameter(new MemoryStream(image))
            );

            if (localizationResults.Count == 0)
            {
                LogDebug("Localization failed");
                return;
            }

            var localizationResult = localizationResults.FirstOrDefault(); //for now, just use the first one

            unityFromEcefTransform = LocationUtilities.ComputeUnityFromEcefTransform(
                localizationResult.CameraFromMapTransform.Position.ToDouble3(),
                localizationResult.CameraFromMapTransform.Rotation.ToMathematicsQuaternion().ToDouble3x3(),
                localizationResult.MapTransform.Position.ToDouble3(),
                localizationResult.MapTransform.Rotation.ToMathematicsQuaternion().ToDouble3x3(),
                cameraTranslationUnityWorldFromCamera,
                // TODO: Adjust unity rotation to account for phone orientation (portrait vs landscape)
                math.mul(
                    ((quaternion)cameraRotationUnityWorldFromCamera).ToDouble3x3(),
                    quaternion.AxisAngle(new float3(0f, 0f, 1f), math.radians(0f)).ToDouble3x3()
                )
            );
            ecefFromUnityTransform = math.inverse(unityFromEcefTransform);

            OnEcefToUnityWorldTransformUpdated?.Invoke();
        }

        public static (Vector3 position, Quaternion rotation) EcefToUnityWorld(
            double3 ecefPosition,
            quaternion ecefRotation
        )
        {
            var (position, rotation) = LocationUtilities.UnityFromEcef(
                unityFromEcefTransform,
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
                ecefFromUnityTransform,
                new double3(position.x, position.y, position.z),
                rotation
            );
    }
}
