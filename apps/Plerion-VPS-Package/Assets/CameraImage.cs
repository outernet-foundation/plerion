using System;
using UnityEngine;

namespace Plerion.VPS
{
    public struct CameraImage
    {
        public int imageWidth;
        public int imageHeight;
        public IntPtr pixelBuffer;
        public Vector2 focalLength;
        public Vector2 principalPoint;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public Quaternion cameraOrientation;
    }
}