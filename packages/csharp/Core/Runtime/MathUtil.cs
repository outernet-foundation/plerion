using Unity.Mathematics;

namespace Plerion.Core
{
    public static class Double4x4
    {
        public static double4x4 FromTranslationRotationScale(double3 translation, double3x3 rotation, double3 scale)
        {
            double3x3 scaleMatrix = new double3x3(
                new double3(scale.x, 0.0, 0.0),
                new double3(0.0, scale.y, 0.0),
                new double3(0.0, 0.0, scale.z));
            double3x3 scaleAndRotate = math.mul(rotation, scaleMatrix);

            return new double4x4(
                new double4(scaleAndRotate.c0, 0.0),
                new double4(scaleAndRotate.c1, 0.0),
                new double4(scaleAndRotate.c2, 0.0),
                new double4(translation, 1.0));
        }

        public static double4x4 FromTranslationRotation(double3 translation, quaternion rotation)
        {
            return FromTranslationRotationScale(translation, rotation.ToDouble3x3(), new double3(1.0, 1.0, 1.0));
        }

        public static double4x4 FromTranslationRotation(double3 translation, double3x3 rotation)
        {
            return FromTranslationRotationScale(translation, rotation, new double3(1.0, 1.0, 1.0));
        }
    }

    public static class ExtensionMethods
    {
        public static double3x3 ToDouble3x3(this quaternion quat)
        {
            return new double3x3(new float3x3(quat.value));
        }

        public static quaternion ToQuaternion(this double3x3 matrix)
        {
            return new quaternion(new float3x3(matrix));
        }

        public static double3 ToFloats(this double3 vector) => new double3((float)vector.x, (float)vector.y, (float)vector.z);

        public static double3 Position(this double4x4 matrix) => matrix.c3.xyz;

        public static quaternion Rotation(this double4x4 matrix)
            => new double3x3(math.normalize(matrix.c0.xyz).ToFloats(), math.normalize(matrix.c1.xyz).ToFloats(), math.normalize(matrix.c2.xyz).ToFloats()).ToQuaternion();
    }

    public static class LocationUtilities
    {
        private static readonly double3x3 basisUnity = double3x3.identity;
        private static readonly double3x3 basisOpenCV = new double3x3(
            1f, 0f, 0f,
            0f, -1f, 0f,
            0f, 0f, 1f
        );
        private static readonly double3x3 basisEcef = new double3x3(
            1f, 0f, 0f,
            0f, -1f, 0f,
            0f, 0f, 1f
        );

        private static readonly double3x3 basisChangeUnityFromOpenCV = math.mul(math.transpose(basisUnity), basisOpenCV);
        private static readonly double3x3 basisChangeOpenCVFromUnity = math.transpose(basisChangeUnityFromOpenCV);
        private static readonly double3x3 basisChangeUnityFromEcef = math.mul(math.transpose(basisUnity), basisEcef);
        private static readonly double3x3 basisChangeEcefFromUnity = math.transpose(basisChangeUnityFromEcef);

        public static (double3, double3x3) ChangeBasisOpenCVToUnity(double3 translation, double3x3 rotation)
            => (math.mul(basisChangeUnityFromOpenCV, translation), math.mul(basisChangeUnityFromOpenCV, math.mul(rotation, basisChangeOpenCVFromUnity)));

        public static (double3, double3x3) ChangeBasisUnityToOpenCV(double3 translation, double3x3 rotation)
            => (math.mul(basisChangeOpenCVFromUnity, translation), math.mul(basisChangeOpenCVFromUnity, math.mul(rotation, basisChangeUnityFromOpenCV)));

        public static (double3, double3x3) ChangeBasisEcefToUnity(double3 translation, double3x3 rotation)
            => (math.mul(basisChangeUnityFromEcef, translation), math.mul(basisChangeUnityFromEcef, math.mul(rotation, basisChangeEcefFromUnity)));

        public static (double3, double3x3) ChangeBasisUnityToEcef(double3 translation, double3x3 rotation)
            => (math.mul(basisChangeEcefFromUnity, translation), math.mul(basisChangeEcefFromUnity, math.mul(rotation, basisChangeUnityFromEcef)));

        public static double4x4 ComputeUnityFromEcefTransform(
            double3 translationCameraFromMap,
            double3x3 rotationCameraFromMap,
            double3 translationEcefFromMap,
            double3x3 rotationEcefFromMap,
            float3 translationUnityWorldFromCamera,
            double3x3 rotationUnityWorldFromCamera
        )
        {
            (translationCameraFromMap, rotationCameraFromMap) = ChangeBasisOpenCVToUnity(translationCameraFromMap, rotationCameraFromMap);
            (translationEcefFromMap, rotationEcefFromMap) = ChangeBasisEcefToUnity(translationEcefFromMap, rotationEcefFromMap);

            var rotationUnityFromMap = math.mul(rotationUnityWorldFromCamera, rotationCameraFromMap);
            var translationUnityFromMap = math.mul(rotationUnityWorldFromCamera, translationCameraFromMap) + translationUnityWorldFromCamera;
            var transformUnityFromMap = Double4x4.FromTranslationRotation(translationUnityFromMap, rotationUnityFromMap);
            var transformEcefFromMap = Double4x4.FromTranslationRotation(translationEcefFromMap, rotationEcefFromMap);
            var transformMapFromEcef = math.inverse(transformEcefFromMap);

            return math.mul(transformUnityFromMap, transformMapFromEcef);
        }

        public static (double3 position, quaternion rotation) EcefToUnityWorld(double4x4 ecefToUnityTransform, double3 ecefPosition, quaternion ecefRotation)
        {
            var (position, rotation) = ChangeBasisEcefToUnity(ecefPosition, ecefRotation.ToDouble3x3());
            var ecefTransform = Double4x4.FromTranslationRotation(position, rotation);
            var unityTransform = math.mul(ecefToUnityTransform, ecefTransform);
            return (unityTransform.Position().ToFloats(), unityTransform.Rotation());
        }

        public static (double3 position, quaternion rotation) UnityWorldToEcef(double4x4 unityToEcefTransform, double3 position, quaternion rotation)
        {
            var unityTransform = Double4x4.FromTranslationRotation(position, rotation);
            var ecefTransform = math.mul(unityToEcefTransform, unityTransform);
            var (ecefPosition, ecefRotation) = ChangeBasisUnityToEcef(ecefTransform.Position(), ecefTransform.Rotation().ToDouble3x3());
            return (ecefPosition, ecefRotation.ToQuaternion());
        }

        // WGS84 semi-axes (meters)
        private const double A = 6378137.0;
        private const double B = 6378137.0;
        private const double C = 6356752.314245;

        private static readonly double A2 = A * A;
        private static readonly double B2 = B * B;
        private static readonly double C2 = C * C;

        public static double3 GeodeticSurfaceNormal(double3 ecef)
        {
            double nx = ecef.x / A2;
            double ny = ecef.y / B2;
            double nz = ecef.z / C2;
            double invLen = 1.0 / math.sqrt(nx * nx + ny * ny + nz * nz);
            return new double3(nx * invLen, ny * invLen, nz * invLen);
        }

        public static double3x3 GetEunFrameInEcef(double3 ecefPosition)
        {
            var up = GeodeticSurfaceNormal(ecefPosition);
            var zAxis = new double3(0.0, 0.0, 1.0);
            var yAxis = new double3(0.0, 1.0, 0.0);
            var refAxis = math.abs(up.z) < 0.99 ? zAxis : yAxis;
            var east = math.normalize(math.cross(refAxis, up));
            var north = math.normalize(math.cross(up, east));
            return new double3x3(east, up, north);
        }
    }
}