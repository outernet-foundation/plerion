using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Unity.Mathematics;

using UnityEngine;

using Cysharp.Threading.Tasks;
using PlerionClient.Client;
using System.IO;
using PlerionClient.Api;
using System.Threading.Tasks;
using System.Net.Http;

namespace Plerion.VPS
{
    public static class VisualPositioningSystem
    {
        private class KeycloakHttpHandler : DelegatingHandler
        {
            protected override async System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var token = await Auth.GetOrRefreshToken();
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                return await base.SendAsync(request, cancellationToken);
            }
        }

        const int estimateHistorySize = 128;

        // The height above the ground of the blk2go camera array at during scan initialization
        const float scanneraOriginHeightOffset = 0.15f;

        public static bool FallbackToMostRecentEstimate = false;
        public static bool DiscardBelowAverageConfidenceEstimates = false;
        public static float MinimumPositionThreshold = 0.05f;
        public static float MinimumRotationThreshold = 2f;
        public static float? DetectedFloorHeight = null;
        public static float? EstimatedFloorHeight { get; private set; } = null;
        public static float? FloorHeight => EstimatedFloorHeight ?? DetectedFloorHeight;

        // Do we need these if we're not doing estimate history?
        // public static float RansacPositionInlierThreshold = 0.05f;
        // public static float RansacRotationInlierThreshold = 0.05f;
        // public static float RansacConfidenceFactor = 1.0f;
        // public static int RansacHistorySize = 128;

        public static double4x4 UnityWorldToEcefTransform => unityWorldToEcefTransform;
        public static double4x4 EcefToUnityWorldTransform => ecefToUnityWorldTransform;

        public static event Action OnEcefToUnityWorldTransformUpdated;

        private static DefaultApi api;
        private static CancellationTokenSource startSessionTokenSource = new CancellationTokenSource();
        private static Task startSessionTask;
        private static PlerionClient.Model.LocalizationSessionRead localizationSession;

        private static double4x4 unityWorldToEcefTransform = double4x4.identity;
        private static double4x4 ecefToUnityWorldTransform = math.inverse(double4x4.identity);

        private static async Task StartSessionInternal(CancellationToken cancellationToken = default)
        {
            PlerionClient.Model.LocalizationSessionRead session = default;

            try
            {
                session = await api.CreateLocalizationSessionAsync(cancellationToken);
                await UniTask.SwitchToMainThread(cancellationToken: cancellationToken);
            }
            catch (Exception exc)
            {
                if (session != null)
                    api.DeleteLocalizationSessionAsync(session.Id);

                throw exc;
            }

            localizationSession = session;
        }

        public static void Initialize(string username, string password)
            => Initialize("https://elliot-laptop-api.outernetfoundation.org", username, password); // TODO EP: Replace with actual url

        public static void Initialize(string baseURL, string username, string password)
        {
            Auth.username = username;
            Auth.password = password;
            api = new DefaultApi(
                new HttpClient(new KeycloakHttpHandler() { InnerHandler = new HttpClientHandler() })
                {
                    BaseAddress = new Uri(baseURL)
                },
                baseURL
            );
        }

        public static UniTask StartLocalizationSession()
        {
            if (localizationSession != null)
                return UniTask.CompletedTask;

            if (startSessionTask != null && !startSessionTask.IsCompleted)
                return startSessionTask.AsUniTask();

            startSessionTokenSource?.Dispose();
            startSessionTokenSource = new CancellationTokenSource();
            startSessionTask = StartSessionInternal(startSessionTokenSource.Token);

            return startSessionTask.AsUniTask();
        }

        public static async UniTask LoadLocalizationMaps(params Guid[] maps)
        {
            await api.LoadLocalizationMapsAsync(
                localizationSession.Id,
                maps.ToList(),
                startSessionTokenSource.Token
            );
        }

        public static void StopLocalizationSession()
        {
            startSessionTokenSource?.Cancel();
            startSessionTokenSource?.Dispose();
            startSessionTokenSource = null;

            if (localizationSession != null)
            {
                api.DeleteLocalizationSessionAsync(localizationSession.Id);
                localizationSession = null;
            }
        }

        public static void SetEcefToUnityWorldTransform(double3 position, quaternion rotation)
            => SetUnityWorldToEcefTransform(math.inverse(Double4x4.FromTranslationRotation(position, rotation)));

        public static void SetEcefToUnityWorldTransform(double4x4 ecefToLocalTransform)
            => SetUnityWorldToEcefTransform(math.inverse(ecefToLocalTransform));

        public static void SetUnityWorldToEcefTransform(double3 position, quaternion rotation)
            => SetUnityWorldToEcefTransform(Double4x4.FromTranslationRotation(position, rotation));

        public static void SetUnityWorldToEcefTransform(double4x4 unityWorldToEcefTransform)
        {
            var newPosition = unityWorldToEcefTransform.Position();
            var newRotation = unityWorldToEcefTransform.Rotation();

            var currentPosition = VisualPositioningSystem.unityWorldToEcefTransform.Position();
            var currentRotation = VisualPositioningSystem.unityWorldToEcefTransform.Rotation();

            var distance = math.distance(currentPosition, newPosition);
            var angle = Quaternion.Angle(currentRotation, newRotation);

            if (Application.isPlaying)
            {
                if (distance < MinimumPositionThreshold && angle < MinimumRotationThreshold)
                    return;

                if (distance > MinimumPositionThreshold)
                    Log.Info(LogGroup.VisualPositioningSystem, $"Distance threshold exceeded: {distance}");

                if (angle > MinimumRotationThreshold)
                    Log.Info(LogGroup.VisualPositioningSystem, $"Angle threshold exceeded: {angle}");
            }

            VisualPositioningSystem.unityWorldToEcefTransform = unityWorldToEcefTransform;
            ecefToUnityWorldTransform = math.inverse(unityWorldToEcefTransform);
            OnEcefToUnityWorldTransformUpdated?.Invoke();
        }

        public static (Vector3 position, Quaternion rotation) EcefToUnityWorld(double3 position, quaternion rotation)
        {
            var ecefTransformMatrix = Double4x4.FromTranslationRotation(position, rotation);
            var localTransformMatrix = math.mul(ecefToUnityWorldTransform, ecefTransformMatrix);
            return (
                // Also see Localizer.cs 
                //
                // I never worked out why this position inversion (and the one in
                // SetEcefTransformFromLocalTransform, and the two other ones in Localizer.cs) are
                // required. I just brute-force guess-and-checked until I found something that worked.
                // The ecef rotations for localization maps comes from Cesium, and I searched
                // CesiumForUnity and Cesium-Native for answers, but there are many layers of
                // indirection, and at time of of writing, all origin modes go through a EUN (East-Up-North)
                // coordinate system that I believe gets "undone" by the local Unity transform of the
                // CesiumGeoreference itself. When I realized that, I gave up. But the ultimate
                // result, apparently, is that the required transform to go from ecef space to unity
                // space happens to be a position inversion. ¯\_(ツ)_/¯ 
                //
                // Apologies to the poor soul (probably me) who has to maintain this code in the future.
                -localTransformMatrix.Position().ToFloats(),
                localTransformMatrix.Rotation()
            );
        }

        public static (double3 position, quaternion rotation) UnityWorldToEcef(Vector3 position, Quaternion rotation)
        {
            var localTransformMatrix = Double4x4.FromTranslationRotation(
                // See above
                -position.ToDoubles(),
                rotation
            );

            var ecefTransformMatrix = math.mul(unityWorldToEcefTransform, localTransformMatrix);
            return (ecefTransformMatrix.Position(), ecefTransformMatrix.Rotation());
        }

        public static async UniTask LocalizeFromCameraImage(CameraImage cameraImage, Vector3 cameraPosition, Quaternion cameraRotation)
        {
            if (cameraImage.pixelBuffer == null)
                return;

            // TODO EP: We will need this result to contain the actual map transform.
            var localizationResults = await api.LocalizeImageAsync(
                localizationSession.Id,
                new FileParameter(new MemoryStream(cameraImage.pixelBuffer))
            );

            var localizationResult = localizationResults.FirstOrDefault(); //for now, just use the first one
            var estimatedCameraPosition = localizationResult.Value.Position.ToUnityVector3();
            var estimatedCameraRotation = localizationResult.Value.Rotation.ToUnityQuaternion();

            EstimatedFloorHeight = cameraPosition.y - estimatedCameraPosition.y - scanneraOriginHeightOffset;

            // Up is always up
            cameraRotation = Quaternion.LookRotation(Vector3.Cross(cameraRotation * Vector3.right, Vector3.up), Vector3.up);
            estimatedCameraRotation = Quaternion.LookRotation(Vector3.Cross(estimatedCameraRotation * Vector3.right, Vector3.up), Vector3.up);

            // The ground is always the ground
            if (FloorHeight != null)
                estimatedCameraPosition.y = -FloorHeight.Value + cameraPosition.y - scanneraOriginHeightOffset;

            var cameraTransformLocalSpace = Double4x4.FromTranslationRotation(
                -cameraPosition, // See note in EcefTransform.cs
                cameraRotation
            );

            var estimateCameraTransformMapSpace = Double4x4.FromTranslationRotation(
                -estimatedCameraPosition, // See note in EcefTransform.cs
                estimatedCameraRotation
            );

            var mapEcefTransform = default(double4x4); /*Double4x4.FromTranslationRotation(localizationResult.MapEcefPosition, localizationResult.MapEcefRotation);*/ //TODO EP: Restore once we get the MapEcefPosition and MapEcefRotation
            var cameraEstimateEcefTransform = math.mul(mapEcefTransform, estimateCameraTransformMapSpace);

            SetUnityWorldToEcefTransform(math.mul(cameraEstimateEcefTransform, math.inverse(cameraTransformLocalSpace)));
        }

        public static async UniTask<MapData[]> GetLocalizationMapsAsync(bool includePoints = false, CancellationToken cancellationToken = default)
        {
            var maps = await api.GetLocalizationMapsAsync(cancellationToken: cancellationToken);

            if (!includePoints)
            {
                return maps.Select(x => new MapData()
                {
                    id = x.Id,
                    name = x.Name,
                    ecefPosition = new double3(x.PositionX, x.PositionY, x.RotationZ),
                    ecefRotation = new quaternion((float)x.RotationX, (float)x.RotationY, (float)x.RotationZ, (float)x.RotationZ)
                }).ToArray();
            }

            var mapsByID = maps.ToDictionary(x => x.Id, x => new MapData()
            {
                id = x.Id,
                name = x.Name,
                ecefPosition = new double3(x.PositionX, x.PositionY, x.RotationZ),
                ecefRotation = new quaternion((float)x.RotationX, (float)x.RotationY, (float)x.RotationZ, (float)x.RotationZ)
            });

            await UniTask.WhenAll(
                maps.Select(map => api
                    .GetLocalizationMapPointsAsync(map.Id, cancellationToken: cancellationToken)
                    .AsUniTask()
                    .ContinueWith(points => mapsByID[map.Id].points = points.Select(x => new Point()
                    {
                        position = x.Position.ToUnityVector3(),
                        color = x.Color.ToUnityColor(),
                    }).ToArray())
                )
            );

            return mapsByID.Values.ToArray();
        }

        public static async UniTask<Point[]> GetLocalizationMapPointsAsync(Guid mapID, CancellationToken cancellationToken = default)
        {
            var points = await api.GetLocalizationMapPointsAsync(mapID, cancellationToken);
            return points.Select(x => new Point()
            {
                position = x.Position.ToUnityVector3(),
                color = x.Color.ToUnityColor(),
            }).ToArray();
        }
    }

    public class MapData
    {
        public Guid id;
        public string name;
        public double3 ecefPosition;
        public quaternion ecefRotation;
        public Point[] points;
    }

    public struct Point
    {
        public Color color;
        public Vector3 position;
    }
}