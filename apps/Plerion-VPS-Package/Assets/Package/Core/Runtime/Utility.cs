using Unity.Mathematics;
using UnityEngine;

using System;
using System.Collections.Generic;

namespace Plerion.VPS
{
    public static class Utility
    {
        const double WGS84_A = 6378137.0; // Semi-major axis
        const double WGS84_E_SQUARED = 0.00669437999014; // Eccentricity squared
        private static readonly double3 WGS84_One_Over_Radii_Squared = new double3(
            1.0 / (6378137.0 * 6378137.0),
            1.0 / (6378137.0 * 6378137.0),
            1.0 / (6356752.3142451793 * 6356752.3142451793)
        );

        public static double4x4 LlhToEcefTransformationMatrix(double latitude, double longitude, double height)
            => EcefPositionToTransformationMatrix(LlhToEcefPosition(latitude, longitude, height));

        public static double4x4 EcefPositionToTransformationMatrix(double3 ecefPosition)
            => Double4x4.FromTranslationRotation(ecefPosition, EcefPositionToEunRotation(ecefPosition));

        public static double3 LlhToEcefPosition(double latitude, double longitude, double height)
        {
            latitude *= Mathf.Deg2Rad;
            longitude *= Mathf.Deg2Rad;

            double N = WGS84_A / Math.Sqrt(1 - WGS84_E_SQUARED * Math.Sin(latitude) * Math.Sin(latitude));

            double x = (N + height) * Math.Cos(latitude) * Math.Cos(longitude);
            double y = (N + height) * Math.Cos(latitude) * Math.Sin(longitude);
            double z = (N * (1 - WGS84_E_SQUARED) + height) * Math.Sin(latitude);

            return new double3(x, y, z);
        }

        // Adapted from eastNorthUpToFixedFrame in this file https://github.com/CesiumGS/cesium-native/blob/main/CesiumGeospatial/src/GlobeTransforms.cpp
        // and Ellipsoid at https://github.com/CesiumGS/cesium-native/blob/main/CesiumGeospatial/src/Ellipsoid.cpp
        public static Quaternion EcefPositionToEunRotation(double3 ecefPosition)
        {
            var up = math.normalize(ecefPosition * WGS84_One_Over_Radii_Squared);
            var east = math.normalize(new double3(-ecefPosition.y, ecefPosition.x, 0));
            var north = math.cross(up, east);

            // Negatives here are to account for handedness difference
            return Quaternion.LookRotation(-north.ToFloats(), -up.ToFloats());
        }

        static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        public static long ToLong(this Color color)
        {
            int a = Clamp((int)(color.a * 65535), 0, 65535);
            int r = Clamp((int)(color.r * 65535), 0, 65535);
            int g = Clamp((int)(color.g * 65535), 0, 65535);
            int b = Clamp((int)(color.b * 65535), 0, 65535);

            return ((long)a << 48) | ((long)r << 32) | ((long)g << 16) | (long)b;
        }

        public static Color ToColor(long colorLong)
        {
            float a = ((colorLong >> 48) & 0xFFFF) / 65535f;
            float r = ((colorLong >> 32) & 0xFFFF) / 65535f;
            float g = ((colorLong >> 16) & 0xFFFF) / 65535f;
            float b = (colorLong & 0xFFFF) / 65535f;

            return new Color(r, g, b, a);
        }

        public static Color ToUnityColor(this PlerionClient.Model.Color color)
        {
            return new Color(color.R / 255f, color.G / 255f, color.B / 255f);
        }

        public static Vector3 ToUnityVector3(this PlerionClient.Model.Vector3 vec)
        {
            return new Vector3((float)vec.X, (float)vec.Y, (float)vec.Z);
        }

        public static Quaternion ToUnityQuaternion(this PlerionClient.Model.Quaternion quat)
        {
            return new Quaternion((float)quat.X, (float)quat.Y, (float)quat.Z, (float)quat.W);
        }
    }

    public static class ExtensionMethods
    {
        public static List<double> ToList(this Vector4 vec)
            => new List<double>() { vec.x, vec.y, vec.z, vec.w };

        public static List<double> ToList(this Quaternion quat)
            => new List<double>() { quat.x, quat.y, quat.z, quat.w };

        public static List<double> ToList(this Vector3 vec)
            => new List<double>() { vec.x, vec.y, vec.z };

        public static double3 ToDouble3(this List<double> doubles)
            => new double3((float)doubles[0], (float)doubles[1], (float)doubles[2]);

        public static Vector3 ToVector3(this List<double> doubles)
            => new Vector3((float)doubles[0], (float)doubles[1], (float)doubles[2]);

        public static Quaternion ToQuaternion(this List<double> doubles)
            => new Quaternion((float)doubles[0], (float)doubles[1], (float)doubles[2], (float)doubles[3]);
    }

    public static class Double4x4
    {
        public static Vector3 ToFloats(this double3 vector) => new Vector3((float)vector.x, (float)vector.y, (float)vector.z);
        public static double3 ToDoubles(this Vector3 vector) => new double3(vector.x, vector.y, vector.z);

        public static double4x4 FromTranslationRotation(Vector3 translation, Quaternion rotation)
        {
            return FromTranslationRotation(
                new double3(translation.x, translation.y, translation.z),
                new quaternion(rotation.x, rotation.y, rotation.z, rotation.w)
            );
        }

        public static double4x4 FromTranslationRotation(PlerionClient.Model.Vector3 translation, PlerionClient.Model.Quaternion rotation)
        {
            return FromTranslationRotationScale(
                new double3(translation.X, translation.Y, translation.Z),
                new quaternion((float)rotation.X, (float)rotation.Y, (float)rotation.Z, (float)rotation.W),
                new double3(1.0, 1.0, 1.0)
            );
        }

        public static double4x4 FromTranslationRotation(double3 translation, quaternion rotation)
        {
            return FromTranslationRotationScale(translation, rotation, new double3(1.0, 1.0, 1.0));
        }

        public static double4x4 FromTranslationRotationScale(double3 translation, quaternion rotation, double3 scale)
        {
            double3x3 scaleMatrix = new double3x3(
                new double3(scale.x, 0.0, 0.0),
                new double3(0.0, scale.y, 0.0),
                new double3(0.0, 0.0, scale.z));
            double3x3 rotationMatrix = new float3x3(rotation);
            double3x3 scaleAndRotate = math.mul(rotationMatrix, scaleMatrix);

            return new double4x4(
                new double4(scaleAndRotate.c0, 0.0),
                new double4(scaleAndRotate.c1, 0.0),
                new double4(scaleAndRotate.c2, 0.0),
                new double4(translation, 1.0));
        }

        public static double3 Position(this double4x4 matrix) => matrix.c3.xyz;

        public static quaternion Rotation(this double4x4 matrix)
            => new quaternion(new float3x3(math.normalize(matrix.c0.xyz).ToFloats(), math.normalize(matrix.c1.xyz).ToFloats(), math.normalize(matrix.c2.xyz).ToFloats()));
    }
}