using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using static Outernet.Shared.Assert;

namespace Outernet.Client.Location
{
    static public class FAV
    {
        static public double4x4? Compute(
            IEnumerable<(
                CameraPoseEstimate estimate,
                float normalizedConfidence,
                double4x4 transform
            )> estimates)
        {
            estimates = estimates.Reverse().Take(Settings.favHistorySize);

            var positions = estimates.Select(estimate => estimate.transform.Position()).ToList();
            var rotations = estimates.Select(estimate => estimate.transform.Rotation()).ToList();

            var (averagePosition, averageRotation) = ComputeAverage(positions, rotations);

            // If there are only two estimates, return the average
            if (rotations.Count <= 2)
            {
                return Double4x4.FromTranslationRotation(averagePosition, averageRotation);
            }

            // Compute the variance of the estimates
            var positionDeviations =
                positions.Select(position => math.lengthsq(position - averagePosition)).ToList();
            var rotationDeviations =
                rotations.Select(rotation => Quaternion.Angle(rotation, averageRotation)).ToList();
            double positionStandardDeviation = math.sqrt(positionDeviations.Average());
            double rotationStandardDeviation = rotationDeviations.Average();

            // Filter out estimates that are too far from the average
            var filteredPositions = new List<double3>();
            var filteredRotations = new List<Quaternion>();

            var numFilteredByPosition = 0;
            var numFilteredByRotation = 0;

            var positionThreshold = positionStandardDeviation * Settings.favStandardDeviationsThreshold;
            var rotationThreshold = rotationStandardDeviation * Settings.favStandardDeviationsThreshold;

            for (int i = 0; i < estimates.Count(); i++)
            {
                if (positionDeviations[i] > positionThreshold)
                {
                    numFilteredByPosition++;
                }

                if (rotationDeviations[i] > rotationThreshold)
                {
                    numFilteredByRotation++;
                }

                if (positionDeviations[i] <= positionThreshold &&
                    rotationDeviations[i] <= rotationThreshold)
                {
                    filteredPositions.Add(positions[i]);
                    filteredRotations.Add(rotations[i]);
                }
            }

            Log.Info(
                LogGroup.Localizer,
                "Position standard deviation: {PositionStandardDeviation}, " +
                "Rotation standard deviation: {RotationStandardDeviation}, " +
                "{Total} total estimates, " +
                "{Filtered} filtered estimates, " +
                "{FilteredByPosition} filtered by position, " +
                "{FilteredByRotation} filtered by rotation",
                positionStandardDeviation,
                rotationStandardDeviation,
                estimates.Count(),
                filteredPositions.Count,
                numFilteredByPosition,
                numFilteredByRotation);

            // If there are no valid estimates, bail
            if (filteredPositions.Count == 0)
            {
                Log.Warn(LogGroup.Localizer, "No estimates after filtering, bailing out");
                return null;
            }

            // Compute the average position and rotation of the valid estimates
            (averagePosition, averageRotation) = ComputeAverage(filteredPositions, filteredRotations);

            return Double4x4.FromTranslationRotation(averagePosition, averageRotation);
        }

        static private (double3 position, Quaternion rotation) ComputeAverage(List<double3> positions, List<Quaternion> rotations)
        {
            ASSERT(positions.Count == rotations.Count, "Mismatched position and rotation counts");

            var positionSum = positions[0];
            var averageRotation = rotations[0];

            for (int i = 1; i < rotations.Count; i++)
            {
                positionSum += positions[i];
                averageRotation = Quaternion.Slerp(averageRotation, rotations[i], 1.0f / (i + 1));
            }

            return (positionSum / positions.Count, averageRotation);
        }
    }
}
