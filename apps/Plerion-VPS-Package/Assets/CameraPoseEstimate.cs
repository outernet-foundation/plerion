

using UnityEngine;

namespace Plerion.VPS
{
    public struct CameraPoseEstimate
    {
        // public double4x4 localToEcefTransform;
        public Vector3 cameraPosition; // Unity coordinates
        public Quaternion cameraRotation; // Unity coordinates
        public Vector3 estimatedCameraPosition; // Immersal Map coordinates
        public Quaternion estimatedCameraRotation; // Immersal Map coordinates
        public int confidence;
        public LocalizationMapModel map;
    }
}