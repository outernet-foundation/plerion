using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Plerion.VPS
{
    public static class Utility
    {
        const double WGS84_A = 6378137.0; // Semi-major axis
        const double WGS84_E_SQUARED = 0.00669437999014; // Eccentricity squared

        public static double3 GpsToEcef(double latitudeRad, double longitudeRad, double altitude)
        {
            double N = WGS84_A / Math.Sqrt(1 - WGS84_E_SQUARED * Math.Sin(latitudeRad) * Math.Sin(latitudeRad));

            double x = (N + altitude) * Math.Cos(latitudeRad) * Math.Cos(longitudeRad);
            double y = (N + altitude) * Math.Cos(latitudeRad) * Math.Sin(longitudeRad);
            double z = (N * (1 - WGS84_E_SQUARED) + altitude) * Math.Sin(latitudeRad);

            return new double3(x, y, z);
        }

        public static (Vector3 position, Quaternion rotation) EcefToLocal(double4x4 ecefToLocalTransform, double3 position, quaternion rotation)
        {
            var ecefTransformMatrix = Double4x4.FromTranslationRotation(position, rotation);
            var localTransformMatrix = math.mul(ecefToLocalTransform, ecefTransformMatrix);
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

        public static (double3 position, quaternion rotation) LocalToEcef(double4x4 localToEcefTransform, Vector3 position, Quaternion rotation)
        {
            var localTransformMatrix = Double4x4.FromTranslationRotation(
                // See above
                -position.ToDoubles(),
                rotation);
            var ecefTransformMatrix = math.mul(localToEcefTransform, localTransformMatrix);
            return (ecefTransformMatrix.Position(), ecefTransformMatrix.Rotation());
        }
    }

    public static class ExtensionMethods
    {
        public static Matrix4x4 SwitchHandedness(this ref Matrix4x4 b)
        {
            Matrix4x4 identity = Matrix4x4.identity;
            identity.m00 = -1f;
            b = identity * b * identity;
            return b;
        }

        public static double4x4 SwitchHandedness(this ref double4x4 b)
        {
            double4x4 identity = double4x4.identity;
            identity.c0.x = -1.0;
            b = math.mul(identity, math.mul(b, identity));
            return b;
        }

        public static Quaternion SwitchHandedness(this ref Quaternion b)
        {
            Matrix4x4 b2 = Matrix4x4.Rotate(b);
            b2.SwitchHandedness();
            b = b2.rotation;
            return b;
        }

        public static Vector3 SwitchHandedness(this ref Vector3 b)
        {
            Matrix4x4 b2 = Matrix4x4.TRS(b, Quaternion.identity, Vector3.one);
            b2.SwitchHandedness();
            b = b2.GetColumn(3);
            return b;
        }
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
        public static Quaternion Rotation(this double4x4 matrix)
        {

            MatrixToRotationAndScale(
                new double3x3(matrix.c0.xyz, matrix.c1.xyz, matrix.c2.xyz),
                out Quaternion rotation,
                out double3 scale);

            return rotation;
        }

        static void MatrixToRotationAndScale(double3x3 matrix, out Quaternion rotation, out double3 scale)
        {
            double lengthColumn0 = math.length(matrix.c0);
            double lengthColumn1 = math.length(matrix.c1);
            double lengthColumn2 = math.length(matrix.c2);

            float3x3 rotationMatrix = new float3x3(
                (float3)(matrix.c0 / lengthColumn0),
                (float3)(matrix.c1 / lengthColumn1),
                (float3)(matrix.c2 / lengthColumn2));

            scale = new double3(lengthColumn0, lengthColumn1, lengthColumn2);

            double3 cross = math.cross(matrix.c0, matrix.c1);
            if (math.dot(cross, matrix.c2) < 0.0)
            {
                rotationMatrix *= -1.0f;
                scale *= -1.0f;
            }

            rotation = math.quaternion(rotationMatrix);
        }
    }
}