

using System;
using Unity.Mathematics;
using UnityEngine;

namespace Plerion
{
    public struct CameraPoseEstimate
    {
        public Vector3 unityWorldCameraPosition; // Unity coordinates
        public Quaternion unityWorldCameraRotation; // Unity coordinates
        public double3 ecefCameraPosition; // ECEF coordinates
        public quaternion ecefCameraRotation; // ECEF coordinates
        public int confidence;
        public Guid map;
    }
}