using System;
using System.Collections.Generic;
using System.Linq;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Cysharp.Threading.Tasks;

namespace Plerion.VPS
{
    public static class LocalizedReferenceFrame
    {
        const int estimateHistorySize = 128;

        // The height above the ground of the blk2go camera array at during scan initialization
        const float scanneraOriginHeightOffset = 0.15f;

        static private float? floorHeight = null;
        static private double4x4 localToEcefTransform = double4x4.identity;
        static private double4x4 ecefToLocalTransform = double4x4.identity;
        static private Queue<CameraPoseEstimate> estimateHistory = new Queue<CameraPoseEstimate>();

        static public event Action onReferenceFrameChanged;

        static public void Initialize()
        {
            UniTask
                .WaitUntil(() => XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XRInputSubsystem>() != null)
                .ContinueWith(() =>
                {
                    XRInputSubsystem inputSubsystem = XRGeneralSettings.Instance.Manager.activeLoader.GetLoadedSubsystem<XRInputSubsystem>();
                    inputSubsystem.trackingOriginUpdated += TrackingOriginUpdated;
                });
        }

        static private void TrackingOriginUpdated(XRInputSubsystem subsystem)
        {
            Debug.LogWarning("Tracking origin updated, clearing estimate history");
            estimateHistory.Clear();
        }

        // Adapted from eastNorthUpToFixedFrame in this file https://github.com/CesiumGS/cesium-native/blob/main/CesiumGeospatial/src/GlobeTransforms.cpp
        static private Quaternion GetEUNRotationFromECEFPosition(double3 ecefPosition)
        {
            var up = CesiumWgs84Ellipsoid.GeodeticSurfaceNormal(ecefPosition);
            var east = math.normalize(new double3(-ecefPosition.y, ecefPosition.x, 0));
            var north = math.cross(up, east);

            // Negatives here are to account for handedness difference
            return Quaternion.LookRotation(-north.ToFloats(), -up.ToFloats());
        }

        static public void ApplyEstimate(CameraPoseEstimate estimate)
        {
            estimateHistory.Enqueue(estimate);

            if (estimateHistory.Count > estimateHistorySize) estimateHistory.Dequeue();

            var orderedEstiamtes = estimateHistory
                .OrderByDescending(estimate => estimate.confidence)
                .ToList();

            float range = orderedEstiamtes.First().confidence - orderedEstiamtes.Last().confidence;

            try
            {
                var filteredEstimateHistory = estimateHistory.Select(estimate => estimate);

                if (Settings.discardBottomHalf)
                {
                    // Discard estimates below the average confidence
                    var averageConfidence =
                        filteredEstimateHistory.Select(estimate => estimate.confidence).Average();
                    filteredEstimateHistory =
                        filteredEstimateHistory.Where(estimate => estimate.confidence >= averageConfidence);
                }

                // If we have any horizontal planes, use the one closest to the estimated floor height
                if (Settings.useFloorPlaneDetection && PlaneDetector.Planes.Count != 0)
                {
                    var estimatedFloorHeight = RANSACFloat.Compute(estimateHistory.Select(estimate =>
                        (
                            estimate.cameraPosition.y - estimate.estimatedCameraPosition.y - scanneraOriginHeightOffset,
                            normalizedConfidence:
                            (estimate.confidence - orderedEstiamtes.Last().confidence) / range
                        )
                    ));

                    floorHeight = estimatedFloorHeight.HasValue ?
                        PlaneDetector.Planes
                            .Select(plane => plane.center.y)
                            .OrderBy(height => Math.Abs(height - estimatedFloorHeight.Value))
                            .First() :
                        null;

                    if (floorHeight.HasValue)
                    {
                        Log.Info(LogGroup.Localizer, $"Estimated floor height: {floorHeight}");
                    }
                }

                var estimates = filteredEstimateHistory.Select(estimate => (
                    estimate,
                    normalizedConfidence:
                        (estimate.confidence - orderedEstiamtes.Last().confidence) / range,
                    // Compute the local to ecef transform for each estimate (needs to be be
                    // recomputed when floor plane height changes, so for simplicity just always
                    // recompute)
                    transform: ComputeLocalToEcefTransform(estimate)
                ));

                double4x4? newLocalToEcefTransform = null;

                // if (Settings.localizationReducer == Shared.LocalizationReducer.FAV)
                // {
                //     newTargetLocalToEcefTransform = FAV.Compute(estimates);
                // }
                // else if (Settings.localizationReducer == Shared.LocalizationReducer.RANSAC)
                {
                    newLocalToEcefTransform = RANSAC.Compute(estimates);
                }

                if (!newLocalToEcefTransform.HasValue)
                {
                    if (Settings.fallbackToMostRecent)
                    {
                        newLocalToEcefTransform = estimates.Last().transform;
                    }
                    else
                    {
                        newLocalToEcefTransform = estimates
                            .OrderByDescending(estimate => estimate.estimate.confidence)
                            .First().transform;
                    }
                }

                var currentTargetPosition = localToEcefTransform.Position();
                var newTargetPosition = newLocalToEcefTransform.Value.Position();

                var currentTargetRotation = localToEcefTransform.Rotation();
                var newTargetRotation = newLocalToEcefTransform.Value.Rotation();

                var distance = math.distance(currentTargetPosition, newTargetPosition);
                var angle = Quaternion.Angle(currentTargetRotation, newTargetRotation);

                if (distance < Settings.minimumPositionThreshold && angle < Settings.minimumRotationThreshold)
                    return;

                if (distance > Settings.minimumPositionThreshold)
                    Log.Info(LogGroup.Localizer, $"Distance threshold exceeded: {distance}");

                if (angle > Settings.minimumRotationThreshold)
                    Log.Info(LogGroup.Localizer, $"Angle threshold exceeded: {angle}");

                var georeference = GameObject.FindFirstObjectByType<CesiumGeoreference>();
                if (georeference != null)
                {
                    var ecefTransform = Utility.LocalToEcef(newLocalToEcefTransform.Value, new Vector3(0, 0, 0), Quaternion.identity);
                    georeference.SetOriginEarthCenteredEarthFixed(
                        ecefTransform.position.x,
                        ecefTransform.position.y,
                        ecefTransform.position.z
                    );

                    var localRotation = Quaternion.Inverse(ecefTransform.rotation) * GetEUNRotationFromECEFPosition(ecefTransform.position);
                    georeference.transform.rotation = localRotation;
                }

                localToEcefTransform = newLocalToEcefTransform.Value;
                ecefToLocalTransform = math.inverse(localToEcefTransform);
                onReferenceFrameChanged?.Invoke();
            }
            catch (Exception exception)
            {
                Log.Error(LogGroup.Localizer, "Failed to apply estimate", exception);
            }
        }

        static public (Vector3 position, Quaternion rotation) EcefToLocal(double3 position, quaternion rotation)
            => Utility.EcefToLocal(ecefToLocalTransform, position, rotation);

        static public (double3 position, quaternion rotation) LocalToEcef(Vector3 position, Quaternion rotation)
            => Utility.LocalToEcef(localToEcefTransform, position, rotation);

        static public double4x4 ComputeLocalToEcefTransform(CameraPoseEstimate estimate)
        {
            var cameraPosition = estimate.cameraPosition;
            var cameraRotation = estimate.cameraRotation;
            var estimatedCameraPosition = estimate.estimatedCameraPosition;
            var estimatedCameraRotation = estimate.estimatedCameraRotation;

            // Up is always up
            cameraRotation = Quaternion.LookRotation(Vector3.Cross(cameraRotation * Vector3.right, Vector3.up), Vector3.up);
            estimatedCameraRotation = Quaternion.LookRotation(Vector3.Cross(estimatedCameraRotation * Vector3.right, Vector3.up), Vector3.up);

            // The ground is always the ground
            if (Settings.useFloorPlaneDetection && floorHeight != null)
            {
                estimatedCameraPosition.y = -floorHeight.Value + cameraPosition.y - scanneraOriginHeightOffset;
            }

            var cameraTransformLocalSpace = Double4x4.FromTranslationRotation(
                -cameraPosition, // See note in EcefTransform.cs
                cameraRotation);

            var estimateCameraTransformMapSpace = Double4x4.FromTranslationRotation(
                -estimatedCameraPosition, // See note in EcefTransform.cs
                estimatedCameraRotation);

            var mapEcefTransform = Double4x4.FromTranslationRotation(estimate.map.EcefPosition, estimate.map.EcefRotation);

            var cameraEstimateEcefTransform = math.mul(mapEcefTransform, estimateCameraTransformMapSpace);

            return math.mul(cameraEstimateEcefTransform, math.inverse(cameraTransformLocalSpace));
        }
    }
}