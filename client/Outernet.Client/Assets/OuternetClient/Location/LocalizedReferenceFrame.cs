using System;
using System.Collections.Generic;
using System.Linq;
using CesiumForUnity;
using Cysharp.Threading.Tasks;
using FofX.Stateful;
using Outernet.Shared;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace Outernet.Client.Location
{
    public static class LocalizedReferenceFrame
    {
        // const float thresholdDistance = 0.05f;
        // const float thresholdAngle = 2f;

        const int estimateHistorySize = 128;

        // The height above the ground of the blk2go camera array at during scan initialization
        const float scanneraOriginHeightOffset = 0.15f;

        static private float? floorHeight = null;
        static private double4x4 localToEcefTransform = double4x4.identity;
        static private double4x4 ecefToLocalTransform = double4x4.identity;
        static private Queue<CameraPoseEstimate> estimateHistory = new Queue<CameraPoseEstimate>();
        static private double4x4? targetLocalToEcefTransform = null;
        static private double3 positionVelocity = double3.zero;
        static private Vector3 rotationVelocity = Vector3.zero;
        static private float smoothTime = 0.2f;
        static private HashSet<Anchor> anchors = new HashSet<Anchor>();
        static private HashSet<SyncedAnchor> syncedAnchors = new HashSet<SyncedAnchor>();

        static public bool IsLocalized => targetLocalToEcefTransform.HasValue;

        static public double4x4 LocalToEcefTransform
        {
            get => localToEcefTransform;
            private set
            {
                if (localToEcefTransform.Equals(value))
                    return;

                localToEcefTransform = value;
                ecefToLocalTransform = math.inverse(value);
                onTransformMatriciesChanged?.Invoke();
            }
        }

        static public double4x4 EcefToLocalTransform
        {
            get => ecefToLocalTransform;
            private set
            {
                if (ecefToLocalTransform.Equals(value))
                    return;

                ecefToLocalTransform = value;
                localToEcefTransform = math.inverse(value);
                onTransformMatriciesChanged?.Invoke();
            }
        }

        static public Action onTransformMatriciesChanged;

        static public void AddAnchor(Anchor anchor) => anchors.Add(anchor);
        static public void RemoveAnchor(Anchor anchor) => anchors.Remove(anchor);
        static public void AddSyncedAnchor(SyncedAnchor anchor) => syncedAnchors.Add(anchor);
        static public void RemoveSyncedAnchor(SyncedAnchor anchor) => syncedAnchors.Remove(anchor);

        public static void SetTargetLocalToEcefTransform(double4x4 transform, bool smoothTransition = true)
        {
            targetLocalToEcefTransform = transform;

            if (!smoothTransition)
                LocalToEcefTransform = transform;

            var georeference = GameObject.FindFirstObjectByType<CesiumGeoreference>();
            if (georeference != null)
            {
                var ecefTransform = Utility.LocalToEcef(targetLocalToEcefTransform.Value, new Vector3(0, 0, 0), Quaternion.identity);
                georeference.SetOriginEarthCenteredEarthFixed(
                    ecefTransform.position.x,
                    ecefTransform.position.y,
                    ecefTransform.position.z
                );

                var localRotation = Quaternion.Inverse(ecefTransform.rotation) * Utility.GetEUNRotationFromECEFPosition(ecefTransform.position);
                georeference.transform.rotation = localRotation;
            }

            Update();
        }

        static public (Vector3 position, Quaternion rotation) EcefToLocal(double3 position, quaternion rotation)
            => Utility.EcefToLocal(EcefToLocalTransform, position, rotation);

        static public (double3 position, quaternion rotation) LocalToEcef(Vector3 position, Quaternion rotation)
            => Utility.LocalToEcef(LocalToEcefTransform, position, rotation);

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
            Log.Warn(LogGroup.Localizer, "Tracking origin updated, clearing targetTransform and estimate history");

            targetLocalToEcefTransform = null;
            estimateHistory.Clear();
        }

        static public void Update()
        {
            if (targetLocalToEcefTransform.HasValue &&
                !targetLocalToEcefTransform.Value.Equals(LocalToEcefTransform))
            {
                var targetPosition = targetLocalToEcefTransform.Value.Position();
                var targetRotation = targetLocalToEcefTransform.Value.Rotation();

                var currentPosition = LocalToEcefTransform.Position();
                var currentRotation = LocalToEcefTransform.Rotation();

                var interpolatedPosition = MathUtils.SmoothDampDouble3(currentPosition, targetPosition, ref positionVelocity, smoothTime);
                var interpolatedRotation = MathUtils.SmoothDampQuaternion(currentRotation, targetRotation, ref rotationVelocity, smoothTime);

                LocalToEcefTransform = Double4x4.FromTranslationRotation(interpolatedPosition, interpolatedRotation);

                // Legacy anchor logic for Users and Setting Menu
                foreach (var anchor in syncedAnchors)
                {
                    anchor.RealUpdate();
                }

                foreach (var anchor in anchors)
                {
                    anchor.SetLocalTransformFromEcefTransform();
                }

                foreach (var anchor in syncedAnchors)
                {
                    // If this anchor is locally controlled or animating, our local transform is
                    // authoritative, so don't update it when the localized reference frame changes.
                    if (anchor.State == SyncedAnchor.SyncState.Animating ||
                        anchor.State == SyncedAnchor.SyncState.LocallyControlled)
                    {
                        continue;
                    }

                    anchor.SetLocalTransformFromEcefTransform();
                }
            }
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
                        Log.Info(LogGroup.Localizer, "Estimated floor height: {FloorHeight}", floorHeight);
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

                double4x4? newTargetLocalToEcefTransform = null;

                // if (Settings.localizationReducer == Shared.LocalizationReducer.FAV)
                // {
                //     newTargetLocalToEcefTransform = FAV.Compute(estimates);
                // }
                // else if (Settings.localizationReducer == Shared.LocalizationReducer.RANSAC)
                {
                    newTargetLocalToEcefTransform = RANSAC.Compute(estimates);
                }

                if (!newTargetLocalToEcefTransform.HasValue)
                {
                    if (Settings.fallbackToMostRecent)
                    {
                        newTargetLocalToEcefTransform = estimates.Last().transform;
                    }
                    else
                    {
                        newTargetLocalToEcefTransform = estimates
                            .OrderByDescending(estimate => estimate.estimate.confidence)
                            .First().transform;
                    }
                }

                if (Settings.thresholding == false ||
                    targetLocalToEcefTransform == null)
                {
                    if (targetLocalToEcefTransform == null)
                    {
                        LocalToEcefTransform = newTargetLocalToEcefTransform.Value;
                    }

                    targetLocalToEcefTransform = newTargetLocalToEcefTransform;
                    return;
                }

                var currentTargetPosition = targetLocalToEcefTransform.Value.Position();
                var newTargetPosition = newTargetLocalToEcefTransform.Value.Position();

                var currentTargetRotation = targetLocalToEcefTransform.Value.Rotation();
                var newTargetRotation = newTargetLocalToEcefTransform.Value.Rotation();

                var distance = math.distance(currentTargetPosition, newTargetPosition);
                var angle = Quaternion.Angle(currentTargetRotation, newTargetRotation);

                if (distance > Settings.positionThreshold || angle > Settings.rotationThreshold)
                {
                    if (distance > Settings.positionThreshold)
                    {
                        Log.Info(LogGroup.Localizer, "Distance threshold exceeded: {Distance}", distance);
                    }

                    if (angle > Settings.rotationThreshold)
                    {
                        Log.Info(LogGroup.Localizer, "Angle threshold exceeded: {Angle}", angle);
                    }

                    SetTargetLocalToEcefTransform(newTargetLocalToEcefTransform.Value);
                }
            }
            catch (Exception exception)
            {
                Log.Warn(LogGroup.Localizer, exception, "Failed to apply estimate");
            }
        }

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

        static public float ComputeFloorHeight(CameraPoseEstimate estimate)
        {
            var cameraHeight = estimate.cameraPosition.y;
            var estimatedCameraHeight = estimate.estimatedCameraPosition.y;

            return cameraHeight - estimatedCameraHeight - scanneraOriginHeightOffset;
        }
    }
}