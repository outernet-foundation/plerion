
namespace Plerion.VPS
{
    public enum Lighting
    {
        Day,
        Dusk,
        Night
    }

    public static class Settings
    {
        public static bool fallbackToMostRecent = false;
        public static Lighting? lightingConditionOverride = null;
        public static float minimumPositionThreshold = 0.05f;
        public static float minimumRotationThreshold = 2f;
        public static float positionInlierThreshold = 0.05f;
        public static float rotationInlierThreshold = 0.05f;
        public static bool discardBottomHalf = false;
        public static float confidenceFactor = 1.0f;
        public static int ransacHistorySize = 128;
        public static bool useFloorPlaneDetection = true;
    }
}