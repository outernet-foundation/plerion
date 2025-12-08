using Unity.Mathematics;

namespace Plerion.Core
{
    public static class WGS84
    {
        private const double A = 6378137.0;
        private const double B = 6378137.0;
        private const double C = 6356752.314245;

        private static readonly double A2 = A * A;
        private static readonly double B2 = B * B;
        private static readonly double C2 = C * C;

        private static readonly double E2 = (A2 - C2) / A2; // first eccentricity squared
        private static readonly double EP2 = (A2 - C2) / C2; // second eccentricity squared

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

        public static double3 LongitudeLatitudeHeightToEarthCenteredEarthFixed(
            double longitude,
            double latitude,
            double height)
        {
            double sinLat = math.sin(latitude);
            double cosLat = math.cos(latitude);
            double sinLon = math.sin(longitude);
            double cosLon = math.cos(longitude);

            // Radius of curvature in the prime vertical
            double N = A / math.sqrt(1.0 - E2 * sinLat * sinLat);

            double x = (N + height) * cosLat * cosLon;
            double y = (N + height) * cosLat * sinLon;
            double z = (N * (1.0 - E2) + height) * sinLat;

            return new double3(x, y, z);
        }

        /// <summary>
        /// Converts ECEF (x, y, z in meters) to geodetic (longitude, latitude, height)
        /// on the WGS-84 ellipsoid. Longitude and latitude are in radians; height in meters.
        /// </summary>
        public static double3 EarthCenteredEarthFixedToLongitudeLatitudeHeight(double3 ecef)
        {
            double x = ecef.x;
            double y = ecef.y;
            double z = ecef.z;

            double p = math.sqrt(x * x + y * y);

            // Longitude
            double longitude = math.atan2(y, x);

            // Bowring’s formula for latitude
            double theta = math.atan2(A * z, C * p);
            double sinTheta = math.sin(theta);
            double cosTheta = math.cos(theta);

            double numerator = z + EP2 * C * sinTheta * sinTheta * sinTheta;
            double denominator = p - E2 * A * cosTheta * cosTheta * cosTheta;
            double latitude = math.atan2(numerator, denominator);

            double sinLat = math.sin(latitude);
            double N = A / math.sqrt(1.0 - E2 * sinLat * sinLat);
            double height = p / math.cos(latitude) - N;

            return new double3(longitude, latitude, height);
        }

    }
}